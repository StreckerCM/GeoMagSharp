# Feature: HDGM 32-bit DLL support for 64-bit consumers

Issue: #38
Branch: `feature/38-hdgm-bitness-bridge`
Target version: **1.8.0** (additive new capability — not a 1.7.x bug fix)

## Why

HDGM2019 (and earlier) ships only a 32-bit DLL. Newer releases (HDGM2025+) may also ship a 64-bit DLL. A 64-bit consuming app (XactSpot-MSA, DownholePro, etc.) cannot `LoadLibrary` a 32-bit DLL — Windows fundamental. Currently `LoadLibraryHdgmInvoker` fails with Win32 error 193 in this case with no fallback. This blocks HDGM evaluation entirely for any 64-bit consumer of GeoMagSharp.

## Goal

Allow any combination of consumer-process bitness × HDGM DLL bitness to work transparently. The existing match-bitness path stays direct P/Invoke (no overhead added). When bitnesses don't match, the library transparently routes through a subprocess bridge.

## Architecture: option 2 + option 4 (subprocess bridge + bitness-aware selection)

Two concerns kept separate, joined by the existing `INativeHdgmInvoker` interface:

| Component | New? | Role |
|---|---|---|
| `HdgmHost.x86` console exe | new | x86 build; owns DLL handle; serves IPC requests |
| `HdgmHost.x64` console exe | new | x64 build of the same — for the inverse case |
| `ProcessBridgeHdgmInvoker` | new (implements existing `INativeHdgmInvoker`) | Spawns and talks to host exe via stdin/stdout |
| `PeBitnessDetector` | new | Reads `IMAGE_FILE_MACHINE_*` from a DLL's PE header |
| `HdgmInvokerFactory` | new (reorganizes `HDGMModelLoader.Load`) | Picks `LoadLibraryHdgmInvoker` or `ProcessBridgeHdgmInvoker` based on bitness match |
| `LoadLibraryHdgmInvoker` | existing | Direct P/Invoke when bitness matches |
| `HDGMCalculationAdapter` | existing | Unchanged — calls `INativeHdgmInvoker.Calculate` polymorphically |

Calling code in `GeoMag.LoadModel` / `HDGMCalculationAdapter` doesn't change.

## IPC protocol

Binary, fixed-width, request/response over stdin/stdout. Single mutex around each call/response pair → inherently ordered, no framing needed.

**Request** (32 bytes, little-endian):

| Bytes | Type | Field |
|---|---|---|
| 0–7 | `double` | latitude |
| 8–15 | `double` | longitude |
| 16–23 | `double` | depth (meters, positive = below MSL) |
| 24–31 | `double` | decimal year |

**Response** (204 bytes, little-endian):

| Bytes | Type | Field |
|---|---|---|
| 0–3 | `int32` | status (0 = ok, non-zero = error from `hdgmcalc`) |
| 4–203 | `double[25]` | outData (the full NOAA hdgmcalc output array) |

**Shutdown:** a request with `latitude = double.NaN` is a sentinel for "exit cleanly". Host returns no response, closes stdout, exits.

Reasons over text format:
- ~5× faster (de)serialize for the per-date sweep case
- No locale / decimal-separator ambiguity
- Byte-exact reproducibility for testing

## Host startup contract

Command: `HdgmHost.x86.exe <dll-path>` (same for `x64`).

First message back from host = single byte status code:

| Byte | Meaning | Follow-on |
|---|---|---|
| `0x00` | Ready — DLL loaded, `hdgmcalc` symbol resolved | (none — ready for requests) |
| `0x01` | `LoadLibrary` failed | 4-byte int32 Win32 error code follows |
| `0x02` | `hdgmcalc` symbol not found | (none) |
| `0x03` | Bitness mismatch between host exe and DLL | (none — defensive; shouldn't normally fire) |

`ProcessBridgeHdgmInvoker` blocks reading this byte during construction; maps non-zero to `GeoMagExceptionModelNotLoaded` with the specific cause and an actionable error message.

## Bitness selection logic

```csharp
internal static INativeHdgmInvoker SelectInvoker(string dllPath)
{
    var dllBits = PeBitnessDetector.Read(dllPath);     // X86 | X64 | Unknown
    var procBits = Environment.Is64BitProcess ? Bitness.X64 : Bitness.X86;

    if (dllBits == Bitness.Unknown)
        throw new GeoMagExceptionModelNotLoaded(
            $"Cannot read PE bitness from '{dllPath}' — file may be corrupt or not a valid DLL.");

    if (dllBits == procBits)
        return new LoadLibraryHdgmInvoker(dllPath);    // direct, fastest path

    // Bitness mismatch — locate matching host exe alongside GeoMagSharp.dll
    string hostExe = FindHostExe(dllBits);
    if (hostExe == null)
        throw new GeoMagExceptionModelNotLoaded(
            $"HDGM DLL is {dllBits}, calling process is {procBits}. " +
            $"HdgmHost.{dllBits}.exe not found alongside GeoMagSharp.dll. " +
            $"See docs/features/38-hdgm-bitness-bridge/README.md for setup.");

    return new ProcessBridgeHdgmInvoker(dllPath, hostExe);
}
```

`HDGMModelLoader.Load` calls `SelectInvoker(...)` instead of `new LoadLibraryHdgmInvoker(...)` directly. One-line change there.

## Packaging

GeoMagSharp NuGet package adds:

- `tools/HdgmHost.x86.exe` + `HdgmHost.x86.pdb`
- `tools/HdgmHost.x64.exe` + `HdgmHost.x64.pdb`

Copied to consumer's output dir via `<contentFiles>` + MSBuild target. Both bitnesses always shipped (small) so consumers can use either DLL without configuration.

## Tests

| Test class | Coverage |
|---|---|
| `PeBitnessDetectorTests` | Hand-crafted IMAGE_DOS + IMAGE_NT byte fixtures (one x86, one x64); also a non-PE file → Unknown |
| `HdgmInvokerFactoryTests` | Mocks for `PeBitnessDetector` + file existence; verifies correct invoker class per (dllBits, procBits) combo |
| `ProcessBridgeHdgmInvokerTests` | Tiny test host that echoes deterministic values; verifies request/response round-trip, dispose lifecycle, error-status mapping |
| `HdgmHost_x86_Tests` (gated) | Real 32-bit HDGM2019 DLL + bridge from x64 test process; computes a known point; asserts against NOAA test value within tolerance. Gated on `HDGM_DLL_PATH_X86` env var, skipped otherwise |
| `HdgmHost_x64_Tests` (gated, future) | Same for inverse case once a 64-bit HDGM DLL is available locally |

## Wine extension point

Out of scope for official support (see openbrain knowledge entry "Wine as an HDGM bridge transport"). To keep the door open at zero design cost, the bridge launcher must be **parameterizable**:

- Either `HdgmInvokerFactory` accepts an optional `Func<string, ProcessStartInfo> launcher` argument
- Or `ProcessBridgeHdgmInvoker` reads an environment variable `GEOMAGSHARP_BRIDGE_LAUNCHER` whose value is prepended to the host exe path

Default launcher just runs `HdgmHost.{bits}.exe` directly. Consumers in controlled environments who want to try Wine can set the override to `wine`. The library itself makes no claim about Wine reliability.

This is **scope-included** because parameterization adds maybe 3 lines and is cheaper to do once than to retrofit later.

## Tasks

- [ ] Create `HdgmHost.x86` project (SDK-style csproj, `<RuntimeIdentifier>win-x86</RuntimeIdentifier>`, console exe, no .NET dependency beyond what's needed)
- [ ] Create `HdgmHost.x64` project (same with `win-x64`)
- [ ] Implement host startup contract (first-byte status code, command-line DLL path)
- [ ] Implement host main loop (read 32-byte request, call `hdgmcalc`, write 204-byte response, repeat until shutdown sentinel)
- [ ] `PeBitnessDetector.Read(path)` — parses DOS + NT headers, returns `Bitness.X86 | X64 | Unknown`
- [ ] `PeBitnessDetectorTests` with byte-array fixtures
- [ ] `ProcessBridgeHdgmInvoker` implementing `INativeHdgmInvoker`:
  - [ ] Constructor: spawn host, wait for ready byte, throw on non-zero status
  - [ ] `Calculate(...)`: serialize request, read response, surface non-zero status as `GeoMagExceptionOutOfRange`
  - [ ] `Dispose`: send shutdown sentinel, wait briefly, kill if needed
  - [ ] Thread safety: mutex around request/response
  - [ ] Parameterizable launcher (Wine extension point)
- [ ] `ProcessBridgeHdgmInvokerTests` with echoing test host
- [ ] `HdgmInvokerFactory.SelectInvoker(dllPath)` — bitness check + host exe location
- [ ] `HdgmInvokerFactoryTests` — mocks for file existence + bitness detector
- [ ] Wire `HDGMModelLoader.Load` to call `SelectInvoker`
- [ ] NuGet packaging: `contentFiles` for `tools/HdgmHost.x86.exe` and `x64.exe`, MSBuild target to copy to consumer output dir
- [ ] Documentation: `docs/features/38-hdgm-bitness-bridge/README.md` with consumer-facing setup notes (host exe location, troubleshooting, the Wine extension point)
- [ ] Real-DLL integration test (gated on env var)
- [ ] Update `docs/features/hdgm-support/README.md` to reference this for cross-bitness use cases
- [ ] CHANGELOG entry under 1.8.0

## Completion Criteria

- [ ] All tasks above checked
- [ ] `dotnet test -c Release` passes (unit + gated integration)
- [ ] Real-DLL integration test passes against NOAA HDGM2019 32-bit DLL from a 64-bit test process within published tolerance
- [ ] Sample consumer project (an x64 console app) loads HDGM2019 32-bit DLL through the bridge and produces output matching `HDGM2019_TestValues.txt` for 5 reference points
- [ ] NuGet package includes `tools/HdgmHost.x86.exe` + `tools/HdgmHost.x64.exe`; consumer build copies them to output dir

## Out of scope

- Linux/macOS support (Wine extension point exists at zero cost; not officially supported — see openbrain entry "Wine as an HDGM bridge transport")
- Pure C# port of HDGM. Separate multi-month effort; file as long-term issue when this bridge lands
- Inverse-direction integration test (x86 process + x64 HDGM DLL) blocked on availability of a 64-bit HDGM DLL for local testing — code path will be implemented but only smoke-tested initially

## Workflow

Standard feature → development → preview → main flow. Tag as `v1.8.0` once promoted.

Implementation pass uses the persona rotation defined in `docs/prompts/PERSONAS.md` per CLAUDE.md (feature has new code, new packaging, and new public-facing extension behavior — full rotation appropriate, not the single-IMPLEMENTER shortcut used for smaller scope features).
