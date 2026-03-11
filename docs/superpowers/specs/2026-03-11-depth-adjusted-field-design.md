# Depth-Adjusted Magnetic Field Values & Depth-Dependent Uncertainty

**Issue:** #3
**Date:** 2026-03-11
**Reference:** SPE-128217-MS (Ekseth & Weston, Gyrodata, 2010)

## Goal

Add depth-adjusted magnetic field values and depth-dependent uncertainty estimates to GeoMagSharp, enabling MWD/MSA applications to account for the systematic error introduced when using surface geomagnetic field values at downhole survey depths.

## Background

SPE-128217-MS demonstrates that geomagnetic field variation with depth is a significant unaccounted error source in IFR-corrected MWD surveys. Their Monte Carlo analysis (9000 runs) shows:

- Global depth variation adds **0.38° (1σ) azimuth error** from this source alone
- This is **more than double** the IFR error model prediction of 0.18°
- The IIFR-MSC error model underestimates real wellbore position uncertainty by 50% or more
- A horizontal east-west singularity exists in the `(1 - sin²A·sin²I)` denominator

The Earth's global magnetic field strengthens with depth following an R³/(R-D)³ relationship. The paper provides analytical equations (Eq 1-8) to quantify this effect.

## Scope

**In scope:**
- Phase 1: Dipole depth correction (SPE-128217 Eq 1-8)
- Phase 2: Depth-dependent uncertainty integrated into existing uncertainty class
- Both pipeline-integrated and standalone API
- East-west singularity detection and flagging
- Validation test comparing dipole correction against SH recalculation at reduced altitude (not a separate API — consumers can already call the existing pipeline with negative altitude for SH-based depth values)

**Out of scope (deferred):**
- Crustal anomaly amplification modeling (SPE-128217 Section "Crustal anomalies")
- Downward continuation algorithms
- Level 2/3 location-dependent uncertainty (requires BGS lookup tables)

## Architecture

### Approach: Post-Processing Layer

Compute the standard surface field first (existing pipeline unchanged), then apply depth corrections as a post-processing step. A new public static class `DepthCorrection` handles all the math.

**Rationale:** The paper's contribution is quantifying the *error introduced by using surface values at depth* — not computing an alternative SH field underground. The post-processing approach directly implements the paper's equations and keeps the existing pipeline untouched.

### Depth Coordinate Model

```
                Sea level
                ─────────────
                     |
                     | Surface elevation (existing: SetElevation)
                     |
Rig floor ──────────►┌─────┐
                     │     │
                     │  ↓  │  SurveyDepthMeters (new: always positive, downward)
                     │     │
Survey station ─────►└─────┘
```

- **Elevation/Altitude** → where the surface calculation happens (existing API, unchanged)
- **SurveyDepthMeters** → how far below that surface the survey tool is (new, always positive)

These are independent. To model locations above the surface, increase the altitude — no negative depth needed.

### Geomagnetic Latitude

Derived self-consistently from the computed field: `φ = atan(B_v / (2·B_h))` where `B_v` is the vertical component (Z, positive downward in GeoMagSharp's `VerticalComp`) and `B_h` is the horizontal intensity (`HorizontalIntensity`). Use `Math.Atan2(Bv, 2*Bh)` to handle all quadrants correctly. When `B_h ≈ 0` (magnetic pole), use `φ = 90°` directly.

### Equatorial Dipole Field Strength (B₀)

In Eq 1-8, `B` represents the **equatorial dipole field strength** (B₀), NOT the total measured field. It is derived from the surface field components:

```
B₀ = B_h / cos(φ)     (equivalently: B₀ = B_v / (2·sin(φ)))
```

This ensures self-consistency: `B₀·cos(φ) = B_h` and `2·B₀·sin(φ) = B_v`.

## Equations (SPE-128217)

### Field at depth (Eq 1-2)

```
B_h(D) = B₀·cos(φ) · R³/(R-D)³     (horizontal component)
B_v(D) = 2·B₀·sin(φ) · R³/(R-D)³   (vertical component)
F(D)   = √(B_h(D)² + B_v(D)²)      (total field at depth)
```

### Error from using surface values (Eq 3-4)

**Note:** These are first-order Taylor approximations of Eq 1-2, valid when D << R. At D = 4 km and R = 6371 km, D/R ≈ 0.0006, so the approximation error is negligible (< 0.001%).

```
ΔB_h = 3·B₀·cos(φ) · D/R
ΔB_v = 6·B₀·sin(φ) · D/R
```

### Tool-frame error components (Eq 5-7)

Requires wellbore **magnetic** azimuth (A, relative to magnetic north) and inclination (I). If the consumer has true azimuth, they must subtract the declination first.

```
ΔB_H =  3·B₀·(cos(φ)·cos(A)·cos(I) - 2·sin(φ)·sin(I)) · D/R   (high-side)
ΔB_R = -3·B₀·cos(φ)·sin(A) · D/R                                  (high-side-right)
ΔB_A =  3·B₀·(cos(φ)·cos(A)·sin(I) + 2·sin(φ)·cos(I)) · D/R    (along-hole)
```

### Azimuth error approximation (Eq 8)

```
ΔA ≈ (sin(2A)·sin²(I) + 2·tan(φ)·sin(A)·sin(2I)) · 1.5·D/R
     ─────────────────────────────────────────────────────────
                    (1 - sin²(A)·sin²(I))
```

Where `sin(2A)` and `sin(2I)` are double-angle functions: `sin(2A) = 2·sin(A)·cos(A)`.

The denominator `(1 - sin²(A)·sin²(I))` creates a singularity for east-west wells at high inclination.

### Worked Example

**Location:** B_h = 20000 nT, B_v = 40000 nT (typical mid-latitude)
**Depth:** D = 3000 m, **Wellbore:** A = 45°, I = 60°

1. Geomagnetic latitude: `φ = atan(40000 / (2·20000)) = atan(1.0) = 45.0°`
2. Equatorial field: `B₀ = 20000 / cos(45°) = 28284.3 nT`
3. Verify: `2·28284.3·sin(45°) = 40000 nT ✓`
4. Scaling factor: `R³/(R-D)³ = 6371000³/6368000³ = 1.001413`
5. B_h at depth: `28284.3·cos(45°)·1.001413 = 20028.3 nT` (ΔB_h = +28.3 nT)
6. B_v at depth: `2·28284.3·sin(45°)·1.001413 = 40056.5 nT` (ΔB_v = +56.5 nT)
7. Linear approx (Eq 3): `3·28284.3·cos(45°)·3000/6371000 = 28.2 nT ✓` (agrees to 0.1 nT)
8. Linear approx (Eq 4): `6·28284.3·sin(45°)·3000/6371000 = 56.5 nT ✓`
9. Singularity factor: `1 - sin²(45°)·sin²(60°) = 1 - 0.5·0.75 = 0.625` (safe)
10. Azimuth error (Eq 8): `(sin(90°)·0.75 + 2·tan(45°)·sin(45°)·sin(120°)) · 1.5·3000/6371000 / 0.625`
    = `(0.75 + 2·1.0·0.7071·0.8660) · 0.000707 / 0.625`
    = `(0.75 + 1.2247) · 0.000707 / 0.625`
    = `1.9747 · 0.001131 = 0.00223 rad = 0.128°`

## File Structure

### New Files

| File | Purpose |
|------|---------|
| `src/GeoMagSharp/DepthCorrection.cs` | Public static class with all depth math (Eq 1-8) |
| `src/GeoMagSharp/Models/Results/DepthCorrectionResult.cs` | Result class for depth corrections |
| `tests/GeoMagSharp.Tests/DepthCorrectionUnitTest.cs` | Unit and integration tests |

### Modified Files

| File | Change |
|------|--------|
| `src/GeoMagSharp/Models/Configuration/CalculationOptions.cs` | Add `SurveyDepthMeters`, `WellboreAzimuth`, `WellboreInclination` + update copy constructor |
| `src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs` | Add `DepthAzimuthUncertainty` field |
| `src/GeoMagSharp/Models/Results/MagneticCalculations.cs` | Add `DepthCorrection` property + update copy constructor |
| `src/GeoMagSharp/GeoMag.cs` | Integrate depth correction into sync + async pipeline |

## API Design

### Input — CalculationOptions Additions

```csharp
public double? SurveyDepthMeters { get; set; }      // TVD below surface, always positive
public double? WellboreAzimuth { get; set; }         // magnetic azimuth in degrees (0-360), optional
public double? WellboreInclination { get; set; }     // degrees (0-180), optional
```

**Azimuth convention:** `WellboreAzimuth` is **magnetic azimuth** (relative to magnetic north), consistent with MWD tool output and SPE-128217 equations. If the consumer has true (geographic) azimuth, they must subtract the declination before passing it here.

**Inclination convention:** Standard MWD convention: 0° = vertical, 90° = horizontal, >90° = past-horizontal. The full 0-180° range is supported.

**Copy constructor:** All three properties must be copied in `CalculationOptions(CalculationOptions other)`. Without this, the pipeline-integrated mode will silently drop depth parameters since `GeoMag.cs` copies options at line ~131.

When `SurveyDepthMeters` has a value, the pipeline auto-computes depth correction. Wellbore geometry is optional — when absent, Eq 5-8 (tool-frame and azimuth error) are skipped.

### Standalone API — DepthCorrection

```csharp
public static class DepthCorrection
{
    /// Calculate depth correction from surface field values.
    /// Uses dipole approximation (SPE-128217 Eq 1-8).
    public static DepthCorrectionResult Calculate(
        double horizontalIntensityNT,
        double verticalIntensityNT,
        double totalFieldNT,
        double depthMeters,
        double? wellboreAzimuthDeg = null,
        double? wellboreInclinationDeg = null,
        double earthRadiusKm = Constants.EarthsRadiusInKm);

    /// Convenience overload accepting MagneticCalculations directly.
    public static DepthCorrectionResult Calculate(
        MagneticCalculations surfaceField,
        double depthMeters,
        double? wellboreAzimuthDeg = null,
        double? wellboreInclinationDeg = null);
}
```

The `totalFieldNT` parameter is used to compute `TotalFieldAtDepth` via `F(D) = F · R³/(R-D)³`. In a pure dipole, B_h and B_v scale identically, so total field scales by the same factor. This is mathematically equivalent to `√(B_h(D)² + B_v(D)²)` but avoids floating-point error accumulation.

Three usage modes:
1. **Pipeline-integrated** — set `SurveyDepthMeters` in options, get corrections automatically
2. **Standalone with GeoMagSharp field** — compute surface field first, then call `DepthCorrection.Calculate()` with `MagneticCalculations`
3. **Standalone with external field** — provide B_h, B_v, F values from any source

### Output — DepthCorrectionResult

```csharp
public class DepthCorrectionResult
{
    // Dipole scaling factor: R³/(R-D)³
    public double DipoleScalingFactor { get; set; }

    // Corrected field components at depth (Eq 1-2)
    public double HorizontalIntensityAtDepth { get; set; }  // nT
    public double VerticalIntensityAtDepth { get; set; }     // nT
    public double TotalFieldAtDepth { get; set; }            // nT

    // Field errors from using surface values (Eq 3-4)
    public double HorizontalError { get; set; }   // ΔB_h in nT
    public double VerticalError { get; set; }      // ΔB_v in nT

    // Tool-frame error components (Eq 5-7) — null when no wellbore geometry
    public double? HighSideError { get; set; }      // ΔB_H in nT
    public double? HighSideRightError { get; set; } // ΔB_R in nT
    public double? AlongHoleError { get; set; }     // ΔB_A in nT

    // Azimuth error estimate (Eq 8) — null when no wellbore geometry
    public double? AzimuthErrorDeg { get; set; }    // ΔA in degrees

    // Singularity proximity: (1 - sin²A·sin²I), values near 0 = singularity
    public double? SingularityFactor { get; set; }
    public bool? NearSingularity { get; set; }      // true if factor < 0.1

    // Geomagnetic latitude derived from field: atan(Bv / 2Bh)
    public double GeomagneticLatitudeDeg { get; set; }

    // Metadata
    public double DepthMeters { get; set; }
    public string Reference { get; set; }  // "SPE-128217-MS"
}
```

### Uncertainty Extension

Added to existing `GeomagneticUncertainty` class:

```csharp
public double? DepthAzimuthUncertainty { get; set; }  // degrees, 1σ
```

- When wellbore geometry provided: computed from Eq 8
- When not provided: 0.38° global average from Monte Carlo (SPE-128217 p.8)
- Only populated when `SurveyDepthMeters` is specified

### Pipeline Integration

Added to `MagneticCalculations`:

```csharp
public DepthCorrectionResult DepthCorrection { get; set; }  // null when no depth specified
```

## Error Handling

| Condition | Behavior |
|-----------|----------|
| `depthMeters < 0` | `ArgumentOutOfRangeException` |
| `depthMeters = 0` | Allowed — returns identity scaling (factor = 1.0, zero errors) |
| `depthMeters > 10000` | Allowed (math valid, dipole approximation degrades gradually) |
| `wellboreInclination` outside 0-180° | `ArgumentOutOfRangeException` |
| `wellboreAzimuth` outside 0-360° | Normalize (mod 360) |
| `horizontalIntensityNT ≈ 0` | Use geomagnetic latitude = 90° (magnetic pole) |
| `earthRadiusKm ≤ 0` | `ArgumentOutOfRangeException` |
| Singularity factor < 0.1 | `NearSingularity = true`, Eq 8 still computed but flagged |
| `SurveyDepthMeters = null` | No depth correction, `DepthCorrection` property stays null |

## Testing Strategy

### Unit Tests (pure math, no model loading)

| Test | Validates |
|------|-----------|
| `DipoleScaling_ZeroDepth_ReturnsOne` | Edge case: no correction at surface |
| `DipoleScaling_1km_CorrectFactor` | R³/(R-D)³ ≈ 1.000471 at 1 km |
| `HorizontalError_Eq3_MatchesPaper` | ΔB_h = 3B·cos(φ)·D/R |
| `VerticalError_Eq4_MatchesPaper` | ΔB_v = 6B·sin(φ)·D/R |
| `ToolFrameErrors_Eq567_MatchesPaper` | High-side, right, along-hole components |
| `AzimuthError_Eq8_KnownCase` | Validate against paper's Figure 2/3 examples |
| `AzimuthError_NearSingularity_Flagged` | A≈90°, I≈85° → NearSingularity=true |
| `GeomagneticLatitude_DerivedFromField` | tan(φ) = Bv/(2·Bh) roundtrip |
| `NullWellboreGeometry_SkipsEq8` | Tool-frame and azimuth error are null |
| `TotalFieldAtDepth_DerivedFromComponents` | F(D) = √(B_h(D)² + B_v(D)²) |
| `NegativeDepth_ThrowsException` | Input validation |
| `ZeroDepth_ReturnsIdentity` | All errors are zero, scaling factor = 1.0 |
| `ZeroHorizontalIntensity_MagneticPole` | φ = 90°, no division by zero |
| `VerticalWell_ToolFrameErrors` | I = 0° degenerates correctly |
| `NorthAzimuth_ToolFrameErrors` | A = 0° boundary case |
| `ZeroEarthRadius_ThrowsException` | Input validation |
| `ConvenienceOverload_MatchesPrimitive` | MagneticCalculations overload extracts correct values |

### Integration Tests (with WMM2025 model)

| Test | Validates |
|------|-----------|
| `Pipeline_WithDepth_PopulatesCorrection` | End-to-end: set SurveyDepthMeters, check result |
| `Pipeline_WithoutDepth_NullCorrection` | Backward compatibility |
| `Pipeline_WithWellboreGeometry_HasAzimuthError` | Full Eq 8 path |
| `Standalone_MatchesPipeline` | Standalone API gives same results as pipeline |
| `DepthUncertainty_AddedToUncertainty` | DepthAzimuthUncertainty populated |
| `DateRange_AllStepsGetDepthCorrection` | Multi-step calculation |
| `SHRecalc_vs_Dipole_Agreement` | SH at reduced altitude agrees with dipole correction within ~1 nT |

## Limitations

1. **Crustal anomaly amplification not modeled.** The dipole correction handles global field variation only. Crustal anomalies (200-1000 nT typical) intensify non-linearly with depth and are location-specific. This is noted in SPE-128217 Section "Crustal anomalies" and deferred to a future issue.

2. **Dipole approximation.** The depth correction uses a dipole model, which captures ~90% of the global field. Higher-order terms (quadrupole, octupole) are negligible at typical drilling depths (0-4 km) relative to Earth's radius (6371 km).

3. **East-west singularity.** Eq 8 becomes unreliable near the horizontal east-west direction. The `NearSingularity` flag alerts consumers but cannot eliminate the mathematical limitation.
