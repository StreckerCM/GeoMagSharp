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

// Load a coefficient file
var geoMag = new GeoMag();
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

## Supported Models

| Model | Type | Source | Included |
|-------|------|--------|----------|
| **WMM** | World Magnetic Model | NOAA | WMM2025.COF, WMM2015.COF |
| **WMMHR** | WMM High Resolution | NOAA | WMMHR.COF |
| **IGRF** | International Geomagnetic Reference Field | IAGA | IGRF12.COF |
| **EMM** | Enhanced Magnetic Model | NOAA | No (survey required) |
| **BGGM** | BGS Global Geomagnetic Model | BGS | No (commercial license) |

Bundled coefficient files are in the `coefficient/` directory. See `coefficient/NOTICE.md` for attribution and download links for non-bundled models.

## Target Frameworks

- .NET Framework 4.8
- .NET Standard 2.0 (compatible with .NET Core 2.0+, .NET 5+)

## API Overview

### Core Classes

- **`GeoMag`** - Main entry point for loading models and running calculations
- **`ModelReader`** - Parses COF/DAT coefficient files into model objects
- **`Calculator`** - Low-level spherical harmonic calculation engine
- **`CalculationOptions`** - Configuration for latitude, longitude, date, elevation, etc.

### Model Classes

- **`MagneticModelSet`** - A set of magnetic models (main field + secular variation)
- **`MagneticModelCollection`** - Manages multiple model sets with JSON serialization
- **`MagneticCalculations`** - Calculation results (declination, inclination, field components)

### Result Properties

Each `MagneticCalculations` result contains `MagneticValue` objects with:
- `Value` - The calculated value
- `ChangePerYear` - Secular variation (when enabled)

Available components: `Declination`, `Inclination`, `TotalField`, `HorizontalIntensity`, `NorthComponent`, `EastComponent`, `VerticalComponent`, `GridVariation`.

## License

MIT License - see [LICENSE](LICENSE) for details.

Bundled coefficient files are U.S. Government works in the public domain. See [coefficient/NOTICE.md](coefficient/NOTICE.md) for full attribution.
