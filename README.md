# GeoMagSharp

A C# library for geomagnetic field calculations using spherical harmonic models. GeoMag # is a port of the C++ application GeoMag 7.0 distributed by NOAA.

[![Build](https://github.com/StreckerCM/GeoMagSharp/actions/workflows/build.yml/badge.svg)](https://github.com/StreckerCM/GeoMagSharp/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/GeoMagSharp.svg)](https://www.nuget.org/packages/GeoMagSharp)

## Installation

```
dotnet add package GeoMagSharp
```

Or via the NuGet Package Manager:

```
Install-Package GeoMagSharp
```

## Quick Start

```csharp
using GeoMagSharp;

// Load a coefficient file (GeoMag is IDisposable so HDGM native handles get released)
using var geoMag = new GeoMag();
geoMag.LoadModel("WMM2025.COF");

// Configure calculation
var options = new CalculationOptions
{
    Latitude = 45.0,
    Longitude = -93.0,
    StartDate = new DateTime(2025, 7, 1),
    SecularVariation = true,
    CalculationMethod = Algorithm.BGS
};
options.SetElevation(0, Distance.Unit.meter, true);

// Run calculation
geoMag.MagneticCalculations(options);

// Access results
var result = geoMag.ResultsOfCalculation[0];
Console.WriteLine($"Declination: {result.Declination.Value:F2} degrees");
Console.WriteLine($"Inclination: {result.Inclination.Value:F2} degrees");
Console.WriteLine($"Total Field: {result.TotalField.Value:F1} nT");
```

### Async API

```csharp
// Async with progress reporting and cancellation
var cts = new CancellationTokenSource();
var progress = new Progress<CalculationProgressInfo>(p =>
    Console.WriteLine($"{p.PercentComplete:F0}% - {p.StatusMessage}"));

var model = await ModelReader.ReadAsync("WMM2025.COF", progress, cts.Token);
await geoMag.MagneticCalculationsAsync(options, progress, cts.Token);
await geoMag.SaveResultsAsync("output.txt", false, cts.Token);
```

### Date-range validation

By default, calculations strictly validate the requested date against the loaded model's `[MinDate, MaxDate)` range and throw `GeoMagExceptionOutOfRange` when out of bounds. For HDGM specifically, the DLL's actual upper bound is determined at load time via `HdgmDateProbe` (no more silent extrapolation past the model's validity). For research scenarios that need raw extrapolation, opt in via `CalculationOptions.AllowExtrapolation = true`.

## Supported Models

| Model | Type | Source | Included |
|-------|------|--------|----------|
| **WMM** | World Magnetic Model | NOAA | WMM2025.COF, WMM2015.COF |
| **WMMHR** | WMM High Resolution | NOAA | WMMHR.COF (WMMHR-2025) |
| **IGRF** | International Geomagnetic Reference Field | IAGA | IGRF14.COF (through 2030), IGRF13.COF (through 2025), IGRF12.COF (through 2020) |
| **EMM** | Enhanced Magnetic Model | NOAA | No (survey required) |
| **BGGM** | BGS Global Geomagnetic Model | BGS | No (commercial license) |
| **HDGM** | High Definition Geomagnetic Model | NOAA | No (user-supplied DLL, Windows-only) |

Bundled coefficient files are in the `coefficient/` directory. See `coefficient/NOTICE.md` for attribution and download links for non-bundled models.

**HDGM** is *Windows-only* and requires a user-supplied NOAA DLL. It provides a high-degree crustal field (720 for HDGM2017–2020, 790 for HDGM2021–2025, 1040 for HDGM2026 per [CIRES](https://geomag.colorado.edu/geomagnetic-and-electric-field-models)) with per-point uncertainty estimates and a high-resolution survey coverage flag. The library detects the loaded version from the filename and probes the DLL for its actual date range. See [docs/features/hdgm-support/README.md](docs/features/hdgm-support/README.md) for setup instructions.

### Uncertainty Estimation

Uncertainty is auto-populated on each calculation result. Three sources contribute, selected automatically by model type (overridable via `CalculationOptions.UncertaintyPreference`):

| Source | Used by | Coverage |
|---|---|---|
| **WMM native error model** | WMM, WMMHR (default under `Auto`) | All seven field σ; declination σ is location-dependent: `δD = √(C₁² + (C₂/H)²)` per WMM2025-2030 Tech Report Section 3.4 |
| **HDGM per-point** | HDGM (default; populated by the DLL) | All seven field σ; varies per coordinate, plus a `HighResolutionCoverage` flag for NSD-covered regions |
| **ISCWSA Level 1** | IGRF, DGRF, EMM, BGGM, or any model with a `ModelCategoryOverride` | Three field σ (`Declination`, `Inclination`, `TotalField`) plus `BhDependentDec`; per-component fields stay null |

Field names on `GeomagneticUncertainty` mirror `MagneticCalculations` exactly — `result.Uncertainty.Inclination` is the σ for `result.Inclination`. The `Source` enum (`Iscwsa` / `WmmErrorModel` / `Hdgm`) records provenance.

```csharp
geoMag.MagneticCalculations(options);
var u = geoMag.ResultsOfCalculation[0].Uncertainty;

Console.WriteLine($"Source:      {u.Source}");
Console.WriteLine($"Declination: ±{u.Declination:F3}° (1σ)");
Console.WriteLine($"Inclination: ±{u.Inclination:F3}° (1σ)");
Console.WriteLine($"Total field: ±{u.TotalField:F0} nT (1σ)");

// Per-component σ — null for ISCWSA, populated for WMM and HDGM:
if (u.HorizontalIntensity.HasValue)
    Console.WriteLine($"Horizontal:  ±{u.HorizontalIntensity:F0} nT (1σ)");
if (u.NorthComp.HasValue)
    Console.WriteLine($"North comp:  ±{u.NorthComp:F0} nT (1σ)");

// Scale to approximate 2σ:
var u2 = u.ScaleTo(2.0);
```

`UncertaintyModelPreference` overrides the default selection:

```csharp
var options = new CalculationOptions
{
    Latitude = 40.0,
    Longitude = -105.0,
    StartDate = new DateTime(2025, 7, 1),

    // Auto (default): WMM/WMMHR → native error model, HDGM → per-point, others → ISCWSA
    // Iscwsa: force ISCWSA Level 1 for all model types
    // Native: require the native error model (throws if model has none)
    UncertaintyPreference = UncertaintyModelPreference.Auto,

    // For commercial models or in-field referencing, use ISCWSA with an explicit category:
    ModelCategoryOverride = GeomagneticModelCategory.InFieldReference1
};
```

Setting `ModelCategoryOverride` always selects ISCWSA (the override is meaningless for native error models). Auto-detected ISCWSA categories: WMM/IGRF/DGRF → LowResolution, WMMHR/EMM/HDGM → HighResolution.

See [`examples/GeoMagSharp.Example`](examples/GeoMagSharp.Example) for a runnable smoke test.

## Target Frameworks

- .NET Framework 4.8
- .NET Standard 2.0 (compatible with .NET Core 2.0+, .NET 5+)

## API Overview

### Core Classes

- **`GeoMag`** - Main entry point for loading models and running calculations
- **`ModelReader`** - Parses COF/DAT coefficient files into model objects
- **`Calculator`** - Low-level spherical harmonic calculation engine
- **`CalculationOptions`** - Configuration for latitude, longitude, date, elevation, etc.
- **`ModelDiscovery`** - Enumerates loadable model files in a folder (COF, DAT, HDGM .dll)

### Discovering models in a folder

`ModelDiscovery.DiscoverModels(folderPath)` enumerates every loadable model file (`.cof`, `.dat`, HDGM `.dll`) in a folder without knowing each format's filename rules:

```csharp
foreach (var d in ModelDiscovery.DiscoverModels("./coefficients"))
    Console.WriteLine($"{d.DisplayName} ({d.DetectedType}) {d.MinDate}..{d.MaxDate}  degree={d.MaxDegree}");

// e.g.   WMM-2025 (WMM)  2025..2030  degree=12
//        IGRF2025 (IGRF) 1900..2030  degree=13     // latest epoch from multi-epoch IGRF files
//        HDGM2019 (HDGM) 1900..2027  degree=720    // date range probed from DLL; degree from CIRES
```

`ModelDescriptor` carries Tier 1 metadata extracted from the file:

| Property | Source |
|---|---|
| `MaxDegree` | IGRF/DGRF: epoch header. WMM/WMMHR/EMM/BGGM: scanned from coefficient rows. HDGM: filename-keyed CIRES table (720 / 790 / 1040 by release era). |
| `SecularVariationDegree` | IGRF/DGRF epoch header (often differs from main, e.g. 13/8 for IGRF2025). |
| `MinAltitudeKm` / `MaxAltitudeKm` | IGRF/DGRF epoch header. Null for other formats. |
| `ReleaseDate` | WMM/WMMHR first-line date. Null for IGRF/DGRF and HDGM. |
| `EpochCount` | IGRF/DGRF: number of epoch header lines (e.g. 26 for IGRF14). 1 for single-epoch models. |

For multi-epoch IGRF/DGRF, `DisplayName` reflects the latest epoch (e.g. `"IGRF2025"` for IGRF14.COF), with `MinDate`/`MaxDate` covering the full validity range across all contained epochs. Single-epoch models report their header-stated year and a 5-year `MaxDate`. For HDGM `.dll` files, `MinDate`/`MaxDate` are determined by probing the DLL with `hdgmcalc` at year-incremented dates until the sentinel boundary is hit.

Pass `new ModelDiscoveryOptions { UseCache = true }` to populate a `.models.json` cache for fast subsequent startups. The cache is schema-versioned and auto-invalidates when classifier behavior changes between library versions.

`ModelDiscovery.DescribeFile(path)` performs the same inspection on a single file. See `ScanMode`, `ModelDiscoveryOptions`, and `ModelDescriptor` IntelliSense for full options.

### Model Classes

- **`ModelDescriptor`** - Read-only snapshot returned by `ModelDiscovery` (file path, detected type, display name, validity range)
- **`MagneticModelSet`** - A set of magnetic models (main field + secular variation)
- **`MagneticModelCollection`** - Manages multiple model sets with JSON serialization
- **`MagneticCalculations`** - Calculation results (declination, inclination, field components)

### Result Properties

Each `MagneticCalculations` result contains `MagneticValue` objects with:
- `Value` - The calculated value
- `ChangePerYear` - Secular variation (when `SecularVariation` is enabled)

Available components: `Declination`, `Inclination`, `TotalField`, `HorizontalIntensity`, `NorthComp`, `EastComp`, `VerticalComp`.

`Uncertainty` (a `GeomagneticUncertainty`) carries 1σ values under the same names — see [Uncertainty Estimation](#uncertainty-estimation).

## License

MIT License - see [LICENSE](LICENSE) for details.

Bundled coefficient files are U.S. Government works in the public domain. See [coefficient/NOTICE.md](coefficient/NOTICE.md) for full attribution.
