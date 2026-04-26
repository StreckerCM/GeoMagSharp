# HDGM Support — Design

**Date:** 2026-04-26
**Status:** Draft pending user review
**Target version:** v1.6.0 (MINOR per semver — additive feature, backward-compatible)
**Feature branch:** `feature/<issue-number>-hdgm-support` (to be created)

## 1. Problem statement

GeoMagSharp today supports `.COF` and `.DAT` text-coefficient magnetic models (WMM, WMMHR, IGRF, EMM, DGRF). Users in the oil-and-gas / drilling-software space increasingly need access to NOAA's High-Definition Geomagnetic Model (HDGM), which provides degree-740 crustal-field resolution with per-point uncertainty estimates and a coverage flag indicating airborne/marine-survey vs satellite-only data sources.

HDGM does not fit the existing GeoMagSharp loader path. NOAA distributes HDGM as compiled C source headers consumed at C-compile time and as precompiled Windows binaries (`hdgm2019_file.exe` CLI plus `hdgm2019.dll` / `hdgm2019-64.dll` libraries). There is no `.COF`-equivalent text file. The data is approximately 14 MB across multiple files and is licensed for non-commercial use without restriction; commercial use requires a license agreement with NOAA.

This design adds HDGM support as a Windows-only feature backed by P/Invoke into the user-supplied NOAA DLL, layered alongside the existing pipeline so that WMM/IGRF/EMM/DGRF/WMMHR loading and calculation remain byte-identical.

## 2. Scope

### In scope (this release)

- Loading the user-supplied NOAA HDGM DLL via dynamic native loading (`LoadLibraryEx`)
- Per-spot and date-sweep magnetic-field calculations: D, I, H, F, X, Y, Z plus secular variations
- Per-point σ outputs: σ_D, σ_I, σ_H, σ_X, σ_Y, σ_Z, σ_F (from the NOAA DLL's `outData[17..23]`)
- NSD high-resolution coverage flag (from `outData[16]` in the DLL's output array)
- EGM96-based depth/MSL handling (handled internally by the NOAA DLL — we marshal but do not implement)
- `IDisposable` lifetime management for the native DLL handle
- Filename-based detection rule shared between the loader and the GeoMagSharp.GUI scanner via `[InternalsVisibleTo]`
- Interface-driven seam (`INativeHdgmInvoker`) for unit-testable adapter logic
- Generic-named optional result fields on `Uncertainty` (no HDGM-specific branding) so a future model with similar capabilities can populate them
- Cleanup: `[Obsolete]` mark of the unused `Constants.MaxDeg` and `Constants.MaxCoeff` constants

### Out of scope (deferred or rejected)

- HDGM-RT (real-time magnetospheric/ionospheric variant)
- Pure C# port of the degree-740 spherical harmonic math (Path A in the original review)
- Cross-platform HDGM (Linux/macOS) — NOAA ships only Windows binaries
- Library-level model discovery API (`GeoMag.DiscoverModels(folder)`) — separate follow-up
- HDGM version metadata extraction from filenames or DLL probing
- Per-version date-range tables — sentinel `outData[0] == -99999` is the authority
- Bitness auto-detection — caller picks the correct DLL for process bitness
- Folder-based DLL search / convention-based discovery
- HDGM-derived test data committed to the repository (license posture)
- Custom EXE patches or a forked NOAA source tree
- New exception subtypes for native load failures
- CI integration testing of HDGM (env-var-gated, local-developer-only)
- Performance benchmarks, stress tests, or memory-leak tests

## 3. Approach decision

Three approaches were considered:

| Approach | Outcome |
|---|---|
| Pure C# port of HDGM math (Path A in original review) | **Rejected.** Degree-740 spherical harmonic implementation is high-risk, ~14 MB of coefficients to embed or transcode, and would require porting NOAA's reference C numerics. Cross-platform was the only argument in its favor; that argument fails because HDGM data is itself only realistically available on Windows. |
| Console-app shell-out (Path C in original review) | **Rejected late.** Equivalent platform constraints to DLL P/Invoke. The CLI's text output omits the NSD coverage flag (`outData[16]` is overwritten with `UsePomme` in `hdgm_file.c:204`). Per-call latency is ~150 ms vs microseconds for DLL. |
| **Dynamic-load DLL P/Invoke (chosen)** | NOAA's DLL exposes the full `outData` array including the coverage flag at `outData[16]` (per `HDGM_Sublibrary.c:212`). Single function `hdgmcalc()` with a fixed-size output array — straightforward marshalling. No process-spawn overhead. Same Windows-only constraint as the CLI but with a richer feature surface. |

Windows-only is acceptable because:
- NOAA only distributes HDGM data and binaries for Windows
- The drilling-industry use case is overwhelmingly Windows
- WMM/IGRF/EMM/DGRF/WMMHR remain cross-platform; the constraint is HDGM-specific
- The architecture leaves the seam open (`INativeHdgmInvoker`) for a future cross-platform variant

## 4. Architecture overview

```
                          ┌─────────────────────┐
   GeoMag.LoadModel(path) ─┤ ModelPathDetector   │
                          │  .IsHdgmPath(path)  │
                          └──────┬──────────────┘
                                 │
                ┌────────────────┼─────────────────┐
                │                │                 │
            .COF / .DAT       contains "hdgm"    other
                                 + ".dll"
                │                │                 │
                ▼                ▼                 │
     ┌───────────────────┐  ┌───────────────────┐  │
     │  ModelReader.Read │  │ HDGMModelLoader   │  │
     │  (existing)       │  │ (new)             │  │
     └─────────┬─────────┘  └─────────┬─────────┘  │
               │                      │            │
               ▼                      ▼            │
     ┌───────────────────────────────────────┐     │
     │  MagneticModelSet                     │     │
     │  - existing fields (unchanged)        │     │
     │  - NEW: NativeInvoker  (null for      │     │
     │         non-HDGM models)              │     │
     │  - NEW: IDisposable                   │     │
     └───────────────┬───────────────────────┘     │
                     │                              │
        GeoMag.MagneticCalculations(opts)           │
                     │                              │
            HDGM-mode?  ─── no ────────────────► existing pipeline
                     │
                    yes
                     ▼
     ┌──────────────────────────────────────┐
     │  HDGMCalculationAdapter (new)         │
     │  - per-date loop                      │
     │  - calls invoker.Calculate(...)       │
     │  - maps double[25] → Magnetic-        │
     │    Calculations + Uncertainty fields  │
     └──────────────┬───────────────────────┘
                    │
                    ▼
     ┌──────────────────────────────────────┐
     │  INativeHdgmInvoker (interface)       │
     └──────────────────────────────────────┘
              ▲                       ▲
              │                       │
      ┌───────┴────────┐    ┌─────────┴────────┐
      │ LoadLibrary-   │    │ FakeHdgmInvoker  │
      │ HdgmInvoker    │    │ (test-only)      │
      │  - LoadLibraryEx    │  - canned arrays │
      │  - GetProcAddress   │                  │
      │  - delegate         │                  │
      │  - FreeLibrary      │                  │
      └───────┬────────┘    └──────────────────┘
              │
              ▼
        NOAA hdgm2019-64.dll
        (or 32-bit equivalent)
```

### Key properties

- **Side-door, not refactor.** The existing pipeline is untouched. HDGM has its own loader, adapter, and invoker. The only shared touchpoint is `MagneticModelSet` gaining one optional field.
- **Interface-seamed.** Calc-translation logic (most of the new code) is mockable via `FakeHdgmInvoker`; only the `LoadLibraryEx` layer requires a real DLL to test.
- **Disposable lifetime.** `MagneticModelSet` and `GeoMag` become `IDisposable` so the DLL handle is freed deterministically. Existing usages compile unchanged — disposal of a non-HDGM model set is a no-op.
- **No changes to numerics.** `Calculator.SpotCalculation` is not in the HDGM path at all. Existing model calculations are byte-identical before and after this feature.

### Detection rule

```csharp
internal static bool IsHdgmPath(string path)
{
    if (string.IsNullOrWhiteSpace(path)) return false;
    return Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase)
        && Path.GetFileNameWithoutExtension(path)
            .IndexOf("hdgm", StringComparison.OrdinalIgnoreCase) >= 0;
}
```

Routing matrix:

| Filename | Action |
|---|---|
| `hdgm2019-64.dll` | HDGM branch (LoadLibraryEx) |
| `HDGM2024.DLL` | HDGM branch (LoadLibraryEx) |
| `halliburton_hdgm.dll` (vendor rename) | HDGM branch (LoadLibraryEx) |
| `hdgm2019_file.exe` | Existing branch → rejected as unknown extension |
| `HDGM_TestValues.txt` | Existing branch → rejected as unknown extension |
| `WMM.COF` | Existing COF branch (unchanged) |
| `IGRF14.DAT` | Existing DAT branch (unchanged) |
| `helloworld.dll` | Existing branch → rejected as unknown extension |

## 5. New and modified components

### New files (production)

| File | Purpose |
|---|---|
| `src/GeoMagSharp/HDGM/INativeHdgmInvoker.cs` | Interface contract: `int Calculate(double lat, double lon, double depth, double date, double[] outData)`. Disposable. **Public** — sole HDGM type in the public surface. |
| `src/GeoMagSharp/HDGM/LoadLibraryHdgmInvoker.cs` | Production implementation. Constructor takes DLL path; uses `LoadLibraryEx` + `GetProcAddress` + `Marshal.GetDelegateForFunctionPointer`. Disposable (`FreeLibrary` on `Dispose`). **Internal.** |
| `src/GeoMagSharp/HDGM/HDGMModelLoader.cs` | `static MagneticModelSet Load(string dllPath)`. Validates platform (Windows-only), constructs `LoadLibraryHdgmInvoker`, returns model set with `Type = knownModels.HDGM` and `NativeInvoker` populated. **Internal.** |
| `src/GeoMagSharp/HDGM/HDGMCalculationAdapter.cs` | `static MagneticCalculations Calculate(opts, date, modelSet)`. Calls invoker, maps `outData` indices to `MagneticCalculations` and `Uncertainty` fields. **Internal.** |
| `src/GeoMagSharp/HDGM/HdgmCalcDelegate.cs` | Delegate type matching native `hdgmcalc` signature with `[UnmanagedFunctionPointer(CallingConvention.StdCall)]`. **Internal.** |
| `src/GeoMagSharp/HDGM/Native/Win32NativeMethods.cs` | Thin P/Invoke wrappers: `LoadLibraryEx`, `GetProcAddress`, `FreeLibrary`. **Internal.** |
| `src/GeoMagSharp/HDGM/ModelPathDetector.cs` | `internal static bool IsHdgmPath(string path)`. Single home for HDGM-detection rule. Future home for additional model-detection helpers. |

### New files (tests)

| File | Purpose |
|---|---|
| `tests/GeoMagSharp.Tests/HDGM/FakeHdgmInvoker.cs` | Test double for `INativeHdgmInvoker`. Configurable canned `double[25]` responses, recorded call list. |
| `tests/GeoMagSharp.Tests/HDGM/ModelPathDetectorTests.cs` | Unit tests for the detection rule (~12 tests covering filename patterns). |
| `tests/GeoMagSharp.Tests/HDGM/HDGMCalculationAdapterTests.cs` | Unit tests using `FakeHdgmInvoker` (~20+ tests covering index mapping, sentinel handling, unit conversions). |
| `tests/GeoMagSharp.Tests/HDGM/HDGMModelLoaderTests.cs` | Unit tests for loader paths (filename detection, missing-file errors, platform-not-supported behavior). |
| `tests/GeoMagSharp.Tests/HDGM/HDGMIntegrationTests.cs` | Integration tests requiring the real NOAA DLL via `HDGM_DLL_PATH` env var. Skipped silently if env var not set. |

### Modified files

| File | Change |
|---|---|
| `src/GeoMagSharp/GeoMag.cs` | `LoadModel(path)` calls `ModelPathDetector.IsHdgmPath(path)`; HDGM → `HDGMModelLoader.Load`, else existing. `MagneticCalculations` branches on `_Models.NativeInvoker != null`: HDGM → adapter loop, else existing. Implements `IDisposable`. **No changes to existing-model code paths.** |
| `src/GeoMagSharp/Models/Magnetic/MagneticModelSet.cs` | Add `internal INativeHdgmInvoker NativeInvoker { get; set; }` (null for non-HDGM, `[JsonIgnore]`). Implement `IDisposable`. Existing usages compile unchanged. |
| `src/GeoMagSharp/Models/Results/MagneticCalculations.cs` (or wherever `Uncertainty` lives) | Extend `Uncertainty` with optional nullable: `SigmaD, SigmaI, SigmaH, SigmaX, SigmaY, SigmaZ, SigmaF` (`double?`) and `HighResolutionCoverage` (`bool?`). Generic naming. Null on non-HDGM models. |
| `src/GeoMagSharp/Enums/GeoMagEnums.cs` | Add `knownModels.HDGM = 6` with XML doc noting HighResolution category. |
| `src/GeoMagSharp/UncertaintyDataProvider.cs` (or equivalent) | Add HDGM case returning ISCWSA HRGM-tier σ values (107 nT MFI, 0.16° MDI, 0.30° AZ, 4118 °·nT DBH per ISCWSA Rev5.13) into the existing global Uncertainty fields. The new per-point `SigmaD/I/H/X/Y/Z/F` fields are populated separately from `outData` by the adapter; both global (model-wide) and per-point (location-specific) values coexist on the result. |
| `src/GeoMagSharp/GeoConstants.cs` | `[Obsolete]` mark unused `Constants.MaxDeg` and `Constants.MaxCoeff` with removal notice for v2.0. |
| `src/GeoMagSharp/GeoMagSharp.csproj` (or AssemblyInfo) | Add `[InternalsVisibleTo("GeoMagSharp.GUI")]` (substitute the actual GUI assembly name). |

### Untouched files

- `Calculator.cs` — no changes; HDGM never calls it
- `ModelReader.cs` — no changes; HDGM never enters here
- `MagneticModel.cs` — no changes; HDGM doesn't use the per-degree coefficient storage
- `ExtensionMethods.cs:CheckStringForModel` — no changes; HDGM detection is filename-based, not header-line-based
- All COF/DAT parsing logic — unchanged

### Folder layout

```
src/GeoMagSharp/
├─ HDGM/                              (NEW)
│  ├─ INativeHdgmInvoker.cs           (public)
│  ├─ LoadLibraryHdgmInvoker.cs       (internal)
│  ├─ HDGMModelLoader.cs              (internal)
│  ├─ HDGMCalculationAdapter.cs       (internal)
│  ├─ HdgmCalcDelegate.cs             (internal)
│  ├─ ModelPathDetector.cs            (internal)
│  └─ Native/
│     └─ Win32NativeMethods.cs        (internal)
├─ Models/                            (existing, unchanged structure)
├─ Enums/                             (modified — knownModels.HDGM added)
├─ Calculator.cs                      (unchanged)
├─ GeoMag.cs                          (modified)
├─ GeoConstants.cs                    (modified — Obsolete on MaxDeg/MaxCoeff)
└─ ModelReader.cs                     (unchanged)
```

The `Native/` sub-subfolder isolates Win32 P/Invoke so a future cross-platform native bridge could drop a sibling folder without disturbing existing files.

## 6. Data flow

### Scenario A — Loading an HDGM DLL

```
User: geo.LoadModel("coeffs/hdgm2019-64.dll")

GeoMag.LoadModel(path):
  ├─ ModelPathDetector.IsHdgmPath(path)? → YES
  └─ HDGMModelLoader.Load(path):
       ├─ Verify RuntimeInformation.IsOSPlatform(Windows)
       │    └─ else throw PlatformNotSupportedException
       ├─ Verify File.Exists(path)
       │    └─ else throw GeoMagExceptionFileNotFound
       ├─ var invoker = new LoadLibraryHdgmInvoker(path):
       │    ├─ hModule = LoadLibraryEx(path, 0, 0)
       │    │    └─ if 0 → GetLastError → throw with descriptive message
       │    ├─ fnPtr = GetProcAddress(hModule, "hdgmcalc")
       │    │    └─ if 0 → FreeLibrary → throw "hdgmcalc symbol missing"
       │    └─ delegate = Marshal.GetDelegateForFunctionPointer<HdgmCalcDelegate>(fnPtr)
       └─ return new MagneticModelSet {
              Type = knownModels.HDGM,
              Name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant(),
                                        // e.g. "hdgm2019-64.dll" → "HDGM2019-64"
              MinDate = 1900.0,         // wide-permissive — sentinel is authoritative
              MaxDate = 9999.0,
              NativeInvoker = invoker,
              FileNames = [filename]
            }

  _Models = result
```

### Scenario B — Single spot calc against HDGM

```
User: geo.MagneticCalculations(opts)  // opts has Lat, Lon, Elev, StartDate

GeoMag.MagneticCalculations(opts):
  ├─ _Models.IsDateInRange(opts.StartDate)?  // permissive 1900–9999 for HDGM
  ├─ Branch: _Models.NativeInvoker != null → HDGM path
  └─ for each date in range (1 iteration for spot):
       └─ HDGMCalculationAdapter.Calculate(opts, date, _Models):
            ├─ pack args: lat, lon, depth (meters), date (decimal year)
            ├─ outData = new double[25]
            ├─ status = invoker.Calculate(lat, lon, depth, date, outData)
            │    └─ delegate(...) → native hdgmcalc(...) [microseconds]
            ├─ if outData[0] == -99999 → throw GeoMagExceptionOutOfRange
            └─ map outData → MagneticCalculations:
                 outData[0]  → Declination.Value           (deg)
                 outData[1]  → Inclination.Value           (deg)
                 outData[2]  → TotalField.Value            (nT)
                 outData[3]  → HorizontalIntensity.Value   (nT)
                 outData[4]  → NorthComp.Value             (nT)
                 outData[5]  → EastComp.Value              (nT)
                 outData[6]  → VerticalComp.Value          (nT)
                 outData[7]  → (Grid Variation, discarded)
                 outData[8]  → Declination.ChangePerYear
                 outData[9]  → Inclination.ChangePerYear
                 outData[10] → TotalField.ChangePerYear
                 outData[11] → HorizontalIntensity.ChangePerYear
                 outData[12] → NorthComp.ChangePerYear
                 outData[13] → EastComp.ChangePerYear
                 outData[14] → VerticalComp.ChangePerYear
                 outData[15] → (dGV/dt, discarded)
                 outData[16] → Uncertainty.HighResolutionCoverage = (val == 0)
                 outData[17] → Uncertainty.SigmaD
                 outData[18] → Uncertainty.SigmaI
                 outData[19] → Uncertainty.SigmaH
                 outData[20] → Uncertainty.SigmaX
                 outData[21] → Uncertainty.SigmaY
                 outData[22] → Uncertainty.SigmaZ
                 outData[23] → Uncertainty.SigmaF
                 outData[24] → (UsePomme HDGM-RT flag, discarded)
            └─ also populate Uncertainty global ISCWSA HRGM-tier fields
       └─ ResultsOfCalculation.Add(result)
```

**Critical implementation note:** Index 16's semantics differ between the DLL (`HDGM_Sublibrary.c:212` writes `IsNotCovered`) and the CLI (`hdgm_file.c:204` overwrites with `UsePomme`). The DLL is authoritative; mark this in `HDGMCalculationAdapter.cs` with an inline comment citing the DLL source line number to prevent regressions.

### Scenario C — Date sweep against HDGM

```
opts = { StartDate=2010-01-01, EndDate=2015-12-31, StepInterval=365 }
geo.MagneticCalculations(opts)

  6 iterations (2010, 2011, 2012, 2013, 2014, 2015)
  each iteration: invoker.Calculate(...) → ~µs of native work
  total: 6 native calls, all in-process, ~ms wall-clock
```

Same code shape as the existing `while`-loop in `GeoMag.cs:136-159`. No batching infrastructure needed because per-call overhead is microseconds.

### Scenario D — Disposal of HDGM DLL handle

```csharp
using (var geo = new GeoMag())
{
    geo.LoadModel("hdgm2019-64.dll");
    geo.MagneticCalculations(opts);
}  // ← Dispose called

GeoMag.Dispose():
  └─ _Models?.Dispose():
       └─ NativeInvoker?.Dispose():
            └─ FreeLibrary(hModule)

For non-HDGM models (e.g., WMM):
  _Models.NativeInvoker == null
  Dispose chain becomes a no-op
```

### Scenario E — Loading WMM (regression check)

```
geo.LoadModel("coefficients/WMM.COF")

  ├─ ModelPathDetector.IsHdgmPath("WMM.COF")? → NO (extension is .COF)
  └─ existing ModelReader.Read(path)  ← unchanged path

geo.MagneticCalculations(opts)
  ├─ _Models.NativeInvoker == null → existing path
  └─ existing while-loop calling Calculator.SpotCalculation  ← unchanged
```

Byte-for-byte identical results vs. before this feature.

### Cross-cutting concerns

**Cancellation.** `CancellationToken.ThrowIfCancellationRequested()` is called between per-date iterations in the loop. Native calls themselves are too fast to cancel mid-call (microseconds). For a 1000-date sweep with mid-sweep cancel, latency to honor cancel is bounded by one `hdgmcalc` invocation.

**Async.** Existing `MagneticCalculationsAsync` wraps in `Task.Run`. HDGM path runs through the same wrapper without modification.

**Thread safety.** The NOAA DLL is not documented as thread-safe. `LoadLibraryHdgmInvoker.Calculate` guards with a `lock(_syncRoot)` so two concurrent `MagneticCalculations` calls on the same model set serialize at the native boundary. Multi-instance use (different DLL handles) is fine because each handle is a separate calculation context.

## 7. Public API surface

### New public types

| Type | Visibility | Purpose |
|---|---|---|
| `INativeHdgmInvoker` | public interface | Seam for advanced consumers wanting a custom native bridge. Default users never touch it directly. |
| All other HDGM types | internal | Implementation details — users go through `LoadModel`. |

### Additions to existing types

```csharp
public class Uncertainty
{
    // existing global ISCWSA fields unchanged
    
    /// <summary>Per-point σ for declination (degrees). Null if model does not provide per-point uncertainty.</summary>
    public double? SigmaD { get; set; }
    public double? SigmaI { get; set; }
    public double? SigmaH { get; set; }
    public double? SigmaX { get; set; }
    public double? SigmaY { get; set; }
    public double? SigmaZ { get; set; }
    public double? SigmaF { get; set; }
    
    /// <summary>True if location has high-resolution survey coverage (28 km half-wavelength). False for satellite-only fallback. Null if the model does not provide coverage information.</summary>
    public bool? HighResolutionCoverage { get; set; }
}

public enum knownModels
{
    NONE = 0, DGRF = 1, EMM = 2, IGRF = 3, WMM = 4, WMMHR = 5,
    HDGM = 6,   // NEW — HighResolution category, Windows-only via NOAA DLL
}

public class MagneticModelSet : IDisposable
{
    // existing members unchanged
    [JsonIgnore]
    internal INativeHdgmInvoker NativeInvoker { get; set; }
    public void Dispose();    // no-op for non-HDGM models
}

public class GeoMag : IDisposable
{
    // existing members unchanged; signatures preserved
    public void Dispose();    // propagates to _Models
}
```

### Removed / obsoleted public surface

```csharp
public static class Constants
{
    [Obsolete("No longer used; calculator sizes dynamically from the loaded model. Will be removed in a future major version.")]
    public const Int32 MaxDeg = 20;
    
    [Obsolete("No longer used; see MaxDeg.")]
    public static Int32 MaxCoeff { get { ... } }
}
```

### Sample HDGM call site

```csharp
using (var geo = new GeoMag())
{
    geo.LoadModel(@"C:\NOAA\HDGM2019\hdgm2019-64.dll");    // auto-detects HDGM
    
    geo.MagneticCalculations(new CalculationOptions
    {
        Latitude = 40.0,
        Longitude = -100.0,
        ElevationFt = 1000,
        StartDate = new DateTime(2020, 6, 1)
    });
    
    var r = geo.ResultsOfCalculation[0];
    
    // Standard fields work identically to WMM/IGRF/etc.
    Console.WriteLine($"D = {r.Declination.Value:F3}°");
    
    // HDGM-specific extras — null on other models
    if (r.Uncertainty.SigmaD.HasValue)
        Console.WriteLine($"σ_D = {r.Uncertainty.SigmaD:F3}°");
    
    if (r.Uncertainty.HighResolutionCoverage == true)
        Console.WriteLine("Location has high-resolution survey coverage");
}
```

### Compile and run impact

| Consumer scenario | Effect |
|---|---|
| Existing C# code using GeoMag for WMM/IGRF | Compiles unchanged. Runs unchanged. Same numerics. |
| Existing JSON-serialized `MagneticModelSet` files | Deserialize unchanged. New `NativeInvoker` is `[JsonIgnore]`. |
| Existing JSON-serialized `MagneticCalculations` results | Deserialize unchanged with new σ/coverage fields defaulting to null. |
| Existing NuGet consumers on .NET Framework 4.8 | Same package; new feature available, opt-in. |
| Existing NuGet consumers on .NET Standard 2.0 / .NET 5+ on Linux/macOS | Same package; HDGM throws `PlatformNotSupportedException`; everything else works. |

## 8. Error handling and edge cases

### Failure modes

| Scenario | Where | Exception | Message intent |
|---|---|---|---|
| File doesn't exist | `HDGMModelLoader.Load` | `GeoMagExceptionFileNotFound` | Standard "file not found at path X" |
| Wrong platform (Linux/macOS) | `HDGMModelLoader.Load` | `PlatformNotSupportedException` | "HDGM is supported only on Windows. Path: X" |
| File is locked | `HDGMModelLoader.Load` (existing `IsFileLocked` check) | `GeoMagExceptionOpenError` | Reuse existing format |
| `LoadLibraryEx` returns 0, GetLastError = 193 | `LoadLibraryHdgmInvoker` ctor | `GeoMagExceptionModelNotLoaded` | "Failed to load HDGM DLL: bitness mismatch (process is 64-bit, DLL is 32-bit). Use hdgm2019-64.dll or run as 32-bit." |
| `LoadLibraryEx` returns 0, other Win32 error | `LoadLibraryHdgmInvoker` ctor | `GeoMagExceptionModelNotLoaded` | "Failed to load HDGM DLL: Win32 error N (descriptive message via FormatMessage)" |
| `GetProcAddress` returns 0 | `LoadLibraryHdgmInvoker` ctor | `GeoMagExceptionModelNotLoaded` | "DLL loaded but `hdgmcalc` symbol not found. Likely not a valid HDGM DLL." Frees handle before throwing. |
| `hdgmcalc` returns sentinel `outData[0] == -99999` | `HDGMCalculationAdapter.Calculate` | `GeoMagExceptionOutOfRange` | "HDGM returned out-of-range result for date {date} at lat {lat}, lon {lon}. The loaded HDGM version may not cover this date, or the location is invalid. Source DLL: {filename}" |
| Date outside `MagneticModelSet.MinDate/MaxDate` | `GeoMag.MagneticCalculations` (existing pre-flight) | `GeoMagExceptionOutOfRange` | Reuse existing format. HDGM defaults are 1900–9999 (sentinel-driven validation). |
| Latitude outside ±89.999° | `HDGMCalculationAdapter.Calculate` (input validation) | `GeoMagExceptionOutOfRange` | Reuse existing pole-clamp logic from `GeoConstants.ThreeHundredFeetFromXPole` |
| `MagneticCalculations` called after `Dispose` | `GeoMag.MagneticCalculations` | `ObjectDisposedException` | Standard .NET pattern |
| Native call throws AccessViolation | inside delegate invocation | `SEHException` wrapped in `GeoMagExceptionModelNotLoaded` | "Native HDGM call failed unexpectedly. The DLL may be corrupted or incompatible." |
| Concurrent `MagneticCalculations` on one model set | inside `LoadLibraryHdgmInvoker.Calculate` | serialized via `lock` | Calls block, do not throw |
| User passes path with "hdgm" in name but `.exe` extension | `GeoMag.LoadModel` (detection rule mismatch) | `GeoMagExceptionModelNotLoaded` | Routes to `ModelReader.Read` → "file type '.exe' not supported" |

### Edge cases

**HDGM model versioning.** No filename-based version inference. `MagneticModelSet.MinDate=1900.0` and `MaxDate=9999.0` for HDGM-loaded sets. The DLL's sentinel return is the authoritative date validator. New NOAA releases (HDGM2024, HDGM2025) work without GeoMagSharp code changes — drop in the new DLL.

**Filename collision (false positives).** A user-named DLL like `myhdgmtools.dll` matches the rule but isn't HDGM. `GetProcAddress("hdgmcalc")` fails and we throw cleanly — no silent corruption.

**Multiple model sets, one DLL.** Two `MagneticModelSet` instances loading the same DLL each call `LoadLibraryEx` independently; the OS reference-counts. Disposing one decrements; disposing both unloads.

**App lifetime DLL loaded but never disposed.** Handle leaks until process exit; equivalent to forgetting `using` on a `FileStream`. Documented in the `using` example pattern.

**Antivirus quarantines the DLL.** Common with NOAA binaries on aggressive corporate AV. `LoadLibraryEx` fails with a generic Win32 error. Exception message includes `FormatMessage` text plus a hint: "If the file exists and is the correct bitness, check that antivirus has not quarantined it."

**Cross-platform consumer that never calls HDGM.** Linux developer using only WMM never hits an HDGM code path. Everything works. `PlatformNotSupportedException` only fires on explicit HDGM `LoadModel` calls.

### Explicitly not caught

- Numerical errors deep inside the NOAA DLL — propagate as DLL returns
- NOAA DLL signature changes in future HDGM releases — `GetProcAddress("hdgmcalc")` succeeds based on symbol name; signature drift would marshal incorrectly. Documented in README that we target HDGM2019's signature; future versions need re-validation.
- DLL handle leaks across `AppDomain.Unload` (legacy `net48` only)

## 9. Testing strategy

### Test pyramid

```
                    ┌─────────────────────────┐
                    │  Integration tests      │   manual / opt-in via env var
                    │  (real NOAA DLL)        │   ~10 tests, validate against
                    │                         │   HDGM2019_TestValues.txt
                    └─────────────────────────┘
                  ┌────────────────────────────────┐
                  │  Adapter unit tests            │  CI, every commit
                  │  (FakeHdgmInvoker)             │  ~20+ tests covering
                  │                                │  index mapping, sentinels
                  └────────────────────────────────┘
              ┌──────────────────────────────────────┐
              │  Path detector / loader unit tests   │  CI, every commit
              │  (no DLL needed)                     │  ~15 tests covering
              │                                      │  filename rules, paths
              └──────────────────────────────────────┘
```

### Layer 1 — Path detector tests (CI, no DLL)

`tests/GeoMagSharp.Tests/HDGM/ModelPathDetectorTests.cs`

Test cases (Method_Scenario_Expected naming per CLAUDE.md):

- `IsHdgmPath_ExactMatch_ReturnsTrue` — "hdgm2019-64.dll"
- `IsHdgmPath_UpperCaseExtension_ReturnsTrue` — "hdgm2019-64.DLL"
- `IsHdgmPath_UpperCaseFilename_ReturnsTrue` — "HDGM2019-64.dll"
- `IsHdgmPath_VendorRename_ReturnsTrue` — "halliburton_hdgm.dll"
- `IsHdgmPath_FullPath_ReturnsTrue` — "C:/foo/bar/hdgm.dll"
- `IsHdgmPath_NoHdgmInName_ReturnsFalse` — "WMM.dll"
- `IsHdgmPath_HdgmInDirectoryNotFile_ReturnsFalse` — "hdgm/wmm.dll"
- `IsHdgmPath_HdgmExe_ReturnsFalse` — "hdgm2019_file.exe"
- `IsHdgmPath_HdgmTxt_ReturnsFalse` — "HDGM_readme.txt"
- `IsHdgmPath_NoExtension_ReturnsFalse` — "hdgm"
- `IsHdgmPath_EmptyString_ReturnsFalse`
- `IsHdgmPath_Null_ReturnsFalse`

### Layer 2 — Adapter unit tests (CI, FakeHdgmInvoker)

`tests/GeoMagSharp.Tests/HDGM/HDGMCalculationAdapterTests.cs`

```csharp
internal class FakeHdgmInvoker : INativeHdgmInvoker
{
    public double[] CannedOutData { get; set; } = new double[25];
    public int CannedReturnValue { get; set; } = 0;
    public List<CalculationCall> Calls { get; } = new List<CalculationCall>();
    
    public int Calculate(double lat, double lon, double depth, double date, double[] outData)
    {
        Calls.Add(new CalculationCall { Lat = lat, Lon = lon, Depth = depth, Date = date });
        Array.Copy(CannedOutData, outData, 25);
        return CannedReturnValue;
    }
    
    public void Dispose() { }
}
```

Test cases (representative subset):

- `Adapter_MapsDeclination_ToOutData0`
- `Adapter_MapsInclination_ToOutData1`
- `Adapter_MapsTotalField_ToOutData2`
- `Adapter_MapsHorizontalIntensity_ToOutData3`
- `Adapter_MapsNorthComp_ToOutData4`
- `Adapter_MapsEastComp_ToOutData5`
- `Adapter_MapsVerticalComp_ToOutData6`
- `Adapter_MapsDeclinationChangePerYear_ToOutData8` — (note: NOT `outData[7]`, which is GV)
- `Adapter_MapsCoverageFlag_FromOutData16_HighRes_True` — `outData[16] == 0`
- `Adapter_MapsCoverageFlag_FromOutData16_Fallback_False` — `outData[16] == 1`
- `Adapter_MapsSigmaD_FromOutData17`
- `Adapter_MapsSigmaI_FromOutData18`
- `Adapter_MapsSigmaH_FromOutData19`
- `Adapter_MapsSigmaX_FromOutData20`
- `Adapter_MapsSigmaY_FromOutData21`
- `Adapter_MapsSigmaZ_FromOutData22`
- `Adapter_MapsSigmaF_FromOutData23`
- `Adapter_SentinelMinus99999_ThrowsOutOfRange`
- `Adapter_LatPassedToInvoker_MatchesOptions`
- `Adapter_LonPassedToInvoker_MatchesOptions`
- `Adapter_DepthPassedAsMeters_NotFeet` — unit conversion verification
- `Adapter_DateDecimalYear_MatchesIntervalDate`
- `Adapter_DateSweep_CallsInvokerOncePerDate`

### Layer 3 — Integration tests (manual, real DLL)

`tests/GeoMagSharp.Tests/HDGM/HDGMIntegrationTests.cs`

```csharp
[TestClass]
public class HDGMIntegrationTests
{
    private static string DllPath => Environment.GetEnvironmentVariable("HDGM_DLL_PATH");
    private static string TestValuesPath => Environment.GetEnvironmentVariable("HDGM_TEST_VALUES_PATH");
    
    [TestInitialize]
    public void RequireDllAndTestValues()
    {
        if (string.IsNullOrEmpty(DllPath) || !File.Exists(DllPath))
            Assert.Inconclusive("HDGM_DLL_PATH not set; integration tests skipped.");
        if (string.IsNullOrEmpty(TestValuesPath) || !File.Exists(TestValuesPath))
            Assert.Inconclusive("HDGM_TEST_VALUES_PATH not set; integration tests skipped.");
    }
    
    [TestMethod, TestCategory("RequiresHDGMDll")]
    public void Integration_LoadsRealDll_NoException();
    
    [TestMethod, TestCategory("RequiresHDGMDll")]
    public void Integration_SinglePoint_MatchesTestValues_WithinTolerance();
    
    [TestMethod, TestCategory("RequiresHDGMDll")]
    public void Integration_SamplePointsFromTestValues_AllWithinTolerance();
    
    [TestMethod, TestCategory("RequiresHDGMDll")]
    public void Integration_DateSweep_ReturnsExpectedNumberOfRows();
    
    [TestMethod, TestCategory("RequiresHDGMDll")]
    public void Integration_OutOfRangeDate_ThrowsOutOfRange();
    
    [TestMethod, TestCategory("RequiresHDGMDll")]
    public void Integration_DisposeFreesDll();
    
    [TestMethod, TestCategory("RequiresHDGMDll")]
    public void Integration_SigmaValuesPopulated();
    
    [TestMethod, TestCategory("RequiresHDGMDll")]
    public void Integration_NSDCoverageFlag_ReturnsBoolForKnownLocations();
}
```

### Tolerances

| Field | Tolerance | Rationale |
|---|---|---|
| D, I (degrees) | 0.0001° | NOAA prints to 5 decimal places; double round-trip |
| H, F, X, Y, Z (nT) | 0.05 nT | NOAA prints to 1 decimal place |
| dD/dt etc. | 0.001 (deg or nT)/yr | Small values, looser tolerance |
| σ values | 0.01 | Lower precision in NOAA's printout |

### License posture

**No HDGM-derived data is committed to the repository.** This includes the DLL, test values, model coefficient files, and documentation. HDGM is publicly available for non-commercial use but requires a license agreement for commercial use; redistribution of HDGM-derived artifacts is intentionally avoided.

End users obtain HDGM independently from NOAA / their vendor. Integration tests require both `HDGM_DLL_PATH` and `HDGM_TEST_VALUES_PATH` env vars; tests skip silently via `Assert.Inconclusive` if not set.

`.gitignore` adds defensive entries:
```
*.dll
HDGM*.txt
HDGM*.pdf
```
(refined to not block legitimate test fixtures or this design doc itself)

`docs/features/hdgm-support/README.md` documents the license posture and the steps to obtain HDGM independently.

### CI configuration

CI runs unit tests only:

```yaml
- name: Run tests
  run: dotnet test --filter "TestCategory!=RequiresHDGMDll" --verbosity normal
```

A future self-hosted Windows runner with the maintainer's NOAA license could run integration tests on a schedule; that infrastructure is deferred (out of scope for this release).

### Out of scope for tests

- Performance benchmarks (HDGM is microseconds)
- Stress tests for concurrent calls (single `lock`, straightforward)
- Memory leak tests
- Cross-bitness validation (caller's responsibility)

## 10. Versioning, packaging, and process

| Item | Resolution |
|---|---|
| Semver bump | **Minor** — v1.5.0 → **v1.6.0** |
| `Directory.Build.props` `VersionPrefix` | Update as first commit on feature branch |
| Target frameworks | Unchanged — `net48` + `netstandard2.0` |
| New NuGet dependencies | None |
| `[InternalsVisibleTo]` directive | Add for the GeoMagSharp.GUI assembly |
| Ralph Loop requirements | Required per CLAUDE.md: GitHub issue, `feature/<n>-hdgm-support` branch from `development`, `docs/features/hdgm-support/tasks.md`, draft PR, rotating-persona Ralph Loop with 2 clean cycles before merge |
| README update | Add HDGM to supported-models list with Windows-only callout and example |
| CLAUDE.md update | Project Overview's models list |
| `docs/features/hdgm-support/README.md` (NEW) | User-facing guide: obtaining the DLL, env vars for tests, license posture |
| `docs/features/hdgm-support/tasks.md` (NEW) | Per Ralph Loop format |

## 11. Deferred follow-ups (separate PRs)

| Follow-up | Brief |
|---|---|
| `GeoMag.DiscoverModels(folder)` API | Library-level discovery returning `ModelDescriptor` records. Replaces GUI's local scanner. |
| BGGM2019+ support | Next HRGM-tier model. Architecture should reuse the same `INativeHdgmInvoker`-style seam if BGS distributes a similar binary. |
| HDGM-RT support | Real-time magnetospheric/ionospheric variant. Likely a `UseRealTime` flag on `CalculationOptions`, mapping to the DLL's `UsePomme/UseDifi` parameters and a real-time data feed. |
| Pure C# port (cross-platform) | If demand emerges. Architecture in this PR (`INativeHdgmInvoker`) leaves the seam open. |
| CI infrastructure for HDGM integration tests | Self-hosted Windows runner with org's NOAA license; nightly schedule. |
| `Constants.MaxDeg/MaxCoeff` removal in v2.0 | After `[Obsolete]` warning period. |

## 12. References

- `HDGM_Sublibrary.c:212` — DLL output array index 16 = `IsNotCovered` (NSD coverage flag)
- `HDGM_Sublibrary.c:46` — `__declspec(dllexport) int __stdcall hdgmcalc(...)` signature
- `hdgm_file.c:204` — CLI overwrites `outData[16]` with `UsePomme` (different semantics from DLL)
- `HDGMheader.h:32-33` — `START_YEAR=1900`, `NUMBER_MODEL_YEARS=120`
- `Calculator.cs:78-80` — existing dynamic-degree sizing from `internalSH.MaxDegree`
- `MagneticModel.cs:78-90` — existing dynamic max-degree computation from coefficient count
- ISCWSA Rev5.13 HRGM-tier σ values (107 nT MFI, 0.16° MDI, 0.30° AZ constant, 4118 °·nT DBH) — openbrain KB#70, KB#105
- NOAA HDGM2019 Documentation — `HDGM_Documentation.pdf` in the user's developer package
- NOAA HDGM license terms — non-commercial use is freely permitted; commercial use requires NOAA license agreement
