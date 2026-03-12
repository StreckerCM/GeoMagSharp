# IGRF Cross-Validation Tests

Issue: #12
Date: 2026-03-11

## Goal

Cross-validate GeoMagSharp's IGRF implementation against NOAA's authoritative IGRF-14 online calculator, verifying accuracy across multiple epochs (2000–2025), altitudes, and geographic locations. Phase 2 uses the existing IGRF-14 coefficient file to extend test coverage through 2025.

## Background

IAGA does not publish official IGRF test values (unlike NOAA's WMM test suite). Reference values are generated from the NOAA Magnetic Field Calculator (https://www.ngdc.noaa.gov/geomag/calculators/magcalc.shtml), which uses DGRF (definitive) coefficients for past epochs and IGRF-14 for 2025+. NOAA and BGS calculators were verified to produce matching results when given identical inputs.

IGRF definitive coefficients do not change between model generations — IGRF-12's definitive epochs (2000, 2005, 2010) use the same coefficients as IGRF-14. This allows cross-validation of GeoMagSharp's existing IGRF12.COF against IGRF-14 reference values for those epochs.

**Available coefficient files:** The repository already contains `IGRF12.COF`, `IGRF13.COF`, and `IGRF14.COF` in `coefficient/`. IGRF-13 is not used in this test suite to keep the matrix focused on IGRF-12 (oldest bundled model) vs IGRF-14 (latest generation), which maximizes the cross-validation value.

## Decision: Separate File

New file `IgrfCrossValidationTest.cs` following the same pattern as `WMM2025ValidationTest.cs`. This keeps model-specific validation tests isolated and repeatable.

## Design

### Phase 1: Cross-Validate IGRF-12 Against IGRF-14 Reference Values

**Test points:** 3 locations × 2 altitudes × 4 definitive epochs = 24 main field test cases

| Location | Latitude | Longitude |
|----------|----------|-----------|
| High-latitude North | 80°N | 0°E |
| Equatorial | 0°N | 120°E |
| High-latitude South | 80°S | 240°E (-120°W) |

| Altitude | Value |
|----------|-------|
| Surface | 0 km |
| Above surface | 100 km |

| Epoch | IGRF-12 Status | IGRF-14 Status | Phase 1 Action |
|-------|----------------|----------------|----------------|
| 2000.0 | Definitive (DGRF2000) | Definitive (DGRF2000) | Test — tight tolerance |
| 2005.0 | Definitive (DGRF2005) | Definitive (DGRF2005) | Test — tight tolerance |
| 2010.0 | Definitive (DGRF2010) | Definitive (DGRF2010) | Test — tight tolerance |
| 2015.0 | Non-definitive (IGRF2015) | Definitive (DGRF2015) | Test — loose tolerance |
| 2020.0 | SV extrapolation from 2015 | Definitive (DGRF2020) | Skip — IGRF-12 has no 2020 model, only SV extrapolation from IGRF2015 |
| 2025.0 | Not available | Non-definitive (IGRF2025) | Skip — Phase 2 only |

**Note on epoch coverage:**
- Epochs 2000.0, 2005.0, 2010.0: Definitive in both IGRF-12 and IGRF-14 — identical coefficients, should match exactly
- Epoch 2015.0: Non-definitive in IGRF-12, definitive in IGRF-14 — coefficients differ, needs loose tolerance
- Epoch 2020.0: IGRF12.COF's last model header is `IGRF2015` covering 2015–2020 via SV. At exactly 2020.0 (the boundary), values come from SV extrapolation only — unreliable for cross-validation, skipped
- Epoch 2025.0: Outside IGRF-12 range entirely — Phase 2 only

Phase 1 tests 4 epochs (2000.0–2015.0) × 3 locations × 2 altitudes = **24 main field test cases**.

SV tests are deferred until accurate epoch-specific SV reference data is obtained (see SV Data Note below).

### Phase 2: Test with IGRF-14 Coefficient File

- Copy existing `coefficient/IGRF14.COF` to `tests/GeoMagSharp.Tests/TestData/`
- Test all 6 epochs (2000.0–2025.0) × 3 locations × 2 altitudes = **36 main field test cases**
- All epochs use IGRF-14 coefficients matching the reference calculator — tight tolerances for all

### Test Methods

**Phase 1:**

1. `MainField_Igrf12_MatchesReferenceValues` — 24 `[DataRow]` entries
   - Asserts: D, I, H, X, Y, Z, F
   - 18 rows at tight tolerance (definitive epochs), 6 rows at loose tolerance (2015.0)

**Phase 2:**

2. `MainField_Igrf14_MatchesReferenceValues` — 36 `[DataRow]` entries
   - Asserts: D, I, H, X, Y, Z, F
   - All at tight tolerance

**Deferred (pending SV data):**

3. `SecularVariation_Igrf12_MatchesReferenceValues`
4. `SecularVariation_Igrf14_MatchesReferenceValues`

### DataRow Format

Same as WMM2025 tests — all primitives:
```
[DataRow(decimalDate, heightKm, lat, lon, X, Y, Z, H, F, I, D, DisplayName = "...")]
```

### Tolerances

| Component | Definitive Epochs | Non-Definitive Epochs (2015 in IGRF-12) |
|-----------|-------------------|----------------------------------------|
| Intensity (X, Y, Z, H, F) | 1.0 nT | 50.0 nT |
| Angles (D, I) | 0.01° | 0.5° |

**Rationale:**
- Definitive epochs use identical coefficients → differences are purely numerical precision
- Non-definitive epoch 2015.0 in IGRF-12 has different coefficients from IGRF-14's definitive DGRF2015 → larger tolerances needed
- If definitive-epoch tests fail at tight tolerances, investigate before loosening

### Test Data Source

Reference values generated from NOAA IGRF-14 online calculator:
- https://www.ngdc.noaa.gov/geomag/calculators/magcalc.shtml
- JSON exports stored in `tests/GeoMagSharp.Tests/TestData/IGRF_JSON/`
- CSV reference files in `tests/GeoMagSharp.Tests/TestData/`:
  - `IGRF_CrossValidation_MainField.csv` (36 rows, all epochs)
  - `IGRF_CrossValidation_SecularVariation.csv` (placeholder — see SV Data Note)

**SV Data Note:** NOAA's multi-epoch JSON export returns constant SV across all epochs (calculator limitation). The SV CSV currently contains these constant values, which are not suitable for cross-validation. Accurate epoch-specific SV values require individual single-date queries from the NOAA calculator. SV test methods will be added once this data is available.

### ClassInitialize

Loads IGRF12.COF (Phase 1) and IGRF14.COF (Phase 2) via `ModelReader.Read()`, using the same path-search pattern as `WMM2025ValidationTest.cs`. IGRF14.COF is copied from the existing `coefficient/IGRF14.COF`.

## What Changes

### Phase 1
- **New:** `tests/GeoMagSharp.Tests/IgrfCrossValidationTest.cs`
- **New:** `docs/features/12-igrf-cross-validation/tasks.md` (Ralph Loop tracking)
- **Existing:** `tests/GeoMagSharp.Tests/TestData/IGRF_JSON/` (reference JSON data, already created)
- **Existing:** `tests/GeoMagSharp.Tests/TestData/IGRF_CrossValidation_*.csv` (reference CSVs, already created)
- **Unchanged:** All source files, existing tests

### Phase 2
- **Copy:** `coefficient/IGRF14.COF` → `tests/GeoMagSharp.Tests/TestData/IGRF14.COF`
- **Modified:** `IgrfCrossValidationTest.cs` (add IGRF-14 test methods)

## Acceptance Criteria

### Phase 1
- All 18 definitive-epoch main field tests pass within tight tolerances (1.0 nT / 0.01°)
- All 6 non-definitive epoch (2015.0) tests pass within loose tolerances (50 nT / 0.5°)
- Tests use IGRF12.COF from TestData directory
- Test method names follow `Method_Scenario_Expected` convention
- NOAA source URL documented in test file comments
- Build succeeds, all existing tests still pass

### Phase 2
- All 36 main field tests pass within tight tolerances using IGRF14.COF
- IGRF14.COF parses correctly via ModelReader
- No changes to existing source code required

### Deferred
- SV tests added once accurate single-date reference data is obtained
