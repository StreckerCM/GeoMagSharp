# Geomagnetic Uncertainty Estimation Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ISCWSA-based 1-sigma geomagnetic uncertainty to every magnetic field calculation result.

**Architecture:** New `GeomagneticUncertainty` class populated from embedded JSON data (ISCWSA Rev5.13 values). Auto-detects model category from `knownModels` enum with manual override via `CalculationOptions`. `UncertaintyDataProvider` static class owns the data loading and lookup logic.

**Tech Stack:** C# / .NET multi-target (net48 + netstandard2.0), MSTest, Newtonsoft.Json, embedded resources

**Spec:** `docs/superpowers/specs/2026-03-10-uncertainty-estimation-design.md`

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `src/GeoMagSharp/Enums/GeoMagEnums.cs` | Modify | Add `GeomagneticModelCategory` enum, add `WMMHR = 5` to `knownModels` |
| `src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs` | Create | Uncertainty result class with `ScaleTo()` |
| `src/GeoMagSharp/Models/Configuration/UncertaintyData.cs` | Create | Internal JSON deserialization POCOs |
| `src/GeoMagSharp/Data/iscwsa-uncertainty.json` | Create | ISCWSA uncertainty values (embedded resource) |
| `src/GeoMagSharp/UncertaintyDataProvider.cs` | Create | Static class: load JSON, map models → categories, look up values |
| `src/GeoMagSharp/GeoMagSharp.csproj` | Modify | Add EmbeddedResource for JSON file |
| `src/GeoMagSharp/Models/Configuration/CalculationOptions.cs` | Modify | Add `ModelCategoryOverride` property + update copy constructor |
| `src/GeoMagSharp/Models/Results/MagneticCalculations.cs` | Modify | Add `Uncertainty` property + update copy constructors |
| `src/GeoMagSharp/ExtensionMethods.cs` | Modify | Fix WMMHR/WMM detection order in `CheckStringForModel()` |
| `src/GeoMagSharp/GeoMag.cs` | Modify | Attach uncertainty after each calculation |
| `tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs` | Create | All uncertainty tests |

---

## Chunk 1: Foundation Types and Data

### Task 1: Add `GeomagneticModelCategory` enum and `WMMHR` to `knownModels`

**Files:**
- Modify: `src/GeoMagSharp/Enums/GeoMagEnums.cs`
- Test: `tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs`

- [ ] **Step 1: Create test file with enum existence tests**

Create `tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs`:

```csharp
/****************************************************************************
 * File:            UncertaintyUnitTest.cs
 * Description:     Tests for ISCWSA geomagnetic uncertainty estimation
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeoMagSharp_UnitTests
{
    [TestClass]
    public class UncertaintyUnitTest
    {
        [TestMethod]
        public void GeomagneticModelCategory_HasAllExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(0, (int)GeoMagSharp.GeomagneticModelCategory.Unknown);
            Assert.AreEqual(1, (int)GeoMagSharp.GeomagneticModelCategory.LowResolution);
            Assert.AreEqual(2, (int)GeoMagSharp.GeomagneticModelCategory.StandardResolution);
            Assert.AreEqual(3, (int)GeoMagSharp.GeomagneticModelCategory.HighResolution);
            Assert.AreEqual(4, (int)GeoMagSharp.GeomagneticModelCategory.InFieldReference1);
            Assert.AreEqual(5, (int)GeoMagSharp.GeomagneticModelCategory.InFieldReference2);
        }

        [TestMethod]
        public void KnownModels_IncludesWMMHR()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(5, (int)GeoMagSharp.knownModels.WMMHR);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~UncertaintyUnitTest" --verbosity normal`

Expected: FAIL — `GeomagneticModelCategory` does not exist, `knownModels.WMMHR` does not exist.

- [ ] **Step 3: Add `GeomagneticModelCategory` enum and `WMMHR` to `knownModels`**

In `src/GeoMagSharp/Enums/GeoMagEnums.cs`, after the closing brace of the `knownModels` enum (line 92), add:

```csharp
    /// <summary>
    /// ISCWSA geomagnetic reference model categories for uncertainty estimation.
    /// Categories are defined by spherical harmonic degree range per ISCWSA Rev5.13.
    /// </summary>
    public enum GeomagneticModelCategory
    {
        /// <summary>
        /// Unknown or unrecognized model — uncertainty cannot be auto-determined.
        /// Use ModelCategoryOverride in CalculationOptions to set manually.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Low Resolution Global Model (ISCWSA LRGM, degree ≤13): IGRF, WMM, DGRF
        /// </summary>
        LowResolution = 1,

        /// <summary>
        /// Standard Resolution Global Model (ISCWSA SRGM, degree ≤133): BGGM pre-2019
        /// </summary>
        StandardResolution = 2,

        /// <summary>
        /// High Resolution Global Model (ISCWSA HRGM, degree ≤720): HDGM, BGGM 2019+, EMM, WMMHR
        /// </summary>
        HighResolution = 3,

        /// <summary>
        /// In-Field Referencing level 1
        /// </summary>
        InFieldReference1 = 4,

        /// <summary>
        /// In-Field Referencing level 2 (with multi-station correction)
        /// </summary>
        InFieldReference2 = 5
    }
```

Also modify the `knownModels` enum — add before the closing brace (after `WMM = 4` on line 91):

```csharp
        /// <summary>
        /// World Magnetic Model High Resolution.
        /// Not in ISCWSA Rev5.13 (predates it); classified as HighResolution (HRGM)
        /// based on SH degree (729).
        /// </summary>
        WMMHR = 5
```

Change `WMM = 4` line to add a trailing comma: `WMM = 4,`

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~UncertaintyUnitTest" --verbosity normal`

Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Enums/GeoMagEnums.cs tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs
git commit -m "feat: add GeomagneticModelCategory enum and WMMHR to knownModels"
```

---

### Task 2: Create `GeomagneticUncertainty` class

**Files:**
- Create: `src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs`
- Modify: `tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs`

- [ ] **Step 1: Write tests for `GeomagneticUncertainty` and `ScaleTo()`**

Add to `UncertaintyUnitTest.cs`:

```csharp
        [TestMethod]
        public void GeomagneticUncertainty_ScaleTo_MultipliesAllValues()
        {
            // Arrange
            var uncertainty = new GeoMagSharp.GeomagneticUncertainty
            {
                ModelCategory = GeoMagSharp.GeomagneticModelCategory.LowResolution,
                Declination = 0.36,
                BhDependentDec = 5000,
                TotalField = 157,
                DipAngle = 0.24,
                Revision = "Rev5.13"
            };

            // Act
            var scaled = uncertainty.ScaleTo(2.0);

            // Assert
            Assert.AreEqual(0.72, scaled.Declination, 0.001);
            Assert.AreEqual(10000, scaled.BhDependentDec, 0.1);
            Assert.AreEqual(314, scaled.TotalField, 0.1);
            Assert.AreEqual(0.48, scaled.DipAngle, 0.001);
            Assert.AreEqual("Rev5.13", scaled.Revision);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.LowResolution, scaled.ModelCategory);
        }

        [TestMethod]
        public void GeomagneticUncertainty_ScaleTo_DoesNotMutateOriginal()
        {
            // Arrange
            var original = new GeoMagSharp.GeomagneticUncertainty
            {
                Declination = 0.36,
                BhDependentDec = 5000,
                TotalField = 157,
                DipAngle = 0.24,
                Revision = "Rev5.13"
            };

            // Act
            var _ = original.ScaleTo(3.0);

            // Assert — original unchanged
            Assert.AreEqual(0.36, original.Declination, 0.001);
            Assert.AreEqual(5000, original.BhDependentDec, 0.1);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~UncertaintyUnitTest" --verbosity normal`

Expected: FAIL — `GeomagneticUncertainty` does not exist.

- [ ] **Step 3: Implement `GeomagneticUncertainty`**

Create `src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs`:

```csharp
/****************************************************************************
 * File:            GeomagneticUncertainty.cs
 * Description:     ISCWSA-based geomagnetic uncertainty values
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

namespace GeoMagSharp
{
    /// <summary>
    /// ISCWSA-based 1-sigma geomagnetic uncertainty values for a model category.
    /// Values are from CDR-SM-03 Rev 8 (Copsegrove, 2013) Table 2.
    /// </summary>
    public class GeomagneticUncertainty
    {
        /// <summary>The ISCWSA model category these values apply to.</summary>
        public GeomagneticModelCategory ModelCategory { get; set; }

        /// <summary>Declination uncertainty in degrees, 1-sigma (ISCWSA DEC term).</summary>
        public double Declination { get; set; }

        /// <summary>
        /// Bh-dependent declination uncertainty in deg·nT, 1-sigma (ISCWSA DBH term).
        /// Effective declination error = BhDependentDec / Bh, where Bh is horizontal field intensity.
        /// </summary>
        public double BhDependentDec { get; set; }

        /// <summary>Total field intensity uncertainty in nT, 1-sigma (ISCWSA MFI term).</summary>
        public double TotalField { get; set; }

        /// <summary>
        /// Dip angle (inclination) uncertainty in degrees, 1-sigma (ISCWSA MDI term).
        /// Same physical quantity as MagneticCalculations.Inclination.
        /// </summary>
        public double DipAngle { get; set; }

        /// <summary>ISCWSA error model revision (e.g., "Rev5.13").</summary>
        public string Revision { get; set; }

        /// <summary>
        /// Returns a new instance with all uncertainty values multiplied by the given scale factor.
        /// Note: This is a linear approximation. Geomagnetic errors follow a Laplacian
        /// (non-Gaussian) distribution, so scaled values are approximate at levels other than 1-sigma.
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
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~UncertaintyUnitTest" --verbosity normal`

Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs
git commit -m "feat: add GeomagneticUncertainty class with ScaleTo()"
```

---

### Task 3: Create JSON data file and deserialization classes

**Files:**
- Create: `src/GeoMagSharp/Data/iscwsa-uncertainty.json`
- Create: `src/GeoMagSharp/Models/Configuration/UncertaintyData.cs`
- Modify: `src/GeoMagSharp/GeoMagSharp.csproj`

- [ ] **Step 1: Create the JSON data file**

First verify the Data directory doesn't exist yet:

```bash
ls src/GeoMagSharp/Data/ 2>/dev/null || mkdir -p src/GeoMagSharp/Data
```

Create `src/GeoMagSharp/Data/iscwsa-uncertainty.json`:

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

- [ ] **Step 2: Add EmbeddedResource to csproj**

In `src/GeoMagSharp/GeoMagSharp.csproj`, add a new `<ItemGroup>` before the closing `</Project>` tag:

```xml
  <ItemGroup>
    <EmbeddedResource Include="Data\iscwsa-uncertainty.json" />
  </ItemGroup>
```

- [ ] **Step 3: Create deserialization POCOs**

Create `src/GeoMagSharp/Models/Configuration/UncertaintyData.cs`:

```csharp
/****************************************************************************
 * File:            UncertaintyData.cs
 * Description:     Internal POCOs for ISCWSA uncertainty JSON deserialization
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System.Collections.Generic;

namespace GeoMagSharp
{
    /// <summary>
    /// Root object for ISCWSA uncertainty JSON data.
    /// Internal — consumers use <see cref="GeomagneticUncertainty"/> instead.
    /// </summary>
    internal class UncertaintyData
    {
        public string Revision { get; set; }
        public string Date { get; set; }
        public string Source { get; set; }
        public Dictionary<string, UncertaintyCategoryData> Categories { get; set; }
    }

    /// <summary>
    /// Uncertainty values for a single model category (JSON shape).
    /// </summary>
    internal class UncertaintyCategoryData
    {
        public double Declination { get; set; }
        public double BhDependentDec { get; set; }
        public double TotalField { get; set; }
        public double DipAngle { get; set; }
    }
}
```

- [ ] **Step 4: Build to verify embedded resource compiles**

Run: `dotnet build src/GeoMagSharp/GeoMagSharp.csproj -c Release`

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Data/iscwsa-uncertainty.json src/GeoMagSharp/Models/Configuration/UncertaintyData.cs src/GeoMagSharp/GeoMagSharp.csproj
git commit -m "feat: add ISCWSA uncertainty JSON data and deserialization classes"
```

---

### Task 4: Create `UncertaintyDataProvider`

**Files:**
- Create: `src/GeoMagSharp/UncertaintyDataProvider.cs`
- Modify: `tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs`

- [ ] **Step 1: Write tests for provider**

Add to `UncertaintyUnitTest.cs`. Add `using System;` to the top if not present.

```csharp
        #region UncertaintyDataProvider Tests

        [TestMethod]
        public void GetModelCategory_WMM_ReturnsLowResolution()
        {
            Assert.AreEqual(
                GeoMagSharp.GeomagneticModelCategory.LowResolution,
                GeoMagSharp.UncertaintyDataProvider.GetModelCategory(GeoMagSharp.knownModels.WMM, null));
        }

        [TestMethod]
        public void GetModelCategory_IGRF_ReturnsLowResolution()
        {
            Assert.AreEqual(
                GeoMagSharp.GeomagneticModelCategory.LowResolution,
                GeoMagSharp.UncertaintyDataProvider.GetModelCategory(GeoMagSharp.knownModels.IGRF, null));
        }

        [TestMethod]
        public void GetModelCategory_DGRF_ReturnsLowResolution()
        {
            Assert.AreEqual(
                GeoMagSharp.GeomagneticModelCategory.LowResolution,
                GeoMagSharp.UncertaintyDataProvider.GetModelCategory(GeoMagSharp.knownModels.DGRF, null));
        }

        [TestMethod]
        public void GetModelCategory_EMM_ReturnsHighResolution()
        {
            Assert.AreEqual(
                GeoMagSharp.GeomagneticModelCategory.HighResolution,
                GeoMagSharp.UncertaintyDataProvider.GetModelCategory(GeoMagSharp.knownModels.EMM, null));
        }

        [TestMethod]
        public void GetModelCategory_WMMHR_ReturnsHighResolution()
        {
            Assert.AreEqual(
                GeoMagSharp.GeomagneticModelCategory.HighResolution,
                GeoMagSharp.UncertaintyDataProvider.GetModelCategory(GeoMagSharp.knownModels.WMMHR, null));
        }

        [TestMethod]
        public void GetModelCategory_NONE_ReturnsUnknown()
        {
            Assert.AreEqual(
                GeoMagSharp.GeomagneticModelCategory.Unknown,
                GeoMagSharp.UncertaintyDataProvider.GetModelCategory(GeoMagSharp.knownModels.NONE, null));
        }

        [TestMethod]
        public void GetModelCategory_OverrideWins()
        {
            // WMM normally maps to LowResolution, but override should win
            Assert.AreEqual(
                GeoMagSharp.GeomagneticModelCategory.HighResolution,
                GeoMagSharp.UncertaintyDataProvider.GetModelCategory(
                    GeoMagSharp.knownModels.WMM,
                    GeoMagSharp.GeomagneticModelCategory.HighResolution));
        }

        [TestMethod]
        public void GetUncertainty_LowResolution_MatchesCDRSM03()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.WMM, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.LowResolution, result.ModelCategory);
            Assert.AreEqual(0.36, result.Declination, 0.001);
            Assert.AreEqual(5000, result.BhDependentDec, 0.1);
            Assert.AreEqual(157, result.TotalField, 0.1);
            Assert.AreEqual(0.24, result.DipAngle, 0.001);
            Assert.AreEqual("Rev5.13", result.Revision);
        }

        [TestMethod]
        public void GetUncertainty_HighResolution_MatchesCDRSM03()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.EMM, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.HighResolution, result.ModelCategory);
            Assert.AreEqual(0.30, result.Declination, 0.001);
            Assert.AreEqual(4118, result.BhDependentDec, 0.1);
            Assert.AreEqual(107, result.TotalField, 0.1);
            Assert.AreEqual(0.16, result.DipAngle, 0.001);
        }

        [TestMethod]
        public void GetUncertainty_StandardResolution_ViaOverride_MatchesCDRSM03()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.NONE,
                GeoMagSharp.GeomagneticModelCategory.StandardResolution);

            Assert.IsNotNull(result);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.StandardResolution, result.ModelCategory);
            Assert.AreEqual(0.36, result.Declination, 0.001);
            Assert.AreEqual(5000, result.BhDependentDec, 0.1);
            Assert.AreEqual(130, result.TotalField, 0.1);
            Assert.AreEqual(0.20, result.DipAngle, 0.001);
        }

        [TestMethod]
        public void GetUncertainty_IFR1_ViaOverride_MatchesCDRSM03()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.NONE,
                GeoMagSharp.GeomagneticModelCategory.InFieldReference1);

            Assert.IsNotNull(result);
            Assert.AreEqual(0.15, result.Declination, 0.001);
            Assert.AreEqual(1500, result.BhDependentDec, 0.1);
            Assert.AreEqual(50, result.TotalField, 0.1);
            Assert.AreEqual(0.10, result.DipAngle, 0.001);
        }

        [TestMethod]
        public void GetUncertainty_IFR2_ViaOverride_MatchesCDRSM03()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.NONE,
                GeoMagSharp.GeomagneticModelCategory.InFieldReference2);

            Assert.IsNotNull(result);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.InFieldReference2, result.ModelCategory);
            Assert.AreEqual(0.15, result.Declination, 0.001);
            Assert.AreEqual(1500, result.BhDependentDec, 0.1);
            Assert.AreEqual(50, result.TotalField, 0.1);
            Assert.AreEqual(0.10, result.DipAngle, 0.001);
        }

        [TestMethod]
        public void GetUncertainty_UnknownNoOverride_ReturnsNull()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.NONE, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetUncertainty_AllFiveCategories_HaveData()
        {
            // Verify all 5 categories are loadable via override
            var categories = new[]
            {
                GeoMagSharp.GeomagneticModelCategory.LowResolution,
                GeoMagSharp.GeomagneticModelCategory.StandardResolution,
                GeoMagSharp.GeomagneticModelCategory.HighResolution,
                GeoMagSharp.GeomagneticModelCategory.InFieldReference1,
                GeoMagSharp.GeomagneticModelCategory.InFieldReference2
            };

            foreach (var cat in categories)
            {
                var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                    GeoMagSharp.knownModels.NONE, cat);
                Assert.IsNotNull(result, $"Category {cat} returned null");
                Assert.IsTrue(result.Declination > 0, $"Category {cat} has zero Declination");
            }
        }

        #endregion
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~UncertaintyUnitTest" --verbosity normal`

Expected: FAIL — `UncertaintyDataProvider` does not exist.

- [ ] **Step 3: Implement `UncertaintyDataProvider`**

Create `src/GeoMagSharp/UncertaintyDataProvider.cs`:

```csharp
/****************************************************************************
 * File:            UncertaintyDataProvider.cs
 * Description:     Loads and provides ISCWSA uncertainty data
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace GeoMagSharp
{
    /// <summary>
    /// Provides ISCWSA-based geomagnetic uncertainty values.
    /// Loads data from embedded JSON resource on first access (thread-safe).
    /// </summary>
    public static class UncertaintyDataProvider
    {
        private static readonly Lazy<UncertaintyData> _data = new Lazy<UncertaintyData>(LoadEmbeddedData);

        /// <summary>
        /// Maps a <see cref="knownModels"/> value to its ISCWSA model category.
        /// </summary>
        /// <param name="model">The detected model type.</param>
        /// <param name="overrideCategory">Optional manual override. If set, returned directly.</param>
        /// <returns>The model category for uncertainty lookup.</returns>
        public static GeomagneticModelCategory GetModelCategory(knownModels model, GeomagneticModelCategory? overrideCategory)
        {
            if (overrideCategory.HasValue)
                return overrideCategory.Value;

            switch (model)
            {
                case knownModels.WMM:
                case knownModels.IGRF:
                case knownModels.DGRF:
                    return GeomagneticModelCategory.LowResolution;

                case knownModels.WMMHR:
                case knownModels.EMM:
                    return GeomagneticModelCategory.HighResolution;

                default:
                    return GeomagneticModelCategory.Unknown;
            }
        }

        /// <summary>
        /// Gets the ISCWSA uncertainty values for the given model and optional category override.
        /// </summary>
        /// <param name="model">The detected model type.</param>
        /// <param name="overrideCategory">Optional manual override for model category.</param>
        /// <returns>Uncertainty values, or null if category is Unknown.</returns>
        public static GeomagneticUncertainty GetUncertainty(knownModels model, GeomagneticModelCategory? overrideCategory)
        {
            var category = GetModelCategory(model, overrideCategory);

            if (category == GeomagneticModelCategory.Unknown)
                return null;

            var data = _data.Value;
            var categoryName = category.ToString();

            if (!data.Categories.ContainsKey(categoryName))
                return null;

            var values = data.Categories[categoryName];

            return new GeomagneticUncertainty
            {
                ModelCategory = category,
                Declination = values.Declination,
                BhDependentDec = values.BhDependentDec,
                TotalField = values.TotalField,
                DipAngle = values.DipAngle,
                Revision = data.Revision
            };
        }

        private static UncertaintyData LoadEmbeddedData()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "GeoMagSharp.Data.iscwsa-uncertainty.json";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        $"Embedded resource '{resourceName}' not found. Ensure iscwsa-uncertainty.json is set as EmbeddedResource in the project file.");

                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<UncertaintyData>(json);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~UncertaintyUnitTest" --verbosity normal`

Expected: PASS (all 19 tests)

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/UncertaintyDataProvider.cs tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs
git commit -m "feat: add UncertaintyDataProvider with auto-detection and JSON loading"
```

---

## Chunk 2: Integration and WMMHR Detection Fix

### Task 5: Add `ModelCategoryOverride` to `CalculationOptions`

**Files:**
- Modify: `src/GeoMagSharp/Models/Configuration/CalculationOptions.cs`
- Modify: `tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs`

- [ ] **Step 1: Write test for copy constructor**

Add to `UncertaintyUnitTest.cs`:

```csharp
        #region CalculationOptions Tests

        [TestMethod]
        public void CalculationOptions_CopyConstructor_CopiesModelCategoryOverride()
        {
            // Arrange
            var original = new GeoMagSharp.CalculationOptions
            {
                ModelCategoryOverride = GeoMagSharp.GeomagneticModelCategory.InFieldReference1
            };

            // Act
            var copy = new GeoMagSharp.CalculationOptions(original);

            // Assert
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.InFieldReference1, copy.ModelCategoryOverride);
        }

        [TestMethod]
        public void CalculationOptions_ModelCategoryOverride_DefaultsToNull()
        {
            var options = new GeoMagSharp.CalculationOptions();
            Assert.IsNull(options.ModelCategoryOverride);
        }

        #endregion
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~UncertaintyUnitTest.CalculationOptions" --verbosity normal`

Expected: FAIL — `ModelCategoryOverride` does not exist.

- [ ] **Step 3: Add property and update copy constructor**

In `src/GeoMagSharp/Models/Configuration/CalculationOptions.cs`:

After `CalculationMethod = Algorithm.BGS;` (line 31), add:
```csharp
            ModelCategoryOverride = null;
```

In the copy constructor, after `CalculationMethod = other.CalculationMethod;` (line 50), add:
```csharp
            ModelCategoryOverride = other.ModelCategoryOverride;
```

In the properties section, after line 78 (`public Algorithm CalculationMethod { get; set; }`), add:

```csharp

        /// <summary>
        /// Optional override for the geomagnetic model category used in uncertainty estimation.
        /// When null, the category is auto-detected from the loaded model type.
        /// Set this for commercial models (BGGM, HDGM) or IFR corrections.
        /// </summary>
        public GeomagneticModelCategory? ModelCategoryOverride { get; set; }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~UncertaintyUnitTest" --verbosity normal`

Expected: PASS (all 21 tests)

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Models/Configuration/CalculationOptions.cs tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs
git commit -m "feat: add ModelCategoryOverride to CalculationOptions"
```

---

### Task 6: Add `Uncertainty` to `MagneticCalculations`

**Files:**
- Modify: `src/GeoMagSharp/Models/Results/MagneticCalculations.cs`
- Modify: `tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs`

- [ ] **Step 1: Write test for copy constructor**

Add to `UncertaintyUnitTest.cs`:

```csharp
        #region MagneticCalculations Tests

        [TestMethod]
        public void MagneticCalculations_CopyConstructor_CopiesUncertainty()
        {
            // Arrange
            var original = new GeoMagSharp.MagneticCalculations();
            original.Uncertainty = new GeoMagSharp.GeomagneticUncertainty
            {
                ModelCategory = GeoMagSharp.GeomagneticModelCategory.LowResolution,
                Declination = 0.36,
                BhDependentDec = 5000,
                TotalField = 157,
                DipAngle = 0.24,
                Revision = "Rev5.13"
            };

            // Act
            var copy = new GeoMagSharp.MagneticCalculations(original);

            // Assert
            Assert.IsNotNull(copy.Uncertainty);
            Assert.AreEqual(0.36, copy.Uncertainty.Declination, 0.001);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.LowResolution, copy.Uncertainty.ModelCategory);
        }

        [TestMethod]
        public void MagneticCalculations_CopyConstructor_HandlesNullUncertainty()
        {
            // Arrange
            var original = new GeoMagSharp.MagneticCalculations();
            // Uncertainty is null by default

            // Act
            var copy = new GeoMagSharp.MagneticCalculations(original);

            // Assert
            Assert.IsNull(copy.Uncertainty);
        }

        #endregion
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~UncertaintyUnitTest.MagneticCalculations" --verbosity normal`

Expected: FAIL — `Uncertainty` property does not exist.

- [ ] **Step 3: Add property and update copy constructors**

In `src/GeoMagSharp/Models/Results/MagneticCalculations.cs`:

In the default constructor (after `TotalField = new MagneticValue();` on line 31), add:
```csharp
            Uncertainty = null;
```

In the copy constructor (after `TotalField = new MagneticValue(other.TotalField);` on line 47), add:
```csharp
            Uncertainty = other.Uncertainty; // Reference copy — uncertainty is immutable per calculation
```

In the properties section (after `public MagneticValue TotalField { get; set; }` on line 145), add:

```csharp

        /// <summary>
        /// ISCWSA-based 1-sigma geomagnetic uncertainty for this calculation.
        /// Null if model category is Unknown and no override was provided.
        /// </summary>
        public GeomagneticUncertainty Uncertainty { get; set; }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~UncertaintyUnitTest" --verbosity normal`

Expected: PASS (all 23 tests)

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Models/Results/MagneticCalculations.cs tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs
git commit -m "feat: add Uncertainty property to MagneticCalculations"
```

---

### Task 7: Fix WMMHR/WMM detection order in `CheckStringForModel()`

**Files:**
- Modify: `src/GeoMagSharp/ExtensionMethods.cs`
- Modify: `tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs`

**Context:** The current `CheckStringForModel()` iterates `Enum.GetValues(typeof(knownModels))` which returns values in numeric order: NONE(0), DGRF(1), EMM(2), IGRF(3), WMM(4), WMMHR(5). The `IndexOf` call on line 248 uses `StringComparison.OrdinalIgnoreCase`. Since WMM(4) is checked before WMMHR(5), a string containing "WMMHR" will match "WMM" first via `IndexOf`. We must check WMMHR before WMM.

- [ ] **Step 1: Write test proving the bug**

Add to `UncertaintyUnitTest.cs`:

```csharp
        #region WMMHR Detection Tests

        [TestMethod]
        public void CheckStringForModel_WMMHR_DetectedAsWMMHR_NotWMM()
        {
            // This tests the substring collision: "WMMHR" contains "WMM"
            string header = "WMMHR2025  2025.0  12/17/2024";
            var result = header.CheckStringForModel();
            Assert.AreEqual(GeoMagSharp.knownModels.WMMHR, result);
        }

        [TestMethod]
        public void CheckStringForModel_WMM_StillDetectedCorrectly()
        {
            string header = "WMM2025  2025.0  12/17/2024";
            var result = header.CheckStringForModel();
            Assert.AreEqual(GeoMagSharp.knownModels.WMM, result);
        }

        [TestMethod]
        public void CheckStringForModel_WMMHR_NewFormat_Detected()
        {
            // New format: year comes first
            string header = "    2025.0            WMMHR-2025        12/17/2024";
            var result = header.CheckStringForModel();
            Assert.AreEqual(GeoMagSharp.knownModels.WMMHR, result);
        }

        #endregion
```

- [ ] **Step 2: Run tests to verify the WMMHR test fails**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~CheckStringForModel_WMMHR" --verbosity normal`

Expected: FAIL — `CheckStringForModel_WMMHR_DetectedAsWMMHR_NotWMM` returns WMM instead of WMMHR.

- [ ] **Step 3: Fix detection order**

In `src/GeoMagSharp/ExtensionMethods.cs`, replace the `foreach` loop approach (lines 242-280) with explicit ordering that checks longer names first. Replace lines 241-280 with:

```csharp
            // Check models in order: longer names first to avoid substring collisions
            // (e.g., "WMMHR" contains "WMM", so WMMHR must be checked before WMM)
            knownModels[] checkOrder = new knownModels[]
            {
                knownModels.WMMHR,  // Must be before WMM (substring collision)
                knownModels.DGRF,
                knownModels.EMM,
                knownModels.IGRF,
                knownModels.WMM
            };

            foreach (knownModels model in checkOrder)
            {
                string modelName = model.ToString();
                Int32 idx = trimmed.IndexOf(modelName, StringComparison.OrdinalIgnoreCase);

                if (idx == -1)
                    continue;

                // EMM can be found anywhere in the line
                if (model.Equals(knownModels.EMM))
                {
                    return model;
                }

                // For other models: accept if found at position 0 (old format)
                if (idx == 0)
                {
                    return model;
                }

                // New format detection: line starts with year (4-digit number like 2020.0)
                if (trimmed.Length >= 4 && char.IsDigit(trimmed[0]))
                {
                    string firstChars = trimmed.Substring(0, 4);
                    if ((firstChars.StartsWith("19") || firstChars.StartsWith("20")) &&
                        char.IsDigit(firstChars[2]) && char.IsDigit(firstChars[3]))
                    {
                        return model;
                    }
                }
            }
```

- [ ] **Step 4: Run all tests to verify fix and no regressions**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --verbosity normal`

Expected: ALL tests PASS (existing + new).

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/ExtensionMethods.cs tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs
git commit -m "fix: check WMMHR before WMM in CheckStringForModel to avoid substring collision"
```

---

### Task 8: Wire uncertainty into `GeoMag.cs` calculation pipeline

**Files:**
- Modify: `src/GeoMagSharp/GeoMag.cs`
- Modify: `tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs`

- [ ] **Step 1: Write integration tests first (TDD)**

Add to `UncertaintyUnitTest.cs`:

```csharp
        #region Integration Tests

        [TestMethod]
        public void Integration_WMMCalculation_HasLowResolutionUncertainty()
        {
            // Arrange — load WMM2025 and run a spot calculation
            var geoMag = new GeoMagSharp.GeoMag();
            geoMag.LoadModel(GeoMagSharp.knownModels.WMM);

            var options = new GeoMagSharp.CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -105.0,
                StartDate = new System.DateTime(2025, 1, 1)
            };
            options.SetElevation(0, GeoMagSharp.Distance.Unit.meter);

            // Act
            geoMag.MagneticCalculations(options);

            // Assert
            Assert.IsTrue(geoMag.ResultsOfCalculation.Count > 0);
            var result = geoMag.ResultsOfCalculation[0];
            Assert.IsNotNull(result.Uncertainty);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.LowResolution, result.Uncertainty.ModelCategory);
            Assert.AreEqual(0.36, result.Uncertainty.Declination, 0.001);
            Assert.AreEqual(5000, result.Uncertainty.BhDependentDec, 0.1);
            Assert.AreEqual(157, result.Uncertainty.TotalField, 0.1);
            Assert.AreEqual(0.24, result.Uncertainty.DipAngle, 0.001);
        }

        [TestMethod]
        public void Integration_WMMCalculation_WithIFROverride_HasIFR1Uncertainty()
        {
            // Arrange — load WMM but override to IFR1
            var geoMag = new GeoMagSharp.GeoMag();
            geoMag.LoadModel(GeoMagSharp.knownModels.WMM);

            var options = new GeoMagSharp.CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -105.0,
                StartDate = new System.DateTime(2025, 1, 1),
                ModelCategoryOverride = GeoMagSharp.GeomagneticModelCategory.InFieldReference1
            };
            options.SetElevation(0, GeoMagSharp.Distance.Unit.meter);

            // Act
            geoMag.MagneticCalculations(options);

            // Assert
            var result = geoMag.ResultsOfCalculation[0];
            Assert.IsNotNull(result.Uncertainty);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.InFieldReference1, result.Uncertainty.ModelCategory);
            Assert.AreEqual(0.15, result.Uncertainty.Declination, 0.001);
            Assert.AreEqual(1500, result.Uncertainty.BhDependentDec, 0.1);
        }

        #endregion
```

- [ ] **Step 2: Run integration tests to verify they fail**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --filter "FullyQualifiedName~Integration_" --verbosity normal`

Expected: FAIL — `Uncertainty` is null because `GeoMag.cs` doesn't populate it yet.

- [ ] **Step 3: Add uncertainty attachment to synchronous calculation path**

In `src/GeoMagSharp/GeoMag.cs`, after line 146 (`if (magCalcDate != null) ResultsOfCalculation.Add(magCalcDate);`), wrap and expand to:

```csharp
                if (magCalcDate != null)
                {
                    magCalcDate.Uncertainty = UncertaintyDataProvider.GetUncertainty(
                        _Models.Type, _CalculationOptions.ModelCategoryOverride);
                    ResultsOfCalculation.Add(magCalcDate);
                }
```

(This replaces the single-line `if (magCalcDate != null) ResultsOfCalculation.Add(magCalcDate);`)

- [ ] **Step 4: Add uncertainty attachment to async calculation path**

In `src/GeoMagSharp/GeoMag.cs`, after line 354 (`if (magCalcDate != null) ResultsOfCalculation.Add(magCalcDate);`), apply the same pattern:

```csharp
                if (magCalcDate != null)
                {
                    magCalcDate.Uncertainty = UncertaintyDataProvider.GetUncertainty(
                        _Models.Type, _CalculationOptions.ModelCategoryOverride);
                    ResultsOfCalculation.Add(magCalcDate);
                }
```

- [ ] **Step 5: Run all tests to verify integration tests now pass**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --verbosity normal`

Expected: ALL tests PASS (including the integration tests from Step 1).

- [ ] **Step 6: Commit**

```bash
git add src/GeoMagSharp/GeoMag.cs tests/GeoMagSharp.Tests/UncertaintyUnitTest.cs
git commit -m "feat: attach ISCWSA uncertainty to calculation results in GeoMag pipeline"
```

---

### Task 9: Final verification — full test suite

- [ ] **Step 1: Run complete test suite**

Run: `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Release --verbosity normal`

Expected: ALL tests PASS (existing tests + all new uncertainty tests).

- [ ] **Step 2: Run release build**

Run: `dotnet build -c Release`

Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Verify no warnings related to new code**

Check build output for any warnings in new files. Address if found.

- [ ] **Step 4: Commit any final fixes if needed**
