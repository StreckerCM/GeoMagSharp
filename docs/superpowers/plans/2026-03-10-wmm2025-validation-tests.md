# WMM2025 Validation Tests Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add parameterized precision validation tests comparing GeoMagSharp WMM2025 output against NOAA's official test values.

**Architecture:** Single new test file with two parameterized test methods (main field + secular variation), each with 12 DataRow entries from NOAA. A shared helper loads the WMM2025 model. Existing tests remain untouched.

**Tech Stack:** C# / MSTest / .NET Framework 4.8 / `[DataRow]` parameterized tests

**Spec:** `docs/superpowers/specs/2026-03-10-wmm2025-validation-tests-design.md`

---

## Chunk 1: Feature Branch Setup and Test File

### Task 1: Create Feature Branch

**Files:** None

- [ ] **Step 1: Create and switch to feature branch from preview**

```bash
git checkout preview
git pull origin preview
git checkout -b feature/4-wmm2025-validation-tests
```

- [ ] **Step 2: Create tasks.md for Ralph Loop tracking**

Create file `docs/features/4-wmm2025-validation/tasks.md`:

```markdown
# Feature: WMM2025 Precision Validation Tests
Issue: #4
Branch: feature/4-wmm2025-validation-tests

## Tasks
- [ ] Create WMM2025ValidationTest.cs with model loading helper
- [ ] Add 12 main field parameterized test cases
- [ ] Add 12 secular variation parameterized test cases
- [ ] Verify all tests pass within tolerance
- [ ] Investigate and document any tolerance exceedances

## Completion Criteria
- [ ] All tasks checked
- [ ] Build succeeds
- [ ] Tests pass
- [ ] 2 clean Ralph Loop cycles
```

- [ ] **Step 3: Commit branch setup**

```bash
git add docs/features/4-wmm2025-validation/tasks.md
git commit -m "[IMPLEMENTER] feat: add tasks.md for Issue #4 WMM2025 validation tests"
```

---

### Task 2: Write Main Field Validation Test

**Files:**
- Create: `tests/GeoMagSharp.Tests/WMM2025ValidationTest.cs`

- [ ] **Step 1: Create the test file with helper and main field test method**

Create `tests/GeoMagSharp.Tests/WMM2025ValidationTest.cs`:

```csharp
/****************************************************************************
 * File:            WMM2025ValidationTest.cs
 * Description:     Precision validation tests against NOAA WMM2025 official test values
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 * Notes:           Reference values from NOAA WMM2025 (December 2024)
 *                  https://www.ncei.noaa.gov/products/world-magnetic-model
 *                  PDF: https://www.ncei.noaa.gov/sites/default/files/2025-02/WMM2025testvalues.pdf
 *                  TXT: https://www.ncei.noaa.gov/sites/default/files/2025-02/WMM2025_TEST_VALUES.txt
 *
 *                  NOAA notes: "The computation was carried out with double precision
 *                  arithmetic. Single precision arithmetic can cause differences of
 *                  up to 0.1 nT."
 *  ****************************************************************************/

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using GeoMagSharp;

namespace GeoMagSharp_UnitTests
{
    [TestClass]
    public class WMM2025ValidationTest
    {
        // Tolerances based on NOAA's single-precision note (0.1 nT)
        // with margin for minor implementation differences
        private const double IntensityTolerance = 1.0;    // nT
        private const double AngleTolerance = 0.01;       // degrees

        private static string TestDataPath;
        private static MagneticModelSet _wmm2025;

        [ClassInitialize]
        public static void ClassInit(TestContext _)
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "TestData"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "tests", "GeoMagSharp.Tests", "TestData"),
                @"C:\GitHub\GeoMagSharp\tests\GeoMagSharp.Tests\TestData"
            };

            string modelPath = null;
            foreach (var path in possiblePaths)
            {
                var candidate = Path.GetFullPath(Path.Combine(path, "WMM2025.COF"));
                if (File.Exists(candidate))
                {
                    modelPath = candidate;
                    break;
                }
            }

            Assert.IsNotNull(modelPath, "Could not find WMM2025.COF in TestData directory");
            _wmm2025 = ModelReader.Read(modelPath);
            Assert.IsNotNull(_wmm2025, "ModelReader.Read returned null for WMM2025.COF");
        }

        //                              date     height  lat    lon     X        Y        Z         H        F        I      D
        [DataRow(2025.0, 0,   80,  0,   6521.6,  145.9,   54791.5,  6523.2,  55178.5,  83.21,  1.28,  DisplayName = "2025.0 0km 80N 0E")]
        [DataRow(2025.0, 0,   0,   120, 39677.8, -109.6,  -10580.2, 39677.9, 41064.3,  -14.93, -0.16, DisplayName = "2025.0 0km 0N 120E")]
        [DataRow(2025.0, 0,   -80, 240, 6117.5,  15751.9, -52022.5, 16898.1, 54698.2,  -72.00, 68.78, DisplayName = "2025.0 0km 80S 240E")]
        [DataRow(2025.0, 100, 80,  0,   6216.0,  92.4,    52598.8,  6216.7,  52964.9,  83.26,  0.85,  DisplayName = "2025.0 100km 80N 0E")]
        [DataRow(2025.0, 100, 0,   120, 37688.6, -96.2,   -10152.1, 37688.7, 39032.1,  -15.08, -0.15, DisplayName = "2025.0 100km 0N 120E")]
        [DataRow(2025.0, 100, -80, 240, 5907.6,  14780.3, -49540.7, 15917.1, 52035.0,  -72.19, 68.21, DisplayName = "2025.0 100km 80S 240E")]
        [DataRow(2027.5, 0,   80,  0,   6500.8,  294.5,   54869.4,  6507.5,  55253.9,  83.24,  2.59,  DisplayName = "2027.5 0km 80N 0E")]
        [DataRow(2027.5, 0,   0,   120, 39701.6, -167.4,  -10381.8, 39702.0, 41036.9,  -14.65, -0.24, DisplayName = "2027.5 0km 0N 120E")]
        [DataRow(2027.5, 0,   -80, 240, 6200.7,  15730.3, -51783.7, 16908.3, 54474.2,  -71.92, 68.49, DisplayName = "2027.5 0km 80S 240E")]
        [DataRow(2027.5, 100, 80,  0,   6196.7,  233.8,   52670.5,  6201.1,  53034.3,  83.29,  2.16,  DisplayName = "2027.5 100km 80N 0E")]
        [DataRow(2027.5, 100, 0,   120, 37711.5, -148.7,  -9969.8,  37711.8, 39007.4,  -14.81, -0.23, DisplayName = "2027.5 100km 0N 120E")]
        [DataRow(2027.5, 100, -80, 240, 5984.0,  14760.1, -49317.7, 15927.0, 51825.7,  -72.10, 67.93, DisplayName = "2027.5 100km 80S 240E")]
        [TestMethod]
        public void MainField_MatchesNOAATestValues(
            double decimalDate, double heightKm, double lat, double lon,
            double expectedX, double expectedY, double expectedZ,
            double expectedH, double expectedF, double expectedI, double expectedD)
        {
            // Arrange
            var dateOfCalc = decimalDate.ToDateTime();
            var calcOptions = new CalculationOptions
            {
                Latitude = lat,
                Longitude = lon,
                StartDate = dateOfCalc,
                SecularVariation = false
            };
            calcOptions.SetElevation(heightKm, Distance.Unit.kilometer, true);

            var internalSH = new Coefficients();
            var externalSH = new Coefficients();
            _wmm2025.GetIntExt(decimalDate, out internalSH, out externalSH);

            // Act
            var result = Calculator.SpotCalculation(calcOptions, dateOfCalc, _wmm2025, internalSH, externalSH);

            // Assert
            var label = $"({lat}, {lon}) h={heightKm}km date={decimalDate}";
            Assert.AreEqual(expectedX, result.NorthComp.Value, IntensityTolerance, $"X (North) at {label}");
            Assert.AreEqual(expectedY, result.EastComp.Value, IntensityTolerance, $"Y (East) at {label}");
            Assert.AreEqual(expectedZ, result.VerticalComp.Value, IntensityTolerance, $"Z (Vertical) at {label}");
            Assert.AreEqual(expectedH, result.HorizontalIntensity.Value, IntensityTolerance, $"H (Horizontal) at {label}");
            Assert.AreEqual(expectedF, result.TotalField.Value, IntensityTolerance, $"F (Total) at {label}");
            Assert.AreEqual(expectedI, result.Inclination.Value, AngleTolerance, $"I (Inclination) at {label}");
            Assert.AreEqual(expectedD, result.Declination.Value, AngleTolerance, $"D (Declination) at {label}");
        }
    }
}
```

- [ ] **Step 2: Build and run the main field tests**

```bash
dotnet build -c Release
dotnet test -c Release --filter "FullyQualifiedName~WMM2025ValidationTest.MainField" --verbosity normal
```

Expected: All 12 main field tests pass. If any fail, note the discrepancy — do NOT loosen tolerances yet.

- [ ] **Step 3: Commit**

```bash
git add tests/GeoMagSharp.Tests/WMM2025ValidationTest.cs
git commit -m "[IMPLEMENTER] feat: add WMM2025 main field validation tests (12 cases)"
```

---

### Task 3: Add Secular Variation Test Method

**Files:**
- Modify: `tests/GeoMagSharp.Tests/WMM2025ValidationTest.cs`

- [ ] **Step 1: Add secular variation test method to WMM2025ValidationTest class**

Add this method to the existing `WMM2025ValidationTest` class, after the `MainField_MatchesNOAATestValues` method:

```csharp
        //                              date     height  lat    lon     Xdot   Ydot    Zdot   Hdot   Fdot    Idot   Ddot
        [DataRow(2025.0, 0,   80,  0,   -8.3,   59.5,   31.1,  -7.0,  30.1,   0.01,  0.52,  DisplayName = "SV 2025.0 0km 80N 0E")]
        [DataRow(2025.0, 0,   0,   120, 9.5,    -23.1,  79.4,  9.6,   -11.2,  0.11,  -0.03, DisplayName = "SV 2025.0 0km 0N 120E")]
        [DataRow(2025.0, 0,   -80, 240, 33.3,   -8.6,   95.5,  4.0,   -89.6,  0.03,  -0.12, DisplayName = "SV 2025.0 0km 80S 240E")]
        [DataRow(2025.0, 100, 80,  0,   -7.7,   56.5,   28.7,  -6.9,  27.6,   0.01,  0.52,  DisplayName = "SV 2025.0 100km 80N 0E")]
        [DataRow(2025.0, 100, 0,   120, 9.2,    -21.0,  72.9,  9.2,   -10.0,  0.11,  -0.03, DisplayName = "SV 2025.0 100km 0N 120E")]
        [DataRow(2025.0, 100, -80, 240, 30.6,   -8.0,   89.2,  3.9,   -83.8,  0.03,  -0.11, DisplayName = "SV 2025.0 100km 80S 240E")]
        [DataRow(2027.5, 0,   80,  0,   -8.3,   59.5,   31.1,  -5.6,  30.3,   0.01,  0.53,  DisplayName = "SV 2027.5 0km 80N 0E")]
        [DataRow(2027.5, 0,   0,   120, 9.5,    -23.1,  79.4,  9.6,   -10.7,  0.11,  -0.03, DisplayName = "SV 2027.5 0km 0N 120E")]
        [DataRow(2027.5, 0,   -80, 240, 33.3,   -8.6,   95.5,  4.2,   -89.5,  0.04,  -0.12, DisplayName = "SV 2027.5 0km 80S 240E")]
        [DataRow(2027.5, 100, 80,  0,   -7.7,   56.5,   28.7,  -5.6,  27.8,   0.01,  0.52,  DisplayName = "SV 2027.5 100km 80N 0E")]
        [DataRow(2027.5, 100, 0,   120, 9.2,    -21.0,  72.9,  9.3,   -9.7,   0.11,  -0.03, DisplayName = "SV 2027.5 100km 0N 120E")]
        [DataRow(2027.5, 100, -80, 240, 30.6,   -8.0,   89.2,  4.0,   -83.7,  0.03,  -0.11, DisplayName = "SV 2027.5 100km 80S 240E")]
        [TestMethod]
        public void SecularVariation_MatchesNOAATestValues(
            double decimalDate, double heightKm, double lat, double lon,
            double expectedXdot, double expectedYdot, double expectedZdot,
            double expectedHdot, double expectedFdot, double expectedIdot, double expectedDdot)
        {
            // Arrange
            var dateOfCalc = decimalDate.ToDateTime();
            var calcOptions = new CalculationOptions
            {
                Latitude = lat,
                Longitude = lon,
                StartDate = dateOfCalc,
                SecularVariation = true
            };
            calcOptions.SetElevation(heightKm, Distance.Unit.kilometer, true);

            var internalSH = new Coefficients();
            var externalSH = new Coefficients();
            _wmm2025.GetIntExt(decimalDate, out internalSH, out externalSH);

            // Act
            var result = Calculator.SpotCalculation(calcOptions, dateOfCalc, _wmm2025, internalSH, externalSH);

            // Assert
            var label = $"({lat}, {lon}) h={heightKm}km date={decimalDate}";
            Assert.AreEqual(expectedXdot, result.NorthComp.ChangePerYear, IntensityTolerance, $"Xdot at {label}");
            Assert.AreEqual(expectedYdot, result.EastComp.ChangePerYear, IntensityTolerance, $"Ydot at {label}");
            Assert.AreEqual(expectedZdot, result.VerticalComp.ChangePerYear, IntensityTolerance, $"Zdot at {label}");
            Assert.AreEqual(expectedHdot, result.HorizontalIntensity.ChangePerYear, IntensityTolerance, $"Hdot at {label}");
            Assert.AreEqual(expectedFdot, result.TotalField.ChangePerYear, IntensityTolerance, $"Fdot at {label}");
            Assert.AreEqual(expectedIdot, result.Inclination.ChangePerYear, AngleTolerance, $"Idot at {label}");
            Assert.AreEqual(expectedDdot, result.Declination.ChangePerYear, AngleTolerance, $"Ddot at {label}");
        }
```

- [ ] **Step 2: Build and run all WMM2025 tests**

```bash
dotnet build -c Release
dotnet test -c Release --filter "FullyQualifiedName~WMM2025ValidationTest" --verbosity normal
```

Expected: All 24 tests (12 main field + 12 secular variation) pass.

- [ ] **Step 3: Run the full test suite to confirm no regressions**

```bash
dotnet test -c Release --verbosity normal
```

Expected: All tests pass (existing + new).

- [ ] **Step 4: Commit**

```bash
git add tests/GeoMagSharp.Tests/WMM2025ValidationTest.cs
git commit -m "[IMPLEMENTER] feat: add WMM2025 secular variation validation tests (12 cases)"
```

---

### Task 4: Investigate Failures (Conditional)

This task only applies if any tests from Tasks 2-3 failed.

- [ ] **Step 1: Document discrepancies**

For each failing test, record: component, expected value, actual value, delta, and whether the pattern is systematic (e.g., all Z values off by same amount).

- [ ] **Step 2: Investigate root cause**

Common causes to check:
- Date conversion: verify `decimalDate.ToDateTime().ToDecimal()` round-trips correctly
- Height handling: verify `SetElevation` with `Distance.Unit.kilometer` and `true` (altitude, not depth) produces the expected geocentric radius
- Coefficient parsing: verify WMM2025.COF is fully parsed (check `_wmm2025.MaxDegree` or similar)

- [ ] **Step 3: Fix or adjust tolerances with documentation**

If the root cause is a genuine library bug, fix it. If it's an acceptable precision difference (e.g., different geodetic constants), loosen the specific tolerance and add a comment explaining why.

- [ ] **Step 4: Commit any fixes**

```bash
git add -A
git commit -m "[TESTER] fix: resolve WMM2025 validation discrepancies"
```

---

### Task 5: Update tasks.md and Final Verification

**Files:**
- Modify: `docs/features/4-wmm2025-validation/tasks.md`

- [ ] **Step 1: Mark all tasks complete in tasks.md**

Update `docs/features/4-wmm2025-validation/tasks.md` — check all completed boxes.

- [ ] **Step 2: Run final full test suite**

```bash
dotnet test -c Release --verbosity normal
```

Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add docs/features/4-wmm2025-validation/tasks.md
git commit -m "[PROJECT_MGR] docs: mark all Issue #4 tasks complete"
```
