# Feature: Expose model metadata via ModelDescriptor (Tier 1)

Issue: #31
Branch: feature/31-model-metadata-tier1
Version bump: 1.7.1 → 1.7.2 (polish — metadata already in source files, just being discarded)

## Scope

Add five Tier 1 properties to `ModelDescriptor`:
- `MaxDegree` (int?) — main field spherical harmonic degree
- `SecularVariationDegree` (int?) — secular variation degree (often differs from main, e.g. 13/8 for IGRF)
- `MinAltitudeKm` (double?) — lower altitude validity bound
- `MaxAltitudeKm` (double?) — upper altitude validity bound
- `ReleaseDate` (DateTime?) — when the model was published (distinct from validity range)

Tier 2 (`EpochCount`, `Source`, `CoefficientCount`) is deferred to a follow-up issue.

Tier 3 (HDGM metadata) was originally deferred but folded back into 1.7.2 once research established that:
- The HDGM DLL exports only `hdgmcalc` (no metadata getters)
- VERSIONINFO is stripped, PE timestamp is faked (reproducible build)
- The C-source `HDGMheader.h` carries `HDGM_MAX_CRUSTAL_MODEL_DEGREES 740` but that's a max-array-sizing constant under NOAA's developer-package license, not the operative degree
- CIRES (NOAA's research partner) publishes the operative crustal degree per HDGM release year on its public Geomagnetic Models page — citable, no licensing involved

The Tier 3 implementation is a filename-keyed lookup (`HdgmModelMetadata.GetMaxDegreeFromFilename`) that maps `hdgm{year}*.dll` → CIRES-published degree (720 for 2017–2020, 790 for 2021–2025, 1040 for 2026). Out-of-range years return null.

## Format-specific extraction

| Property | IGRF/DGRF | WMM/WMMHR | HDGM (DLL) |
|---|---|---|---|
| MaxDegree | epoch header parts[2] | scan max `n` in coefficient lines | filename-keyed CIRES lookup (Tier 3, in 1.7.2) |
| SecularVariationDegree | epoch header parts[3] | not in file (likely null) | null (not on CIRES public page) |
| MinAltitudeKm / MaxAltitudeKm | epoch header parts[7] / parts[8] | not in WMM header (likely null) | null (not on CIRES public page) |
| ReleaseDate | not typically present | first line parts[2] (e.g. "11/13/2024") | null (only year is publicly stated) |

For multi-epoch IGRF/DGRF: extract from the **last** (latest) epoch's header — that's the degree/altitude relevant to current calculations.

## Tasks

- [x] Bump `Directory.Build.props` 1.7.1 → 1.8.0
- [ ] Add Tier 1 properties to `ModelDescriptor`:
  - Optional constructor parameters (default `null`) — preserves backwards compatibility
  - Public read-only getters
  - Update XML doc comments
- [ ] Update `ModelHeaderInspector.Inspect` for IGRF/DGRF path:
  - `ScanMultiEpochHeaders` already walks the right lines; capture additional fields
  - Track latest epoch's `Nmax`, `Nmax SV`, altitude min, altitude max alongside dates and label
- [ ] Update `ModelHeaderInspector.Inspect` for WMM/WMMHR path:
  - Parse `parts[2]` of first line as `ReleaseDate` (M/d/yyyy format)
  - Add a `ScanMaxDegree` helper that walks coefficient lines and tracks max `n`
- [ ] Update `ModelDiscovery.ClassifyFile` to plumb the new fields through
- [ ] Quick mode behavior: new fields stay `null` (Quick is extension-only)
- [ ] Add unit tests:
  - WMM2025 fixture: ReleaseDate populated, MaxDegree from coefficient scan
  - IGRF14 multi-epoch fixture: MaxDegree, SecularVariationDegree, AltitudeRange from latest epoch header
  - HDGM .dll fixture (if available): all Tier 1 fields stay null (deferred to Tier 3)
- [ ] All existing tests still pass
- [x] Add `HdgmModelMetadata.GetMaxDegreeFromFilename` (filename → CIRES crustal degree)
- [x] Wire `HdgmModelMetadata` into `ModelDiscovery.ClassifyFile` HDGM branch + `DescribeFile`
- [x] Bump cache schema 3 → 4 (HDGM descriptor values now include MaxDegree)
- [x] Tests: parameterized version-to-degree mapping, RT/64-bit suffix variants, out-of-range null behavior, v3 cache invalidation

## Constructor signature

Current:
```csharp
public ModelDescriptor(string filePath, knownModels detectedType, string displayName,
                       double? minDate, double? maxDate, string description = null)
```

Proposed (additive, optional params):
```csharp
public ModelDescriptor(string filePath, knownModels detectedType, string displayName,
                       double? minDate, double? maxDate, string description = null,
                       int? maxDegree = null,
                       int? secularVariationDegree = null,
                       double? minAltitudeKm = null,
                       double? maxAltitudeKm = null,
                       DateTime? releaseDate = null)
```

Existing 5-arg + optional-`description` callers keep working unchanged. The constructor parameter list is getting long but readable — alternative would be a builder, which feels overkill for this scope.

## Workflow

Single IMPLEMENTER pass with TDD: write a failing test (e.g. `Inspect_Wmm2025_HasReleaseDate`), implement, repeat. Skipping full Ralph rotation — same pattern as #24 (no security/UX surface area changing).

## Out of scope

- Tier 2 (`EpochCount`, `Source`, `CoefficientCount`) — file as separate issue if desired
- HDGM SecularVariationDegree, altitude bounds, exact ReleaseDate — these aren't on the CIRES public page; only the developer-package C header has them, and we won't redistribute that
- AutoSize / Layout / GUI consumer updates (will land separately in [GeoMagSharpGUI #61](https://github.com/StreckerCM/GeoMagSharpGUI/issues/61))

## Completion Criteria

- [ ] All Tier 1 tasks above checked
- [ ] `dotnet test -c Release --verbosity normal` passes (all existing + new tests)
- [ ] `dotnet pack` produces `GeoMagSharp.1.8.0.nupkg`
- [ ] Manual verification: `ModelDiscovery.DiscoverModels(coefficientFolder)` populates new properties for IGRF12/13/14, WMM2025, WMMHR
