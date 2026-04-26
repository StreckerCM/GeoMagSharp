# Feature: HDGM Support

Issue: #19
Branch: feature/19-hdgm-support
Design: docs/superpowers/specs/2026-04-26-hdgm-support-design.md
Plan: docs/superpowers/plans/2026-04-26-hdgm-support.md

## Status

**Implementation:** ✅ Complete (19 commits, all 17 plan tasks executed via subagent-driven development with per-task review)
**Build:** ✅ Clean for `net48` and `netstandard2.0`
**Unit tests:** ✅ 368 / 368 passing (4 inconclusive — HDGM-DLL-gated integration tests, expected)
**NuGet pack:** ✅ `GeoMagSharp.1.6.0.nupkg` produced; no HDGM artifacts in package
**Final code review (holistic):** ✅ READY FOR QA / MERGE — 0 Critical, 0 Important, 7 Minor deferred

## Tasks

### Native binding layer
- [x] Define `INativeHdgmInvoker` interface (public) — `32030e0`
- [x] Implement `Win32NativeMethods` P/Invokes — `32030e0`
- [x] Implement `LoadLibraryHdgmInvoker` (production, IDisposable) — `d17d77e` + `84fad95` (thread-safety fixes)
- [x] Define `HdgmCalcDelegate` with `[UnmanagedFunctionPointer(CallingConvention.StdCall)]` — `32030e0`
- [x] Cover bitness-mismatch and missing-symbol error cases — `d17d77e`

### Loader layer
- [x] Implement `ModelPathDetector.IsHdgmPath(string)` — `1ab5569`
- [x] Add `[InternalsVisibleTo]` for `GeoMagSharp.Tests` and `GeoMagSharp.GUI` — `1ab5569`
- [x] Implement `HDGMModelLoader.Load(string)` with platform check — `fa344b5`
- [x] Wire `GeoMag.LoadModel(path)` to route HDGM paths to the new loader — `a54ba97`

### Adapter / calculation layer
- [x] Implement `HDGMCalculationAdapter.Calculate(opts, date, modelSet)` — `b70665e`
- [x] Map `outData[0..6]` to `MagneticCalculations` field values — `b70665e`
- [x] Map `outData[8..14]` to secular variations (skip GV at 7 and 15) — `b70665e`
- [x] Map `outData[16]` to `Uncertainty.HighResolutionCoverage` (DLL semantics; cite `HDGM_Sublibrary.c:212`) — `b70665e`
- [x] Map `outData[17..23]` to `Uncertainty.SigmaD/I/H/X/Y/Z/F` — `b70665e`
- [x] Implement `-99999` sentinel detection → `GeoMagExceptionOutOfRange` — `b70665e`
- [x] Wire `GeoMag.MagneticCalculations` and `MagneticCalculationsAsync` to branch on `_Models.NativeInvoker != null` — `a54ba97`
- [x] Add `lock` around native invoker call for thread safety — `d17d77e` + `84fad95`
- [x] Honor `CancellationToken` between iterations in async path — `a54ba97`

### Result-shape extensions
- [x] Add `SigmaD/I/H/X/Y/Z/F` and `HighResolutionCoverage` to `GeomagneticUncertainty` (nullable) — `c97a6f5`
- [x] Add `knownModels.HDGM = 6` to enum — `c5aeed1`
- [x] Add HDGM case to `UncertaintyDataProvider` (HRGM-tier ISCWSA values) — `1205140`

### Lifetime / disposability
- [x] Implement `IDisposable` on `MagneticModelSet` (no-op for non-HDGM) — `17e30d0`
- [x] Implement `IDisposable` on `GeoMag` (propagates to `_Models`) — `a54ba97`
- [x] Add `[JsonIgnore]` on `MagneticModelSet.NativeInvoker` — `17e30d0`

### Cleanup
- [x] `[Obsolete]` mark `Constants.MaxDeg` and `Constants.MaxCoeff` with v2.0 removal notice — `e88ad21`

### Tests — unit (CI)
- [x] `ModelPathDetectorTests` (16 cases) — `1ab5569`
- [x] `FakeHdgmInvoker` test double — `864ff14`
- [x] `HDGMCalculationAdapterTests` (29 cases including index mapping, sentinel, conversions) — `b70665e`
- [x] `HDGMModelLoaderTests` (filename detection, missing-file, platform-not-supported) — `fa344b5`

### Tests — integration (env-var-gated)
- [x] `HDGMIntegrationTests` skeleton with `Assert.Inconclusive` skip — `6532530`
- [x] LoadsRealDll, SinglePoint, SamplePoints (validate against `HDGM2019_TestValues.txt`) — `6532530`
- [x] DateSweep, OutOfRangeDate, DisposeFreesDll — `6532530`
- [x] SigmaValuesPopulated, NSDCoverageFlag — `6532530`

### Documentation
- [x] Update `README.md` — HDGM in supported-models with Windows-only callout — `7102f35`
- [x] Update `CLAUDE.md` Project Overview models list — `7102f35`
- [x] Create `docs/features/hdgm-support/README.md` (license posture, env vars, setup) — `13762b8`
- [x] Add `.gitignore` defensive entries for HDGM-derived artifacts — `753167d`

### Build / project file
- [x] Add `[InternalsVisibleTo]` directive — `1ab5569`
- [x] Verify multi-target build still passes (net48 + netstandard2.0) — Task 17 verification

## Completion Criteria

- [x] All tasks above checked
- [x] Build succeeds (`dotnet build -c Release`) for both target frameworks
- [x] All unit tests pass (`dotnet test --filter "TestCategory!=RequiresHDGMDll"`) — 368/368 passing
- [ ] Integration tests pass locally with `HDGM_DLL_PATH` and `HDGM_TEST_VALUES_PATH` env vars set (manual maintainer verification, deferred to merge gate)
- [x] Existing model calculations (WMM, WMMHR, IGRF, EMM, DGRF) produce byte-identical results before/after this branch (zero changes to Calculator.cs / ModelReader.cs / MagneticModel.cs / ExtensionMethods.cs:CheckStringForModel; full unit suite passes)
- [ ] 2 clean Ralph Loop cycles (all 6 personas find no issues twice) — IN PROGRESS

## Ralph Loop progress

Each iteration's persona is `iteration MOD 6`:
- 0 → IMPLEMENTER · 1 → REVIEWER · 2 → TESTER · 3 → API_DESIGNER · 4 → SECURITY_AUDITOR · 5 → PROJECT_MANAGER

| Iteration | Persona | Status | Findings |
|---|---|---|---|
| 1 | REVIEWER | ❌ issues fixed | Found 2 Important: native return code discarded; finalizer took a lock. Both fixed in `e24ccff`. Cycle 1 not clean. |
| 2 | TESTER | ❌ issues fixed | Found ~10 coverage gaps + 1 weak assertion. All addressed in `5dd14f9` (10 new tests, 1 assertion tightened). Cycle 1 still not clean. |
| 3 | API_DESIGNER | ❌ issues fixed | Found 2 Important: INativeHdgmInvoker public but no injection path; copy ctor silently drops NativeInvoker. Both fixed in `add4e7f` (added LoadModel(invoker) overload + copy ctor docs). Cycle 1 still not clean. |
| 4 | SECURITY_AUDITOR | ❌ issues fixed | Found 1 Critical (DLL planting) + 2 Important (NaN/Inf inputs, info disclosure). All addressed in `fef639b`. Cycle 1 still not clean. |
| 5 | PROJECT_MANAGER | ✅ CLEAN | Spec traceability 100%, task list accurate, no scope creep, no Critical/Important issues. First clean iter of cycle 1. |
| 6 | IMPLEMENTER | ✅ CLEAN | Build green for both targets, 368/368 unit tests pass, zero TODOs in HDGM code, all 17 task SHAs verified. Cycle 1 closes; iters 5+6 clean, iters 1-4 had findings (all fixed). |
| 7 | REVIEWER | ✅ CLEAN | Cycle 1 fixes verified stable. No new Critical/Important. 4 minor suggestions noted (non-blocking). 1st clean iter of cycle 2. |
| 8 | TESTER | ❌ issues fixed | Boundary tests gap (lat=±90, lon=±180 acceptance). Fixed in `5c260e4` (+4 tests). Cycle 2 not clean. |
| 9 | API_DESIGNER | ✅ CLEAN | iter 3 fixes stable. No new Critical/Important. 2 Minor suggestions (LoadModel(string) doc + namespace hint) deferred. |
| 10 | SECURITY_AUDITOR | ✅ CLEAN | iter 4 fixes (DLL planting, NaN/Inf, info disclosure) all stable. Zero new attack surface from iter 5-8. No Critical/Important. |
| 11 | PROJECT_MANAGER | ✅ CLEAN | Tasks.md accurate, all 39 leaf items checked, no scope creep, byte-identical existing models. Iter 8 fix `5c260e4` confirmed stable. |
| 12 | IMPLEMENTER | pending | — |

Cycle 1 = iterations 1-6 · Cycle 2 = iterations 7-12. A "clean cycle" requires all 6 personas in that cycle to report zero issues requiring fixes (Minor suggestions OK to defer).
