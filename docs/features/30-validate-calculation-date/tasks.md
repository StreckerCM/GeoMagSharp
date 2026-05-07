# Feature: Validate calculation date against loaded model's MinDate/MaxDate

Issue: #30
Branch: feature/30-validate-calculation-date
Version: folded into 1.7.2 (merged with #31 in same release)

## Problem (and surprise from investigation)

The issue claims `GeoMag.MagneticCalculations` doesn't validate calculation dates against the loaded model's range. **Investigation showed the validation already exists** at `GeoMag.cs:143–158` and `:344–358` — both call `_Models.IsDateInRange(...)` and throw `GeoMagExceptionOutOfRange`.

The actual root cause: `HDGMModelLoader.Load` (and `GeoMag.LoadModel(INativeHdgmInvoker, ...)`) hardcode `MaxDate = 9999.0` as a permissive fallback. The check fires correctly for WMM/IGRF/EMM/BGGM (which load realistic dates from .COF headers) but never trips for HDGM. So the user's smoke test of HDGM2019 calculating values through 2030 produced silent extrapolation.

## Scope

Two complementary changes:

### A. Tighten HDGM `MaxDate` via probe

`HDGMModelLoader.Load` now calls `HdgmDateProbe.Probe` (already used by discovery) before constructing the `MagneticModelSet`. The probe makes ~8 `hdgmcalc` calls with year-incremented dates and treats the first sentinel result as the upper bound. Result: HDGM2019 loads with realistic `MaxDate` (e.g. 2020.0 from probe vs old hardcoded 9999.0).

If probe fails (corrupt DLL, LoadLibrary error), fall back to the old wide-permissive bounds — runtime sentinel inside `HDGMCalculationAdapter` is still authoritative.

`GeoMag.LoadModel(INativeHdgmInvoker, ...)` (the test/extension overload) gets optional `minDate` / `maxDate` parameters with permissive defaults. Tests can specify tight ranges; existing callers unaffected.

### B. Opt-in `CalculationOptions.AllowExtrapolation`

New `bool AllowExtrapolation { get; set; }` on `CalculationOptions`, default `false`. When `true`, the four existing `IsDateInRange` checks in `MagneticCalculations` and `MagneticCalculationsAsync` are skipped — for research callers who explicitly want raw extrapolation.

Wrap each existing `if (!_Models.IsDateInRange(...))` block in `if (!inCalculationOptions.AllowExtrapolation && !_Models.IsDateInRange(...))`.

## Out of scope

- Per-DLL filename-keyed date table for HDGM (probe is authoritative; filename is the regex source we already have via `HdgmDateProbe.ExtractYearFromFilename`)
- GUI-side warning before invoking Calculate (separate GeoMagSharpGUI issue)

## Tasks

- [ ] Create this `tasks.md`
- [ ] Modify `HDGMModelLoader.Load`: probe → use result for `MinDate`/`MaxDate`, fall back on null
- [ ] Modify `GeoMag.LoadModel(INativeHdgmInvoker, ...)`: add optional `minDate` / `maxDate` parameters
- [ ] Add `CalculationOptions.AllowExtrapolation` (auto-property, default `false`, copied in copy ctor)
- [ ] Wrap the 4 `IsDateInRange` blocks in `GeoMag.cs` (lines ~143, 153, 344, 353) with `if (!opts.AllowExtrapolation)`
- [ ] Tests:
  - HDGM date past loaded `MaxDate` throws `GeoMagExceptionOutOfRange`
  - Same scenario with `AllowExtrapolation = true` does NOT throw
  - WMM regression: existing date-range check still trips when out of range
  - Boundary cases: exactly at `MinDate`, exactly at `MaxDate`
- [ ] Update CHANGELOG.md — add bullet to existing 1.7.2 "Bug Fixes" section (no version bump)

## Workflow

Single IMPLEMENTER pass. Same pattern as #31: small, well-scoped, no UX/security surface area changing.

## Completion Criteria

- [ ] All tasks above checked
- [ ] `dotnet test -c Release` passes (existing + new)
- [ ] Manual GUI verification: load HDGM2019, attempt calc for 2028 → `GeoMagExceptionOutOfRange`
