# Feature: WMM Location-Dependent Uncertainty (Level 2)
Issue: #13
Branch: feature/13-wmm-error-model

## Tasks
- [x] Add `UncertaintyModelPreference` enum (Auto, Iscwsa, Native)
- [x] Add `UncertaintyModel` property to `CalculationOptions`
- [x] Create `wmm-error-model.json` embedded resource with WMM2025 and WMMHR2025 constants
- [x] Add JSON POCO classes for WMM error model deserialization
- [x] Add new properties to `GeomagneticUncertainty` (X, Y, Z, H + UncertaintySource)
- [x] Extend `UncertaintyDataProvider` to resolve WMM error model when applicable
- [x] Compute δD from H at each location using δD = √(base² + (coeff/H)²)
- [x] Integrate WMM uncertainty into GeoMag calculation pipeline
- [x] Add unit tests for WMM error model constants
- [x] Add unit tests for δD computation at multiple H values
- [x] Add integration test verifying against NOAA calculator output (41.31°N, 81.33°W)
- [x] Verify backward compatibility: existing ISCWSA behavior preserved with Iscwsa override
- [x] Verify build succeeds with 0 errors

## Completion Criteria
- [x] All tasks checked
- [x] Build succeeds
- [x] Tests pass
- [ ] 2 clean Ralph Loop cycles
