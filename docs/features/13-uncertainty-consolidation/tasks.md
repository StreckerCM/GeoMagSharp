# Feature: WMM native error model + uncertainty field consolidation

Issue: #13 (closes; supersedes PR #15)
Branch: feature/13-uncertainty-consolidation
Version: folded into 1.7.2

## Background

Three uncertainty sources had landed in the codebase under different parallel naming schemes, accumulated by separate work that never merged or restructured:

| Source | Where it landed | Field names |
|---|---|---|
| **ISCWSA Level 1** | 1.5.0 (#2) | `Declination`, `BhDependentDec`, `TotalField`, `DipAngle` |
| **HDGM per-point** | 1.6.0 (#19) | `Sigma{D,I,H,F,X,Y,Z}` (nullable, populated only by HDGM) |
| **WMM error model** | PR #15 (Mar 2026, stalled with conflicts) | `NorthIntensity`, `EastIntensity`, `VerticalIntensity`, `HorizontalIntensity` (nullable) |

The same physical quantity (σ for the X component) had three potential names: `SigmaX`, `NorthIntensity`, or nothing. The user's mental model was clean — one set of seven uncertainty quantities, three sources with varying coverage — but the code had drifted.

## Resolution

**Field names mirror `MagneticCalculations` exactly.** Reader sees `result.Uncertainty.Inclination` and knows it's the σ for `result.Inclination`. The `Source` property (`Iscwsa` / `WmmErrorModel` / `Hdgm`) records provenance.

| Quantity | Field name | Nullable? | Notes |
|---|---|---|---|
| Declination σ | `Declination` | no | All sources populate. WMM uses `δD = √(C₁² + (C₂/H)²)`; sentinel 999.0 at H=0. |
| Inclination σ | `Inclination` | no | All sources. |
| Total field σ | `TotalField` | no | All sources. |
| Horizontal intensity σ | `HorizontalIntensity` | yes | Null for ISCWSA. |
| North component σ | `NorthComp` | yes | Null for ISCWSA. |
| East component σ | `EastComp` | yes | Null for ISCWSA. |
| Vertical component σ | `VerticalComp` | yes | Null for ISCWSA. |
| ISCWSA Bh-dependent declination | `BhDependentDec` | no | 0 for WMM/HDGM (location-dependent declination already in `Declination`). |
| HDGM coverage flag | `HighResolutionCoverage` | yes | HDGM-only. |
| Depth-correction sigma | `DepthAzimuthUncertainty` | yes | Populated by depth correction step. |

`Source` enum: `Iscwsa` (default for non-WMM/HDGM), `WmmErrorModel`, `Hdgm`.

## Tasks

- [x] Port WMM error-model JSON + POCO loader (`Data/wmm-error-model.json`, `WmmErrorModelData.cs`) — values from WMM2025-2030 Tech Report Section 3.4.
- [x] Add `UncertaintySource` and `UncertaintyModelPreference` enums.
- [x] Restructure `GeomagneticUncertainty`: rename to value-side names, narrow setters to `internal`, add `[Obsolete]` `DipAngle` bridge to `Inclination`.
- [x] Rewrite `UncertaintyDataProvider`: ISCWSA path (3-field), WMM path (7-field with location-dependent δD), 4-arg `GetUncertainty(model, override, preference, H)` dispatch, kept legacy 2-arg overload returning ISCWSA.
- [x] `ComputeDeclinationUncertainty` formula with sentinel at H=0; throws on NaN/Inf/negative.
- [x] `HDGMCalculationAdapter`: write per-point sigmas to new field names; set `Source = Hdgm`.
- [x] `CalculationOptions.UncertaintyPreference` (default `Auto`); copy ctor wired.
- [x] `GeoMag.MagneticCalculations` (sync + async): compute per-result uncertainty so WMM's location-dependent δD reflects each date's H.
- [x] `GeoMag.ApplyDepthCorrection`: copy all 11 uncertainty fields when rebuilding (was dropping the new ones).
- [x] csproj: embed `wmm-error-model.json`.
- [x] Tests:
  - WMM formula (typical H, large H, H=0 sentinel, NaN/Inf/negative throws)
  - Dispatch table (Auto/Iscwsa/Native × WMM/WMMHR/IGRF/HDGM)
  - WMM2025 + WMMHR2025 end-to-end value tables
  - Cross-source comparison: same WMM model with `Iscwsa` vs `Auto` returns different shapes
  - DipAngle obsolete bridge round-trip
- [x] GUI consumer (`CalculationDetailPanel`): replace `unc.SigmaX ?? unc.…` patterns with direct field access; add `FormatSigmaSource` mapping enum to display string.

## Out of scope

- Consolidating with PR #15 commit history — PR #15 closed as superseded; the work was extracted (JSON data, formula, dispatch idea) and re-implemented atop current development.
- Per-component σ for ISCWSA Level 2 — separate enhancement.

## Migration for external consumers

| Old (1.6.x / pre-1.7.2) | New (1.7.2+) | Notes |
|---|---|---|
| `unc.DipAngle` | `unc.Inclination` | `DipAngle` keeps working with `[Obsolete]` warning. |
| `unc.SigmaD` | `unc.Declination` | Type changes from `double?` to `double`. Was nullable to signal "HDGM-only data"; now always populated. |
| `unc.SigmaI` | `unc.Inclination` | Same change. |
| `unc.SigmaF` | `unc.TotalField` | Same change. |
| `unc.SigmaH` | `unc.HorizontalIntensity` | Still nullable; null for ISCWSA. |
| `unc.SigmaX` | `unc.NorthComp` | Still nullable. |
| `unc.SigmaY` | `unc.EastComp` | Still nullable. |
| `unc.SigmaZ` | `unc.VerticalComp` | Still nullable. |
| `if (unc.SigmaD.HasValue) /* HDGM */` | `if (unc.Source == UncertaintySource.Hdgm)` | Cleaner provenance check. |
