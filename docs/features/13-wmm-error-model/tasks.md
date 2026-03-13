# Feature: WMM Location-Dependent Uncertainty (Level 2)
Issue: #13
Branch: feature/13-wmm-error-model

## Tasks
- [ ] Add `UncertaintyModelPreference` enum (Auto, Iscwsa, Native)
- [ ] Add `UncertaintyModel` property to `CalculationOptions`
- [ ] Create `wmm-error-model.json` embedded resource with WMM2025 and WMMHR2025 constants
- [ ] Add JSON POCO classes for WMM error model deserialization
- [ ] Add new properties to `GeomagneticUncertainty` (X, Y, Z, H + UncertaintySource)
- [ ] Extend `UncertaintyDataProvider` to resolve WMM error model when applicable
- [ ] Compute δD from H at each location using δD = √(base² + (coeff/H)²)
- [ ] Integrate WMM uncertainty into GeoMag calculation pipeline
- [ ] Add unit tests for WMM error model constants
- [ ] Add unit tests for δD computation at multiple H values
- [ ] Add integration test verifying against NOAA calculator output (41.31°N, 81.33°W)
- [ ] Verify backward compatibility: existing ISCWSA behavior preserved with Iscwsa override
- [ ] Verify build succeeds with 0 errors

## Completion Criteria
- [ ] All tasks checked
- [ ] Build succeeds
- [ ] Tests pass
- [ ] 2 clean Ralph Loop cycles
