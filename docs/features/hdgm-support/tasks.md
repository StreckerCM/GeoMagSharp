# Feature: HDGM Support

Issue: #19
Branch: feature/19-hdgm-support
Design: docs/superpowers/specs/2026-04-26-hdgm-support-design.md

## Tasks

Tasks below are derived from the approved design. The detailed implementation
plan (sequencing, dependencies, test ordering) will be produced via the
writing-plans skill in a separate step before any code is written.

### Native binding layer
- [ ] Define `INativeHdgmInvoker` interface (public)
- [ ] Implement `Win32NativeMethods` (LoadLibraryEx / GetProcAddress / FreeLibrary P/Invokes)
- [ ] Implement `LoadLibraryHdgmInvoker` (production implementation, IDisposable)
- [ ] Define `HdgmCalcDelegate` with correct calling convention and signature
- [ ] Cover bitness-mismatch and missing-symbol error cases with descriptive messages

### Loader layer
- [ ] Implement `ModelPathDetector.IsHdgmPath(string)`
- [ ] Add `[InternalsVisibleTo]` for the GeoMagSharp.GUI assembly
- [ ] Implement `HDGMModelLoader.Load(string)` with platform check
- [ ] Wire `GeoMag.LoadModel(path)` to route HDGM paths to the new loader

### Adapter / calculation layer
- [ ] Implement `HDGMCalculationAdapter.Calculate(opts, date, modelSet)`
- [ ] Map `outData[0..6]` to `MagneticCalculations` field values
- [ ] Map `outData[8..14]` to `MagneticCalculations` secular variations (skip GV at indices 7 and 15)
- [ ] Map `outData[16]` to `Uncertainty.HighResolutionCoverage` (per DLL semantics — see `HDGM_Sublibrary.c:212`)
- [ ] Map `outData[17..23]` to `Uncertainty.SigmaD/I/H/X/Y/Z/F`
- [ ] Implement `-99999` sentinel detection → `GeoMagExceptionOutOfRange`
- [ ] Wire `GeoMag.MagneticCalculations` and `MagneticCalculationsAsync` to branch on `_Models.NativeInvoker != null`
- [ ] Add `lock` around native invoker call for thread safety
- [ ] Honor `CancellationToken` between iterations in async path

### Result-shape extensions
- [ ] Add `SigmaD/I/H/X/Y/Z/F` and `HighResolutionCoverage` to `Uncertainty` (nullable)
- [ ] Add `knownModels.HDGM = 6` to enum
- [ ] Add HDGM case to `UncertaintyDataProvider` (HRGM-tier ISCWSA values)

### Lifetime / disposability
- [ ] Implement `IDisposable` on `MagneticModelSet` (no-op for non-HDGM)
- [ ] Implement `IDisposable` on `GeoMag` (propagates to `_Models`)
- [ ] Add `[JsonIgnore]` on `MagneticModelSet.NativeInvoker`

### Cleanup
- [ ] `[Obsolete]` mark `Constants.MaxDeg` and `Constants.MaxCoeff` with v2.0 removal notice

### Tests — unit (CI)
- [ ] `ModelPathDetectorTests` — filename rule (~12 cases)
- [ ] `FakeHdgmInvoker` test double
- [ ] `HDGMCalculationAdapterTests` — index mapping, sentinel handling, unit conversions (~22 cases)
- [ ] `HDGMModelLoaderTests` — filename detection, missing-file errors, platform-not-supported behavior

### Tests — integration (env-var-gated)
- [ ] `HDGMIntegrationTests` skeleton with `Assert.Inconclusive` skip if env vars missing
- [ ] LoadsRealDll, SinglePoint, SamplePoints (validate against `HDGM2019_TestValues.txt`)
- [ ] DateSweep, OutOfRangeDate, DisposeFreesDll
- [ ] SigmaValuesPopulated, NSDCoverageFlag

### Documentation
- [ ] Update `README.md` — add HDGM to supported models with Windows-only callout and example
- [ ] Update `CLAUDE.md` Project Overview models list
- [ ] Create `docs/features/hdgm-support/README.md` — license posture, env vars, setup
- [ ] Add `.gitignore` defensive entries for HDGM-derived artifacts

### Build / project file
- [ ] Add `[InternalsVisibleTo]` directive (csproj or AssemblyInfo)
- [ ] Verify multi-target build still passes (net48 + netstandard2.0)

## Completion Criteria

- [ ] All tasks above checked
- [ ] Build succeeds (`dotnet build -c Release`) for both target frameworks
- [ ] All unit tests pass (`dotnet test --filter "TestCategory!=RequiresHDGMDll"`)
- [ ] Integration tests pass locally with `HDGM_DLL_PATH` and `HDGM_TEST_VALUES_PATH` env vars set (manual verification by maintainer)
- [ ] Existing model calculations (WMM, WMMHR, IGRF, EMM, DGRF) produce byte-identical results before/after this branch
- [ ] 2 clean Ralph Loop cycles (all 6 personas find no issues twice)
