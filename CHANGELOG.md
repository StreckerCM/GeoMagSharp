# Changelog

## v1.4.0 (2026-02-11)

First standalone NuGet release of GeoMagSharp, extracted from [GeoMagSharpGUI](https://github.com/StreckerCM/GeoMagSharpGUI).

### Features
- Multi-target: .NET Framework 4.8 and .NET Standard 2.0
- Async API: `ModelReader.ReadAsync()`, `GeoMag.MagneticCalculationsAsync()`, `GeoMag.SaveResultsAsync()`
- Progress reporting via `IProgress<CalculationProgressInfo>` with cancellation token support
- `MagneticModelCollection.LoadAsync()` / `SaveAsync()` for async JSON serialization
- Bundled public domain coefficient files (WMM2025, WMMHR, WMM2015, IGRF12)
- Source Link support for debugging

### Supported Models
- WMM (World Magnetic Model)
- WMMHR (WMM High Resolution)
- IGRF (International Geomagnetic Reference Field)
- EMM (Enhanced Magnetic Model) - user-supplied COF file
- BGGM (BGS Global Geomagnetic Model) - user-supplied COF file
