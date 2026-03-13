# WMM2025 Precision Validation Tests

Issue: #4
Date: 2026-03-10

## Goal

Add parameterized precision validation tests that compare GeoMagSharp's WMM2025 output against NOAA's official test values, verifying numeric accuracy rather than just directional correctness.

## Decision: Separate File

New file `WMM2025ValidationTest.cs` alongside existing `CalculatorUnitTest.cs`. Existing tests remain untouched — they validate model-independent physics behavior. This pattern is repeatable: WMM2030 gets its own file when released.

## Design

### File: `tests/GeoMagSharp.Tests/WMM2025ValidationTest.cs`

**Two parameterized test methods:**

1. `MainField_MatchesNOAATestValues` — 12 `[DataRow]` entries covering:
   - Epochs: 2025.0 and 2027.5
   - Heights: 0 km and 100 km
   - Locations: 80N/0E, 0N/120E, 80S/240E
   - Asserts: X (NorthComp), Y (EastComp), Z (VerticalComp), H (HorizontalIntensity), F (TotalField), I (Inclination), D (Declination)

2. `SecularVariation_MatchesNOAATestValues` — 12 `[DataRow]` entries, same coverage
   - Asserts: ChangePerYear for all 7 components

**Helper method:** `LoadWMM2025Model()` reads `TestData/WMM2025.COF` via `ModelReader.Read()`.

### DataRow Format

`[DataRow]` accepts primitives only. Decimal dates (2025.0, 2027.5) passed as `double`, converted to `DateTime` inside the method using the existing `ToDateTime()` extension or manual calculation.

### Tolerances

From NOAA's note that single precision causes up to 0.1 nT differences:

| Component | Tolerance |
|-----------|-----------|
| Intensity (X, Y, Z, H, F) | 1.0 nT |
| Angles (D, I) | 0.01 deg |
| SV intensity (Xdot, Ydot, Zdot, Hdot, Fdot) | 1.0 nT/yr |
| SV angles (Ddot, Idot) | 0.01 deg/yr |

If tests fail, investigate root cause before loosening tolerances.

### Failure Messages

Each assertion includes a formatted message identifying the test case: `$"X at ({lat}, {lon}), h={height}km, date={date}: expected {expectedX}, got {result.NorthComp.Value}"`.

## Test Data Source

NOAA WMM2025 official test values (December 2024):
- https://www.ncei.noaa.gov/sites/default/files/2025-02/WMM2025testvalues.pdf
- https://www.ncei.noaa.gov/sites/default/files/2025-02/WMM2025_TEST_VALUES.txt

## What Changes

- **New:** `tests/GeoMagSharp.Tests/WMM2025ValidationTest.cs`
- **New:** `docs/features/4-wmm2025-validation/tasks.md` (Ralph Loop tracking)
- **Unchanged:** `CalculatorUnitTest.cs`, all source files

## Acceptance Criteria

- All 12 main field test cases pass within tolerance
- All 12 secular variation test cases pass within tolerance
- Tests use WMM2025.COF from TestData directory
- Test method names follow `Method_Scenario_Expected` convention
- NOAA source URL documented in test file comments
- Build succeeds, all existing tests still pass
