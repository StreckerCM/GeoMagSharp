# Feature: Fix ModelHeaderInspector for multi-epoch IGRF/DGRF .COF files

Issue: #24 (multi-epoch DisplayName + dates) + #26 (cache invalidation on classifier change)
Branch: feature/24-igrf-dgrf-display-name
Version bump: 1.7.0 → 1.7.1 (patch)

## Background

`ModelHeaderInspector.Inspect()` was designed for single-epoch model files (WMM, EMM) where the first non-blank line specifies the model and a single validity year. For **IGRF and DGRF .COF files, which contain many 5-year epoch blocks within a single file**, only the *first* epoch (the oldest) is read — yielding `DisplayName = "IGRF00"` and `MaxDate = 1905` regardless of which IGRF generation the file represents.

## Approach

When `type == knownModels.IGRF || type == knownModels.DGRF`:
1. Scan all lines in the file
2. Identify epoch header lines: line starting with whitespace + `IGRF` or `DGRF` + alphanumeric epoch label, with at least 7 whitespace-separated tokens
3. The latest epoch is the last such line in the file (file ordering is chronological)
4. `DisplayName` = first token of the last epoch header line (e.g. `IGRF2025`, `DGRF2020`)
5. `MinDate` = parts[5] of the first epoch line (start year of oldest epoch)
6. `MaxDate` = parts[6] of the last epoch line (end year of newest epoch)

Single-epoch models (WMM, EMM, BGGM) keep their existing fast-path: read first line only, derive DisplayName from `firstLine.IndexOf(type)`.

## Tasks

- [x] Bump `Directory.Build.props` 1.7.0 → 1.7.1
- [ ] Add `ScanMultiEpochHeader` private helper in `ModelHeaderInspector.cs`
  - Returns `(string lastLabel, double? minDate, double? maxDate)`
  - Handles both 2-digit (IGRF00) and 4-digit (IGRF2025) epoch labels
  - Skips malformed lines silently (continues scan)
- [ ] Update `Inspect()` to dispatch on type:
  - IGRF/DGRF → call `ScanMultiEpochHeader`
  - Other types → existing single-line code path
- [ ] Add MSTest unit tests covering:
  - IGRF12.COF → DisplayName "IGRF2015", MinDate 1900, MaxDate 2020
  - IGRF13.COF → DisplayName "IGRF2020", MinDate 1900, MaxDate 2025
  - IGRF14.COF → DisplayName "IGRF2025", MinDate 1900, MaxDate 2030
  - WMM2025.COF → existing behavior preserved (DisplayName "WMM-2025")
- [ ] Build + run all existing tests (no regressions)
- [ ] Repack `GeoMagSharp.1.7.1.nupkg` to artifacts/

### Cache invalidation (#26)

Discovered while smoke-testing #24's fix in [GeoMagSharpGUI #58](https://github.com/StreckerCM/GeoMagSharpGUI/pull/58): a v1 `.models.json` cache written by 1.7.0 contains buggy `"IGRF00"` entries; on upgrade to 1.7.1, the cache hit logic (path + size + mtime) reuses the stale entries verbatim, hiding the fix. Bundling the cache invalidation into the same release means consumers see the corrected classifier output immediately on upgrade rather than after a manual `.models.json` delete.

- [x] Bump `ModelDiscoveryCache.CurrentSchemaVersion` 1 → 2 (with version-history comment block explaining the trigger)
- [x] Add `Load_LegacyV1Cache_DiscardedAfterSchemaBumpToV2` test that writes a v1 cache with realistic stale entries and asserts `TryLoad` returns empty
- [x] Existing `Load_WrongSchemaVersion_ReturnsEmptyList` test still covers the strict-equality check generally

## Completion Criteria

- [ ] All tasks above checked
- [ ] `dotnet test -c Release --verbosity normal` passes (existing + new tests)
- [ ] `dotnet pack` produces `GeoMagSharp.1.7.1.nupkg`
- [ ] Manual verification: `ModelDiscovery.DiscoverModels(coefficientFolder)` returns expected DisplayName for IGRF12, IGRF13, IGRF14

## Workflow

This is a focused single-file bug fix. Skipping the full Ralph rotation; one IMPLEMENTER pass with TDD (write failing test first) then a single REVIEWER pass at the end.

## Notes

- This is a strict additive change to public behavior: the `ModelDescriptor` API surface is unchanged; only the *values* returned for IGRF/DGRF files are corrected.
- Consumers who depend on the previous (broken) behavior of returning "IGRF00" for IGRF14.COF will need to update their expectations — but no such consumer is known.
- The library does not expose `SupportedExtensions` as data (a future enhancement that would let GUI clients build file-picker filters from library metadata instead of hardcoding extensions).
