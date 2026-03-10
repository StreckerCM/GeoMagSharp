# Issue #2: Geomagnetic Uncertainty Estimation — Design Spec

## Goal

Add ISCWSA-based geomagnetic uncertainty reporting to GeoMagSharp so that every magnetic field calculation includes 1-sigma uncertainty values for declination, dip, total field, and the Bh-dependent declination term.

## Background

The ISCWSA Error Model (Rev5.13, Jan 2023) defines 32 geomagnetic error terms across 5 model categories. For Level 1, the relevant roll-up values come from CDR-SM-03 Rev 8 (Copsegrove, 2013) Table 2, which provides composite 1-sigma uncertainties per model category.

Geomagnetic errors follow a **non-Gaussian (Laplacian) distribution** — the 95.4% confidence interval is more than 2× the 68.3% interval. The `ScaleTo()` method uses simple linear scaling as an approximation; users needing precise confidence intervals at non-1σ levels should consult SPE-119851 or BGS lookup tables.

### ISCWSA Model Categories

| Category | ISCWSA Name | SH Degree | Models |
|----------|-------------|-----------|--------|
| LowResolution | LRGM | ≤13 | IGRF, WMM, DGRF |
| StandardResolution | SRGM | ≤133 | BGGM pre-2019 |
| HighResolution | HRGM | ≤720 | HDGM, BGGM 2019+, EMM, WMMHR |
| InFieldReference1 | IFR1 | N/A | IFR corrected |
| InFieldReference2 | IFR2 | N/A | IFR + multi-station |

> **Note:** WMMHR is not listed in ISCWSA Rev5.13 (which predates it) but is classified as HighResolution as the closest-fit category — its SH degree (729) exceeds the HRGM upper bound (720) by a trivial margin, and its purpose and resolution tier align with HRGM models.

### 1-Sigma Uncertainty Values (CDR-SM-03 Table 2)

| Category | DEC (deg) | DBH (deg·nT) | MFI (nT) | MDI (deg) |
|----------|-----------|--------------|----------|-----------|
| LowResolution | 0.36 | 5000 | 157 | 0.24 |
| StandardResolution | 0.36 | 5000 | 130 | 0.20 |
| HighResolution | 0.30 | 4118 | 107 | 0.16 |
| InFieldReference1 | 0.15 | 1500 | 50 | 0.10 |
| InFieldReference2 | 0.15 | 1500 | 50 | 0.10 |

> StandardResolution uses the BGGM baseline values from CDR-SM-03 Table 2. HighResolution values are derived via the 0.82 HDGM multiplier. LowResolution values via the 1.21 IGRF/WMM multiplier.

## Design

### Section 1: New Types

#### `GeomagneticModelCategory` enum

```csharp
// File: src/GeoMagSharp/Enums/GeoMagEnums.cs (add to existing file)

public enum GeomagneticModelCategory
{
    Unknown = 0,
    LowResolution = 1,      // IGRF, WMM, DGRF (ISCWSA LRGM, degree ≤13)
    StandardResolution = 2,  // BGGM pre-2019 (ISCWSA SRGM, degree ≤133)
    HighResolution = 3,      // HDGM, BGGM 2019+, EMM, WMMHR (ISCWSA HRGM, degree ≤720)
    InFieldReference1 = 4,   // IFR1
    InFieldReference2 = 5    // IFR2
}
```

#### `WMMHR` added to `knownModels` enum

```csharp
// File: src/GeoMagSharp/Enums/GeoMagEnums.cs (modify existing enum)

public enum knownModels
{
    NONE = 0,
    DGRF = 1,
    EMM = 2,
    IGRF = 3,
    WMM = 4,
    WMMHR = 5
}
```

#### `GeomagneticUncertainty` class

```csharp
// File: src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs (new file)

public class GeomagneticUncertainty
{
    public GeomagneticModelCategory ModelCategory { get; set; }
    public double Declination { get; set; }        // DEC: degrees, 1-sigma
    public double BhDependentDec { get; set; }     // DBH: deg·nT, 1-sigma (effective dec error = DBH / Bh)
    public double TotalField { get; set; }         // MFI: nT, 1-sigma
    public double DipAngle { get; set; }           // MDI: degrees, 1-sigma (same quantity as Inclination)
    public string Revision { get; set; }           // e.g., "Rev5.13"

    /// <summary>
    /// Returns a new instance with all uncertainty values multiplied by the given scale factor.
    /// Note: This is a linear approximation. Geomagnetic errors follow a Laplacian (non-Gaussian)
    /// distribution, so scaled values are approximate at levels other than 1-sigma.
    /// </summary>
    /// <param name="scaleFactor">Multiplicative scale factor (e.g., 2.0 for approximate 2-sigma).</param>
    public GeomagneticUncertainty ScaleTo(double scaleFactor)
    {
        return new GeomagneticUncertainty
        {
            ModelCategory = ModelCategory,
            Declination = Declination * scaleFactor,
            BhDependentDec = BhDependentDec * scaleFactor,
            TotalField = TotalField * scaleFactor,
            DipAngle = DipAngle * scaleFactor,
            Revision = Revision
        };
    }
}
```

#### Auto-Detection Mapping

| `knownModels` value | `GeomagneticModelCategory` |
|---------------------|---------------------------|
| `WMM` | LowResolution |
| `IGRF` | LowResolution |
| `DGRF` | LowResolution |
| `WMMHR` | HighResolution |
| `EMM` | HighResolution |
| `NONE` | Unknown (requires manual override) |

Commercial models (BGGM, HDGM) are not in the `knownModels` enum and will auto-detect as `Unknown`. Users must set `ModelCategoryOverride` to the appropriate category (StandardResolution for BGGM pre-2019, HighResolution for HDGM or BGGM 2019+). Similarly, StandardResolution has no auto-detection path — it is only reachable via manual override.

IFR1 and IFR2 are always set via manual override — there are no IFR coefficient files.

> **WMMHR detection:** The existing `CheckStringForModel()` method in `ExtensionMethods.cs` does substring matching on model names. Since "WMMHR" contains "WMM", the implementation **must check for "WMMHR" before "WMM"** to avoid false matches. The implementation plan should ensure the longer string is matched first.

### Section 2: Data Storage

ISCWSA uncertainty magnitudes are stored as an embedded JSON resource. This allows future updates when ISCWSA publishes new revisions without recompiling.

#### JSON Format

```json
{
  "revision": "Rev5.13",
  "date": "2023-01",
  "source": "CDR-SM-03 Rev 8 (Copsegrove, 2013) + ISCWSA Error Model Rev5.13",
  "categories": {
    "LowResolution": {
      "declination": 0.36,
      "bhDependentDec": 5000,
      "totalField": 157,
      "dipAngle": 0.24
    },
    "StandardResolution": {
      "declination": 0.36,
      "bhDependentDec": 5000,
      "totalField": 130,
      "dipAngle": 0.20
    },
    "HighResolution": {
      "declination": 0.30,
      "bhDependentDec": 4118,
      "totalField": 107,
      "dipAngle": 0.16
    },
    "InFieldReference1": {
      "declination": 0.15,
      "bhDependentDec": 1500,
      "totalField": 50,
      "dipAngle": 0.10
    },
    "InFieldReference2": {
      "declination": 0.15,
      "bhDependentDec": 1500,
      "totalField": 50,
      "dipAngle": 0.10
    }
  }
}
```

#### File Location

- **Embedded resource:** `src/GeoMagSharp/Data/iscwsa-uncertainty.json`
- **Build action:** EmbeddedResource in `.csproj`

#### JSON Deserialization Classes

```csharp
// File: src/GeoMagSharp/Models/Configuration/UncertaintyData.cs (new file)

/// <summary>
/// Root object for ISCWSA uncertainty JSON data.
/// </summary>
internal class UncertaintyData
{
    public string Revision { get; set; }
    public string Date { get; set; }
    public string Source { get; set; }
    public Dictionary<string, UncertaintyCategoryData> Categories { get; set; }
}

/// <summary>
/// Uncertainty values for a single model category.
/// </summary>
internal class UncertaintyCategoryData
{
    public double Declination { get; set; }
    public double BhDependentDec { get; set; }
    public double TotalField { get; set; }
    public double DipAngle { get; set; }
}
```

#### Loading Strategy

- Default: load from embedded resource on first access via `UncertaintyDataProvider` (static class, lazy singleton, thread-safe via `Lazy<T>`)
- Future: `LoadUncertaintyModel(string path)` method to load from external file, same pattern as coefficient file loading
- Deserialization via Newtonsoft.Json (already a dependency)
- Resource namespace: `GeoMagSharp.Data.iscwsa-uncertainty.json` (determined by folder path under project root)

### Section 3: API Integration

#### `CalculationOptions` — Manual Override

```csharp
// File: src/GeoMagSharp/Models/Configuration/CalculationOptions.cs (add property)

/// <summary>
/// Optional override for the geomagnetic model category used in uncertainty estimation.
/// When null, the category is auto-detected from the loaded model type.
/// Set this for commercial models (BGGM, HDGM) or IFR corrections.
/// </summary>
public GeomagneticModelCategory? ModelCategoryOverride { get; set; }
```

> **Copy constructors:** Both `CalculationOptions` and `MagneticCalculations` have copy constructors that explicitly copy each property. The implementation must update these to include `ModelCategoryOverride` and `Uncertainty` respectively.

#### `MagneticCalculations` — Uncertainty Property

```csharp
// File: src/GeoMagSharp/Models/Results/MagneticCalculations.cs (add property)

/// <summary>
/// ISCWSA-based 1-sigma geomagnetic uncertainty for this calculation.
/// Null if model category is Unknown and no override was provided.
/// </summary>
public GeomagneticUncertainty Uncertainty { get; set; }
```

#### `GeoMag` — Population Logic

After each calculation, `GeoMag` populates the `Uncertainty` property. The mapping logic lives in a static method `UncertaintyDataProvider.GetUncertainty(knownModels model, GeomagneticModelCategory? overrideCategory)` so it can be unit-tested independently.

**Pipeline insertion point:** In `GeoMag.cs`, after the `MagneticCalculations` object is constructed (both spot and range calculation paths), call the provider to attach uncertainty.

1. Determine category: use `ModelCategoryOverride` if set, otherwise auto-detect from `knownModels`
2. Look up uncertainty values from the loaded JSON data via `UncertaintyDataProvider`
3. Attach to the `MagneticCalculations` result
4. If category is `Unknown` and no override: leave `Uncertainty` as null

### Section 4: Testing Strategy

| Test | Description |
|------|-------------|
| Category values match CDR-SM-03 | Verify each category's 4 values against the table |
| Auto-detection per model | Each `knownModels` value maps to correct category |
| Manual override wins | Set override, verify it takes precedence over auto-detect |
| `ScaleTo()` scaling | 1σ × 2 = approximate 2σ values for all fields |
| Unknown model returns null | `NONE` with no override → `Uncertainty` is null |
| JSON deserialization | Load embedded resource, verify all 5 categories present with correct values |
| WMMHR classification | WMMHR auto-detects as HighResolution |
| WMMHR vs WMM detection | Model name "WMMHR2025" detected as WMMHR, not WMM |
| Copy constructor coverage | `CalculationOptions` and `MagneticCalculations` copy constructors include new properties |

## Future Enhancements (Out of Scope)

- **External file loading:** `LoadUncertaintyModel(string path)` to swap in updated ISCWSA data without recompile
- **Level 2:** Latitude-dependent uncertainty using SPE-119851 spline curves or BGS lookup tables
- **Level 3:** Full location + time dependent uncertainty (seasonal, diurnal, model vintage)
- **32 sub-term decomposition:** Expose individual ISCWSA error terms (DEC-U, DEC-CH, etc.) for advanced users
- **Non-Gaussian confidence intervals:** Proper Laplacian quantile scaling instead of linear approximation

## References

- ISCWSA Error Model Definition Rev5.13 (Jan 2023) — Section 6.2.3, Table p.39
- CDR-SM-03 Rev 8 (Copsegrove, April 2013) — Tables 1 & 2
- SPE-119851-MS (Macmillan, McKay, Grindrod, 2009) — Non-Gaussian distribution evidence
- BGS Quantifying Uncertainties (Beggan, ISCWSA-49, 2019) — Methodology
