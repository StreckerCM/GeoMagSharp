# Feature: Expose model metadata via ModelDescriptor (Tier 1)

Issue: #31
Branch: feature/31-model-metadata-tier1
Version bump: 1.7.1 → 1.8.0 (additive API surface)

## Scope

Add five Tier 1 properties to `ModelDescriptor`:
- `MaxDegree` (int?) — main field spherical harmonic degree
- `SecularVariationDegree` (int?) — secular variation degree (often differs from main, e.g. 13/8 for IGRF)
- `MinAltitudeKm` (double?) — lower altitude validity bound
- `MaxAltitudeKm` (double?) — upper altitude validity bound
- `ReleaseDate` (DateTime?) — when the model was published (distinct from validity range)

Tier 2 (`EpochCount`, `Source`, `CoefficientCount`) and Tier 3 (HDGM DLL exports) are deferred to follow-up issues. HDGM DLL metadata research is more involved than COF/DAT parsing.

## Format-specific extraction

| Property | IGRF/DGRF | WMM/WMMHR | HDGM (DLL) |
|---|---|---|---|
| MaxDegree | epoch header parts[2] | scan max `n` in coefficient lines | DLL export (deferred — Tier 3) |
| SecularVariationDegree | epoch header parts[3] | not in file (likely null) | not applicable |
| MinAltitudeKm / MaxAltitudeKm | epoch header parts[7] / parts[8] | not in WMM header (likely null) | DLL export (deferred — Tier 3) |
| ReleaseDate | not typically present | first line parts[2] (e.g. "11/13/2024") | DLL export (deferred — Tier 3) |

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
- Tier 3 HDGM DLL metadata — file as separate issue with NOAA-API research scope
- AutoSize / Layout / GUI consumer updates (will land separately in [GeoMagSharpGUI #61](https://github.com/StreckerCM/GeoMagSharpGUI/issues/61) once 1.8.0 ships)

## Completion Criteria

- [ ] All Tier 1 tasks above checked
- [ ] `dotnet test -c Release --verbosity normal` passes (all existing + new tests)
- [ ] `dotnet pack` produces `GeoMagSharp.1.8.0.nupkg`
- [ ] Manual verification: `ModelDiscovery.DiscoverModels(coefficientFolder)` populates new properties for IGRF12/13/14, WMM2025, WMMHR
