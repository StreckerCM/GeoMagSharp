# Changelog

## v1.7.2 (2026-05-04)

### API Polish
- **`ModelDescriptor` exposes additional metadata** (#31): six properties that were always present in the file's existing header but previously discarded:
  - `MaxDegree` (main field spherical harmonic degree)
  - `SecularVariationDegree` (often differs from main, e.g. 13/8 for IGRF2025)
  - `MinAltitudeKm` / `MaxAltitudeKm` (altitude validity bounds)
  - `ReleaseDate` (when the model was published)
  - `EpochCount` (number of distinct coefficient epochs — 1 for single-epoch models, N for IGRF/DGRF)
- Extraction is format-aware:
  - **IGRF/DGRF .COF**: degree, SV degree, altitude, and epoch count come from the per-epoch header walk — reuses the existing multi-epoch scan (no extra I/O).
  - **WMM/WMMHR/EMM/BGGM .COF**: `ReleaseDate` parsed from the first-line date; `MaxDegree` scanned from coefficient rows (max `n`); `EpochCount` is 1 by definition.
  - **HDGM .dll**: `MaxDegree` resolved via filename-keyed CIRES lookup (`HdgmModelMetadata`) — the DLL exports only `hdgmcalc` and strips VERSIONINFO, so we cite the CIRES public table (720 for HDGM2017–2020, 790 for 2021–2025, 1040 for 2026). Out-of-range filenames stay null. `EpochCount` is 1 (single fused continuous model with built-in secular variation).
  - **Quick mode**: unchanged; new fields stay null.
- Backwards compatible: existing `ModelDescriptor` constructor calls work unchanged via optional parameters appended to the end. No behavior change for consumers that don't consult the new properties.

### Bug Fixes
- **Cache round-trip dropped Tier 1 fields** (#31): `ModelDiscoveryCacheEntry` didn't carry the new metadata properties, and `ModelDiscovery`'s cache-hit reconstruction passed only the original 6 args to `ModelDescriptor`. First scan populated fields correctly; second scan (cache hit) silently returned them as null. DTO + reconstruction now plumb all 12 fields end-to-end. Cache schema bumped 2 → 5 to invalidate stale entries from prior 1.7.x test builds.

## v1.7.1 (2026-04-29)

### Bug Fixes
- **Multi-epoch IGRF/DGRF DisplayName + dates** (#24): `ModelHeaderInspector` now scans all epoch headers in IGRF/DGRF .COF files and uses the latest epoch label as `DisplayName` (e.g. `"IGRF2025"` for IGRF14.COF) with the file's overall validity range. Previously returned `"IGRF00"` and 1900-1905 for every IGRF generation.
- **Stale cache invalidation** (#26): `.models.json` schema bumped 1 → 2; v1 caches written by 1.7.0 are auto-discarded so consumers see corrected classifier output immediately on upgrade.
- **Filter unclassifiable files** (#27): `ModelDiscovery.DiscoverModels` (Full mode) no longer yields descriptors for empty .cof/.dat or garbled-header files. Quick mode and `DescribeFile` unchanged.

## v1.7.0 (2026-04-28)

### Features
- New **`ModelDiscovery`** API for folder-based model enumeration (#21):
  - `DiscoverModels(folderPath)` — enumerates all loadable model files (`.cof`, `.dat`, HDGM `.dll`)
  - `DescribeFile(path)` — single-file deep inspection
  - `ScanMode` enum: `Quick` (extension-only) or `Full` (header peek + HDGM date probe)
  - `.models.json` cache for fast subsequent startups
  - `ModelDiscoveryOptions` for recursion, cancellation, error callbacks
  - `ModelDescriptor` immutable record returned from discovery APIs

### Documentation
- README accuracy fixes for `IDisposable` semantics and bundled coefficient files (#23)

## v1.6.0 (2026-04-28)

### Features
- **HDGM (High Definition Geomagnetic Model)** support via NOAA-supplied native DLL (#19): degree-740 crustal field with per-point sigma uncertainty and a high-resolution survey coverage flag. Windows-only; requires user-supplied DLL. See [docs/features/hdgm-support/README.md](docs/features/hdgm-support/README.md) for setup.
- **Depth-adjusted field values** per SPE-128217-MS (#3): dipole depth correction with depth-dependent uncertainty.
- **IGRF cross-validation** tests using BGS IGRF-14 Fortran reference (#12).

### API
- `GeoMag` now implements `IDisposable` to release HDGM native handles
- `MagneticModelSet` now implements `IDisposable`
- `GeomagneticUncertainty` adds per-point sigma and coverage fields (populated for HDGM)
- `knownModels.HDGM` enum value

## v1.5.0 (2026-03-11)

### Features
- **ISCWSA-based geomagnetic uncertainty estimation** (#2): per-result `Uncertainty` populated automatically based on the loaded model type, with `ScaleTo()` for sigma multiplication and `ModelCategoryOverride` on `CalculationOptions` for in-field referencing or commercial models.
- **WMM2025 precision validation tests** (#4) against NOAA official reference values.

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
