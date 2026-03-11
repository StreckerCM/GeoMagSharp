# Feature: Depth-Adjusted Magnetic Field Values (SPE-128217-MS)
Issue: #3
Branch: feature/3-depth-adjusted-field

## Tasks
- [x] Task 1: Create DepthCorrectionResult model class
- [x] Task 2: Create DepthCorrection static class with dipole math (Eq 1-8)
- [x] Task 3: Add tool-frame error and boundary unit tests
- [x] Task 4: Add MagneticCalculations convenience overload
- [x] Task 5: Add SurveyDepthMeters, WellboreAzimuthDeg, WellboreInclinationDeg to CalculationOptions
- [x] Task 6: Add DepthCorrection property to MagneticCalculations; Add DepthAzimuthUncertainty to GeomagneticUncertainty
- [x] Task 7: Integrate depth correction into GeoMag sync and async pipelines
- [x] Task 8: Add 7 integration tests with WMM2025 model
- [x] Task 9: Create tasks.md, push branch, create draft PR

## Completion Criteria
- [x] All tasks checked
- [x] Build succeeds
- [x] Tests pass (34 depth correction tests: 27 unit + 7 integration)
- [ ] 2 clean Ralph Loop cycles
