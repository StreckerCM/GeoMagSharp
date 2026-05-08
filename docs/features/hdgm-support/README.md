# HDGM Support — User Guide

GeoMagSharp v1.6.0 adds support for NOAA's High Definition Geomagnetic Model (HDGM)
on Windows. This guide explains how to obtain HDGM, configure GeoMagSharp to use it,
and run the integration tests.

## Licensing

HDGM is publicly available from NOAA for **non-commercial use** without restriction.
**Commercial use requires a license agreement with NOAA.**

GeoMagSharp does not redistribute any HDGM-derived artifacts. The library only
provides the dynamic-loading mechanism — the DLL, coefficient data, test values,
and documentation must be obtained separately from NOAA or your authorized vendor.

If you have any uncertainty about your usage's commercial/non-commercial status,
contact NOAA's HDGM support (`hdgm.support@noaa.gov`) for guidance.

## Obtaining the NOAA HDGM DLL

Download the NOAA HDGM2019 (or later) developer package from NCEI. The package
contains:

- `hdgm2019-64.dll` (or `hdgm2019.dll` for 32-bit)
- `HDGM2019_TestValues.txt` (validation data)
- `HDGM_Documentation.pdf` (NOAA's user guide)
- Other files (developer C source, GUI installers, etc. — not needed by GeoMagSharp)

Place the DLL anywhere your application can read it. GeoMagSharp does not impose
a folder convention.

## Bitness selection

NOAA ships both 32-bit and 64-bit DLLs:

| Process bitness | Use this DLL |
|---|---|
| 64-bit (most modern apps) | `hdgm2019-64.dll` |
| 32-bit (legacy net48 apps configured x86) | `hdgm2019.dll` |

A bitness mismatch surfaces as `GeoMagExceptionModelNotLoaded` with a hint
message. Pick the DLL that matches the process bitness of the application
that consumes GeoMagSharp.

## Loading HDGM

```csharp
using (var geo = new GeoMag())
{
    geo.LoadModel(@"C:\NOAA\HDGM2019\hdgm2019-64.dll");

    geo.MagneticCalculations(new CalculationOptions
    {
        Latitude = 40.0,
        Longitude = -100.0,
        StartDate = new DateTime(2020, 6, 1)
    });

    var r = geo.ResultsOfCalculation[0];

    // Standard fields (work for any model)
    Console.WriteLine($"D = {r.Declination.Value:F3}°");
    Console.WriteLine($"F = {r.TotalField.Value:F1} nT");

    // Per-point uncertainty (HDGM populates Source = Hdgm and all 7 component σ).
    // ISCWSA-source uncertainty leaves the per-component fields null.
    Console.WriteLine($"Source: {r.Uncertainty.Source}");
    Console.WriteLine($"σ_D = {r.Uncertainty.Declination:F3}°");
    if (r.Uncertainty.NorthComp.HasValue)
        Console.WriteLine($"σ_X = {r.Uncertainty.NorthComp:F1} nT");
    if (r.Uncertainty.HighResolutionCoverage == true)
        Console.WriteLine("Location has high-resolution NSD survey coverage");
}
```

The `using` block disposes the DLL handle when scope exits. Without `using`,
the handle leaks until process exit (no functional issue, but it's good hygiene).

## Detection rule

`GeoMag.LoadModel(path)` automatically routes a path into the HDGM pipeline if:

- The file extension is `.dll` (case-insensitive), AND
- The filename (without extension) contains the substring `hdgm` (case-insensitive)

Examples that route to HDGM:
- `hdgm2019-64.dll`
- `HDGM2024.DLL`
- `halliburton_hdgm.dll` (vendor rename)
- `C:\NOAA\HDGM2019\hdgm2019-64.dll`

Examples that do NOT route to HDGM:
- `WMM.COF` → existing COF loader
- `IGRF14.DAT` → existing DAT loader
- `hdgm/wmm.dll` → no "hdgm" in filename portion
- `hdgm2019_file.exe` → wrong extension

## Cross-platform behavior

HDGM is **Windows-only**. Calls to `LoadModel` with an HDGM path on Linux or
macOS throw `PlatformNotSupportedException`. The other models (WMM, WMMHR,
IGRF, EMM, DGRF) remain cross-platform — only HDGM is restricted.

## Date validity

HDGM ships with model coefficients for a defined year range (e.g., HDGM2019
covers approximately 1900–2027). GeoMagSharp probes the DLL at load time
(`HdgmDateProbe` calls `hdgmcalc` at year-incremented dates until the
sentinel boundary is hit) and stores the result as `MinDate`/`MaxDate` on
the loaded model. By default, `MagneticCalculations` and
`MagneticCalculationsAsync` validate the requested date against this range
and throw `GeoMagExceptionOutOfRange` when out of bounds.

For research scenarios that need raw extrapolation past the validity end,
opt in via `CalculationOptions.AllowExtrapolation = true`. The HDGM DLL's
internal sentinel (`outData[0] == -99999`) remains the last-line guard
and surfaces as `GeoMagExceptionOutOfRange` regardless of the flag.

## HDGM version detection (1.7.2+)

In addition to the date probe, GeoMagSharp parses the version year from the
filename (`hdgm{year}.dll`) to populate `ModelDescriptor.MaxDegree` from the
[CIRES](https://geomag.colorado.edu/geomagnetic-and-electric-field-models)
public table:

| Filename year | Crustal degree |
|---|---|
| 2017–2020 | 720 |
| 2021–2025 | 790 |
| 2026 | 1040 |

Files outside the CIRES-published range yield `null` for `MaxDegree`. The
NOAA DLL itself exports no metadata (`hdgmcalc` is the only symbol), so this
filename heuristic is the only citable path.

## Integration tests

GeoMagSharp's HDGM integration tests are gated on environment variables.
The tests skip silently if either variable is unset:

```bash
HDGM_DLL_PATH=C:\NOAA\HDGM2019\hdgm2019-64.dll
HDGM_TEST_VALUES_PATH=C:\NOAA\HDGM2019\HDGM2019_TestValues.txt

dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj \
  --filter "TestCategory=RequiresHDGMDll"
```

CI runs unit tests only, with `--filter "TestCategory!=RequiresHDGMDll"`.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `GeoMagExceptionFileNotFound` | DLL path wrong or file missing |
| `GeoMagExceptionModelNotLoaded` with "Win32 error 193" | Bitness mismatch — wrong DLL for process bitness |
| `GeoMagExceptionModelNotLoaded` with "hdgmcalc symbol not found" | Wrong DLL (not a real HDGM DLL, or unsupported version) |
| `GeoMagExceptionModelNotLoaded` with arbitrary Win32 error | Often antivirus quarantine; check that the DLL exists and is not blocked |
| `GeoMagExceptionOutOfRange` from a calculation | Date or location outside what the loaded HDGM version covers |
| `PlatformNotSupportedException` | Running on Linux/macOS; HDGM is Windows-only |

## Reference

- Design document: `docs/superpowers/specs/2026-04-26-hdgm-support-design.md`
- GitHub issue: #19
- NOAA HDGM contact: `hdgm.support@noaa.gov`
