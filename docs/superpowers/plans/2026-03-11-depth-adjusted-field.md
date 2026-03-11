# Depth-Adjusted Magnetic Field Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add depth-adjusted magnetic field values and depth-dependent uncertainty per SPE-128217-MS, with both standalone and pipeline-integrated API.

**Architecture:** Post-processing layer that takes surface field results and applies dipole depth correction equations (Eq 1-8). New `DepthCorrection` static class handles all math. Results attached to existing `MagneticCalculations` via nullable `DepthCorrection` property. Depth uncertainty added to existing `GeomagneticUncertainty` class.

**Tech Stack:** C# (.NET Framework 4.8 / .NET Standard 2.0), MSTest, GeoMagSharp library

**Spec:** `docs/superpowers/specs/2026-03-11-depth-adjusted-field-design.md`

---

## Chunk 1: Result Class and Core Math

### Task 1: Create DepthCorrectionResult class

**Files:**
- Create: `src/GeoMagSharp/Models/Results/DepthCorrectionResult.cs`

- [ ] **Step 1: Create the result class**

```csharp
/****************************************************************************
 * File:            DepthCorrectionResult.cs
 * Description:     Result class for dipole depth correction (SPE-128217-MS)
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

namespace GeoMagSharp
{
    /// <summary>
    /// Results of dipole depth correction per SPE-128217-MS (Ekseth &amp; Weston, 2010).
    /// Null tool-frame and azimuth error properties indicate wellbore geometry was not provided.
    /// </summary>
    public class DepthCorrectionResult
    {
        /// <summary>Dipole scaling factor: R³/(R-D)³</summary>
        public double DipoleScalingFactor { get; set; }

        /// <summary>Horizontal intensity at depth (nT), Eq 1</summary>
        public double HorizontalIntensityAtDepth { get; set; }

        /// <summary>Vertical intensity at depth (nT), Eq 2</summary>
        public double VerticalIntensityAtDepth { get; set; }

        /// <summary>Total field at depth (nT), derived from Eq 1-2</summary>
        public double TotalFieldAtDepth { get; set; }

        /// <summary>Horizontal field error from using surface values (nT), Eq 3</summary>
        public double HorizontalError { get; set; }

        /// <summary>Vertical field error from using surface values (nT), Eq 4</summary>
        public double VerticalError { get; set; }

        /// <summary>High-side error component (nT), Eq 5. Null if no wellbore geometry.</summary>
        public double? HighSideError { get; set; }

        /// <summary>High-side-right error component (nT), Eq 6. Null if no wellbore geometry.</summary>
        public double? HighSideRightError { get; set; }

        /// <summary>Along-hole error component (nT), Eq 7. Null if no wellbore geometry.</summary>
        public double? AlongHoleError { get; set; }

        /// <summary>Azimuth error estimate (degrees), Eq 8. Null if no wellbore geometry.</summary>
        public double? AzimuthErrorDeg { get; set; }

        /// <summary>Singularity proximity: (1 - sin²A·sin²I). Values near 0 indicate E-W singularity.</summary>
        public double? SingularityFactor { get; set; }

        /// <summary>True when SingularityFactor &lt; 0.1, indicating Eq 8 is unreliable.</summary>
        public bool? NearSingularity { get; set; }

        /// <summary>Geomagnetic latitude (degrees), derived from field: atan(Bv / 2Bh)</summary>
        public double GeomagneticLatitudeDeg { get; set; }

        /// <summary>Survey depth below surface (meters)</summary>
        public double DepthMeters { get; set; }

        /// <summary>Reference paper identifier</summary>
        public string Reference { get; set; }
    }
}
```

- [ ] **Step 2: Verify build succeeds**

Run: `dotnet build -c Release`
Expected: Build succeeded. 0 Error(s)

- [ ] **Step 3: Commit**

```bash
git add src/GeoMagSharp/Models/Results/DepthCorrectionResult.cs
git commit -m "feat: add DepthCorrectionResult class for SPE-128217 depth corrections"
```

---

### Task 2: Create DepthCorrection static class with core dipole math

**Files:**
- Create: `src/GeoMagSharp/DepthCorrection.cs`
- Test: `tests/GeoMagSharp.Tests/DepthCorrectionUnitTest.cs`

- [ ] **Step 1: Write failing tests for input validation and dipole scaling**

Create `tests/GeoMagSharp.Tests/DepthCorrectionUnitTest.cs`:

```csharp
/****************************************************************************
 * File:            DepthCorrectionUnitTest.cs
 * Description:     Tests for dipole depth correction (SPE-128217-MS)
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests
{
    [TestClass]
    public class DepthCorrectionUnitTest
    {
        // Worked example from spec: B_h=20000, B_v=40000, D=3000m, A=45°, I=60°
        private const double Bh = 20000.0;
        private const double Bv = 40000.0;
        private const double F = 44721.36; // sqrt(20000² + 40000²)
        private const double DepthM = 3000.0;
        private const double Azimuth = 45.0;
        private const double Inclination = 60.0;

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Calculate_NegativeDepth_ThrowsException()
        {
            DepthCorrection.Calculate(Bh, Bv, F, -100);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Calculate_ZeroEarthRadius_ThrowsException()
        {
            DepthCorrection.Calculate(Bh, Bv, F, DepthM, earthRadiusKm: 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Calculate_InclinationOutOfRange_ThrowsException()
        {
            DepthCorrection.Calculate(Bh, Bv, F, DepthM, wellboreAzimuthDeg: 45, wellboreInclinationDeg: 200);
        }

        [TestMethod]
        public void Calculate_ZeroDepth_ReturnsIdentity()
        {
            var result = DepthCorrection.Calculate(Bh, Bv, F, 0);

            Assert.AreEqual(1.0, result.DipoleScalingFactor, 1e-10);
            Assert.AreEqual(Bh, result.HorizontalIntensityAtDepth, 0.01);
            Assert.AreEqual(Bv, result.VerticalIntensityAtDepth, 0.01);
            Assert.AreEqual(0.0, result.HorizontalError, 1e-10);
            Assert.AreEqual(0.0, result.VerticalError, 1e-10);
        }

        [TestMethod]
        public void Calculate_1kmDepth_CorrectScalingFactor()
        {
            var result = DepthCorrection.Calculate(Bh, Bv, F, 1000);

            // R³/(R-D)³ where R=6371200m, D=1000m
            double R = Constants.EarthsRadiusInKm * 1000;
            double expected = Math.Pow(R / (R - 1000), 3);
            Assert.AreEqual(expected, result.DipoleScalingFactor, 1e-8);
        }

        [TestMethod]
        public void Calculate_WorkedExample_GeomagneticLatitude()
        {
            // B_h=20000, B_v=40000 → φ = atan(40000/(2*20000)) = atan(1) = 45°
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM);
            Assert.AreEqual(45.0, result.GeomagneticLatitudeDeg, 0.01);
        }

        [TestMethod]
        public void Calculate_WorkedExample_HorizontalError()
        {
            // Eq 3: ΔB_h = 3·B₀·cos(φ)·D/R
            // B₀ = 20000/cos(45°) = 28284.3, ΔB_h = 3·28284.3·cos(45°)·3000/6371200 ≈ 28.2 nT
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM);
            Assert.AreEqual(28.2, result.HorizontalError, 0.5);
        }

        [TestMethod]
        public void Calculate_WorkedExample_VerticalError()
        {
            // Eq 4: ΔB_v = 6·B₀·sin(φ)·D/R ≈ 56.5 nT
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM);
            Assert.AreEqual(56.5, result.VerticalError, 0.5);
        }

        [TestMethod]
        public void Calculate_WorkedExample_FieldAtDepth()
        {
            // B_h(D) ≈ 20028.3, B_v(D) ≈ 40056.5
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM);
            Assert.AreEqual(20028.3, result.HorizontalIntensityAtDepth, 1.0);
            Assert.AreEqual(40056.5, result.VerticalIntensityAtDepth, 1.0);
        }

        [TestMethod]
        public void Calculate_WorkedExample_TotalFieldAtDepth()
        {
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM);
            double expectedF = F * result.DipoleScalingFactor;
            Assert.AreEqual(expectedF, result.TotalFieldAtDepth, 0.1);
            // Also verify it equals sqrt(Bh² + Bv²) at depth
            double fromComponents = Math.Sqrt(
                result.HorizontalIntensityAtDepth * result.HorizontalIntensityAtDepth +
                result.VerticalIntensityAtDepth * result.VerticalIntensityAtDepth);
            Assert.AreEqual(fromComponents, result.TotalFieldAtDepth, 0.1);
        }

        [TestMethod]
        public void Calculate_NullWellboreGeometry_SkipsToolFrameAndAzimuth()
        {
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM);

            Assert.IsNull(result.HighSideError);
            Assert.IsNull(result.HighSideRightError);
            Assert.IsNull(result.AlongHoleError);
            Assert.IsNull(result.AzimuthErrorDeg);
            Assert.IsNull(result.SingularityFactor);
            Assert.IsNull(result.NearSingularity);
        }

        [TestMethod]
        public void Calculate_ZeroHorizontalIntensity_MagneticPole()
        {
            // At magnetic pole, B_h ≈ 0, B_v is large
            var result = DepthCorrection.Calculate(0.1, 60000, 60000, DepthM);
            Assert.AreEqual(90.0, result.GeomagneticLatitudeDeg, 1.0);
        }

        [TestMethod]
        public void Calculate_Reference_AlwaysSPE128217()
        {
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM);
            Assert.AreEqual("SPE-128217-MS", result.Reference);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test -c Release --verbosity normal`
Expected: FAIL — `DepthCorrection` class does not exist

- [ ] **Step 3: Implement DepthCorrection.Calculate (primitive overload)**

Create `src/GeoMagSharp/DepthCorrection.cs`:

```csharp
/****************************************************************************
 * File:            DepthCorrection.cs
 * Description:     Dipole depth correction per SPE-128217-MS
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 * Notes:           Reference: SPE-128217-MS (Ekseth & Weston, Gyrodata, 2010)
 *                  "Wellbore Positions Obtained While Drilling by the Most
 *                  Advanced Magnetic Surveying Methods May Be Less Accurate
 *                  than Predicted"
 ****************************************************************************/

using System;

namespace GeoMagSharp
{
    /// <summary>
    /// Calculates dipole depth corrections for geomagnetic field values
    /// per SPE-128217-MS (Ekseth &amp; Weston, 2010).
    /// </summary>
    public static class DepthCorrection
    {
        private const double SingularityThreshold = 0.1;
        private const double HorizontalIntensityPoleThreshold = 1.0; // nT
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// Calculate depth correction from surface field values using dipole approximation.
        /// </summary>
        /// <param name="horizontalIntensityNT">Surface horizontal intensity B_h (nT)</param>
        /// <param name="verticalIntensityNT">Surface vertical intensity B_v (nT, positive downward)</param>
        /// <param name="totalFieldNT">Surface total field F (nT)</param>
        /// <param name="depthMeters">Survey depth below surface (meters, must be >= 0)</param>
        /// <param name="wellboreAzimuthDeg">Magnetic azimuth (degrees, 0-360). Null to skip Eq 5-8.</param>
        /// <param name="wellboreInclinationDeg">Wellbore inclination (degrees, 0-180). Null to skip Eq 5-8.</param>
        /// <param name="earthRadiusKm">Earth radius (km). Default: Constants.EarthsRadiusInKm</param>
        public static DepthCorrectionResult Calculate(
            double horizontalIntensityNT,
            double verticalIntensityNT,
            double totalFieldNT,
            double depthMeters,
            double? wellboreAzimuthDeg = null,
            double? wellboreInclinationDeg = null,
            double earthRadiusKm = Constants.EarthsRadiusInKm)
        {
            if (depthMeters < 0)
                throw new ArgumentOutOfRangeException(nameof(depthMeters), "Depth must be >= 0");
            if (earthRadiusKm <= 0)
                throw new ArgumentOutOfRangeException(nameof(earthRadiusKm), "Earth radius must be > 0");
            if (wellboreInclinationDeg.HasValue && (wellboreInclinationDeg.Value < 0 || wellboreInclinationDeg.Value > 180))
                throw new ArgumentOutOfRangeException(nameof(wellboreInclinationDeg), "Inclination must be 0-180 degrees");

            double earthRadiusM = earthRadiusKm * 1000.0;

            // Geomagnetic latitude: φ = atan(Bv / (2·Bh))
            double geomagLatRad;
            if (Math.Abs(horizontalIntensityNT) < HorizontalIntensityPoleThreshold)
            {
                geomagLatRad = Math.PI / 2.0; // 90° at magnetic pole
            }
            else
            {
                geomagLatRad = Math.Atan2(verticalIntensityNT, 2.0 * horizontalIntensityNT);
            }

            double cosPhi = Math.Cos(geomagLatRad);
            double sinPhi = Math.Sin(geomagLatRad);

            // Equatorial dipole field: B₀ = Bh / cos(φ)
            double B0 = Math.Abs(cosPhi) > 1e-10
                ? horizontalIntensityNT / cosPhi
                : verticalIntensityNT / (2.0 * sinPhi);

            // Dipole scaling factor: R³/(R-D)³
            double scalingFactor = Math.Pow(earthRadiusM / (earthRadiusM - depthMeters), 3);

            // Field at depth (Eq 1-2)
            double bhAtDepth = horizontalIntensityNT * scalingFactor;
            double bvAtDepth = verticalIntensityNT * scalingFactor;
            double fAtDepth = totalFieldNT * scalingFactor;

            // Field errors (Eq 3-4): first-order approximation
            double dOverR = depthMeters / earthRadiusM;
            double deltaH = 3.0 * B0 * cosPhi * dOverR;
            double deltaV = 6.0 * B0 * sinPhi * dOverR;

            var result = new DepthCorrectionResult
            {
                DipoleScalingFactor = scalingFactor,
                HorizontalIntensityAtDepth = bhAtDepth,
                VerticalIntensityAtDepth = bvAtDepth,
                TotalFieldAtDepth = fAtDepth,
                HorizontalError = deltaH,
                VerticalError = deltaV,
                GeomagneticLatitudeDeg = geomagLatRad * RadToDeg,
                DepthMeters = depthMeters,
                Reference = "SPE-128217-MS"
            };

            // Tool-frame errors and azimuth error (Eq 5-8) — requires wellbore geometry
            if (wellboreAzimuthDeg.HasValue && wellboreInclinationDeg.HasValue)
            {
                double A = (wellboreAzimuthDeg.Value % 360.0) * DegToRad;
                double I = wellboreInclinationDeg.Value * DegToRad;

                double cosA = Math.Cos(A);
                double sinA = Math.Sin(A);
                double cosI = Math.Cos(I);
                double sinI = Math.Sin(I);

                // Eq 5: ΔB_H (high-side)
                result.HighSideError = 3.0 * B0 * (cosPhi * cosA * cosI - 2.0 * sinPhi * sinI) * dOverR;

                // Eq 6: ΔB_R (high-side-right)
                result.HighSideRightError = -3.0 * B0 * cosPhi * sinA * dOverR;

                // Eq 7: ΔB_A (along-hole)
                result.AlongHoleError = 3.0 * B0 * (cosPhi * cosA * sinI + 2.0 * sinPhi * cosI) * dOverR;

                // Singularity factor: (1 - sin²A·sin²I)
                double singFactor = 1.0 - sinA * sinA * sinI * sinI;
                result.SingularityFactor = singFactor;
                result.NearSingularity = singFactor < SingularityThreshold;

                // Eq 8: ΔA (azimuth error)
                double sin2A = Math.Sin(2.0 * A);
                double sin2I = Math.Sin(2.0 * I);
                double tanPhi = Math.Abs(cosPhi) > 1e-10 ? sinPhi / cosPhi : 1e10;

                double numerator = (sin2A * sinI * sinI + 2.0 * tanPhi * sinA * sin2I) * 1.5 * dOverR;
                double azErrorRad = Math.Abs(singFactor) > 1e-10
                    ? numerator / singFactor
                    : numerator / 1e-10; // Avoid division by zero

                result.AzimuthErrorDeg = azErrorRad * RadToDeg;
            }

            return result;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test -c Release --verbosity normal`
Expected: All DepthCorrectionUnitTest tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/DepthCorrection.cs tests/GeoMagSharp.Tests/DepthCorrectionUnitTest.cs
git commit -m "feat: add DepthCorrection class with dipole math (Eq 1-8)"
```

---

### Task 3: Add tool-frame and boundary tests

**Files:**
- Modify: `tests/GeoMagSharp.Tests/DepthCorrectionUnitTest.cs`

- [ ] **Step 1: Add tool-frame, singularity, and boundary tests**

Append to `DepthCorrectionUnitTest` class:

```csharp
        [TestMethod]
        public void Calculate_WorkedExample_ToolFrameErrors()
        {
            // A=45°, I=60°, D=3000m, B₀=28284.3, φ=45°
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM,
                wellboreAzimuthDeg: Azimuth, wellboreInclinationDeg: Inclination);

            Assert.IsNotNull(result.HighSideError);
            Assert.IsNotNull(result.HighSideRightError);
            Assert.IsNotNull(result.AlongHoleError);

            // Eq 5: 3·B₀·(cos45·cos45·cos60 - 2·sin45·sin60)·D/R
            double B0 = 28284.3;
            double dOverR = 3000.0 / (Constants.EarthsRadiusInKm * 1000);
            double cos45 = Math.Cos(45 * Math.PI / 180);
            double sin45 = Math.Sin(45 * Math.PI / 180);
            double cos60 = Math.Cos(60 * Math.PI / 180);
            double sin60 = Math.Sin(60 * Math.PI / 180);

            double expectedH = 3 * B0 * (cos45 * cos45 * cos60 - 2 * sin45 * sin60) * dOverR;
            double expectedR = -3 * B0 * cos45 * sin45 * dOverR;
            double expectedA = 3 * B0 * (cos45 * cos45 * sin60 + 2 * sin45 * cos60) * dOverR;

            Assert.AreEqual(expectedH, result.HighSideError.Value, 0.5);
            Assert.AreEqual(expectedR, result.HighSideRightError.Value, 0.5);
            Assert.AreEqual(expectedA, result.AlongHoleError.Value, 0.5);
        }

        [TestMethod]
        public void Calculate_WorkedExample_AzimuthError()
        {
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM,
                wellboreAzimuthDeg: Azimuth, wellboreInclinationDeg: Inclination);

            // From spec worked example: ΔA ≈ 0.128°
            Assert.IsNotNull(result.AzimuthErrorDeg);
            Assert.AreEqual(0.128, result.AzimuthErrorDeg.Value, 0.01);
        }

        [TestMethod]
        public void Calculate_WorkedExample_SingularityFactor()
        {
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM,
                wellboreAzimuthDeg: Azimuth, wellboreInclinationDeg: Inclination);

            // 1 - sin²(45)·sin²(60) = 1 - 0.5·0.75 = 0.625
            Assert.AreEqual(0.625, result.SingularityFactor.Value, 0.001);
            Assert.IsFalse(result.NearSingularity.Value);
        }

        [TestMethod]
        public void Calculate_NearEastWest_HighInclination_FlagsSingularity()
        {
            // A=90° (east), I=85° → singFactor = 1 - sin²(90)·sin²(85) ≈ 1 - 0.9924 = 0.0076
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM,
                wellboreAzimuthDeg: 90, wellboreInclinationDeg: 85);

            Assert.IsTrue(result.NearSingularity.Value);
            Assert.IsTrue(result.SingularityFactor.Value < 0.1);
        }

        [TestMethod]
        public void Calculate_VerticalWell_ToolFrameErrors()
        {
            // I=0° (vertical): cosI=1, sinI=0
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM,
                wellboreAzimuthDeg: 0, wellboreInclinationDeg: 0);

            // Eq 6: ΔB_R = -3·B₀·cos(φ)·sin(0)·D/R = 0
            Assert.AreEqual(0.0, result.HighSideRightError.Value, 0.01);
            // Eq 8: sin(2A)=0, sin(2I)=0 → ΔA = 0
            Assert.AreEqual(0.0, result.AzimuthErrorDeg.Value, 0.001);
        }

        [TestMethod]
        public void Calculate_NorthAzimuth_ToolFrameErrors()
        {
            // A=0° (north): sinA=0
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM,
                wellboreAzimuthDeg: 0, wellboreInclinationDeg: Inclination);

            // Eq 6: ΔB_R = -3·B₀·cos(φ)·sin(0)·D/R = 0
            Assert.AreEqual(0.0, result.HighSideRightError.Value, 0.01);
        }
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test -c Release --verbosity normal`
Expected: All tests PASS

- [ ] **Step 3: Commit**

```bash
git add tests/GeoMagSharp.Tests/DepthCorrectionUnitTest.cs
git commit -m "test: add tool-frame, singularity, and boundary tests for depth correction"
```

---

## Chunk 2: Convenience Overload and Input Properties

### Task 4: Add MagneticCalculations convenience overload

**Files:**
- Modify: `src/GeoMagSharp/DepthCorrection.cs`
- Modify: `tests/GeoMagSharp.Tests/DepthCorrectionUnitTest.cs`

- [ ] **Step 1: Write failing test for convenience overload**

Append to `DepthCorrectionUnitTest`:

```csharp
        [TestMethod]
        public void Calculate_ConvenienceOverload_MatchesPrimitive()
        {
            // Create a MagneticCalculations with known values
            var magCalc = new MagneticCalculations
            {
                HorizontalIntensity = new MagneticValue { Value = Bh },
                VerticalComp = new MagneticValue { Value = Bv },
                TotalField = new MagneticValue { Value = F }
            };

            var fromPrimitive = DepthCorrection.Calculate(Bh, Bv, F, DepthM,
                wellboreAzimuthDeg: Azimuth, wellboreInclinationDeg: Inclination);
            var fromOverload = DepthCorrection.Calculate(magCalc, DepthM,
                wellboreAzimuthDeg: Azimuth, wellboreInclinationDeg: Inclination);

            Assert.AreEqual(fromPrimitive.DipoleScalingFactor, fromOverload.DipoleScalingFactor, 1e-10);
            Assert.AreEqual(fromPrimitive.HorizontalIntensityAtDepth, fromOverload.HorizontalIntensityAtDepth, 0.01);
            Assert.AreEqual(fromPrimitive.VerticalIntensityAtDepth, fromOverload.VerticalIntensityAtDepth, 0.01);
            Assert.AreEqual(fromPrimitive.AzimuthErrorDeg.Value, fromOverload.AzimuthErrorDeg.Value, 1e-10);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test -c Release --verbosity normal`
Expected: FAIL — overload does not exist

- [ ] **Step 3: Implement the convenience overload**

Add to `DepthCorrection` class:

```csharp
        /// <summary>
        /// Convenience overload accepting MagneticCalculations directly.
        /// Extracts HorizontalIntensity, VerticalComp, and TotalField values.
        /// </summary>
        public static DepthCorrectionResult Calculate(
            MagneticCalculations surfaceField,
            double depthMeters,
            double? wellboreAzimuthDeg = null,
            double? wellboreInclinationDeg = null)
        {
            if (surfaceField == null)
                throw new ArgumentNullException(nameof(surfaceField));

            return Calculate(
                surfaceField.HorizontalIntensity.Value,
                surfaceField.VerticalComp.Value,
                surfaceField.TotalField.Value,
                depthMeters,
                wellboreAzimuthDeg,
                wellboreInclinationDeg);
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test -c Release --verbosity normal`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/DepthCorrection.cs tests/GeoMagSharp.Tests/DepthCorrectionUnitTest.cs
git commit -m "feat: add MagneticCalculations convenience overload to DepthCorrection"
```

---

### Task 5: Add SurveyDepthMeters and wellbore properties to CalculationOptions

**Files:**
- Modify: `src/GeoMagSharp/Models/Configuration/CalculationOptions.cs`

- [ ] **Step 1: Add properties and update copy constructor**

Add three nullable properties to `CalculationOptions` (after the existing `ModelCategoryOverride` property):

```csharp
        /// <summary>Survey depth below surface in meters (TVD). Always positive. Null to skip depth correction.</summary>
        public double? SurveyDepthMeters { get; set; }

        /// <summary>Wellbore magnetic azimuth in degrees (0-360). Required for tool-frame errors (Eq 5-8).</summary>
        public double? WellboreAzimuth { get; set; }

        /// <summary>Wellbore inclination in degrees (0-180, MWD convention). Required for tool-frame errors (Eq 5-8).</summary>
        public double? WellboreInclination { get; set; }
```

In the copy constructor (around line 43-57), add after the `ModelCategoryOverride` copy (line 52):

```csharp
            SurveyDepthMeters = other.SurveyDepthMeters;
            WellboreAzimuth = other.WellboreAzimuth;
            WellboreInclination = other.WellboreInclination;
```

- [ ] **Step 2: Verify build and all existing tests still pass**

Run: `dotnet test -c Release --verbosity normal`
Expected: All tests PASS (no behavioral change)

- [ ] **Step 3: Commit**

```bash
git add src/GeoMagSharp/Models/Configuration/CalculationOptions.cs
git commit -m "feat: add SurveyDepthMeters and wellbore geometry to CalculationOptions"
```

---

### Task 6: Add DepthCorrection property to MagneticCalculations and DepthAzimuthUncertainty to GeomagneticUncertainty

**Files:**
- Modify: `src/GeoMagSharp/Models/Results/MagneticCalculations.cs`
- Modify: `src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs`

- [ ] **Step 1: Add DepthCorrection property to MagneticCalculations**

Add property (after `Uncertainty` at line ~153):

```csharp
        /// <summary>Depth correction results per SPE-128217-MS. Null when no survey depth specified.</summary>
        public DepthCorrectionResult DepthCorrection { get; set; }
```

In the copy constructor (around line 39-50), add after `Uncertainty` copy (line 49):

```csharp
            DepthCorrection = other.DepthCorrection;
```

- [ ] **Step 2: Add DepthAzimuthUncertainty to GeomagneticUncertainty**

Add property (after `DipAngle` property):

```csharp
        /// <summary>
        /// Depth-dependent azimuth uncertainty in degrees (1σ), per SPE-128217-MS.
        /// When wellbore geometry provided: computed from Eq 8.
        /// When not provided: 0.38° global average from Monte Carlo.
        /// Null when no survey depth specified.
        /// </summary>
        public double? DepthAzimuthUncertainty { get; set; }
```

- [ ] **Step 3: Verify build and all existing tests still pass**

Run: `dotnet test -c Release --verbosity normal`
Expected: All tests PASS

- [ ] **Step 4: Commit**

```bash
git add src/GeoMagSharp/Models/Results/MagneticCalculations.cs src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs
git commit -m "feat: add DepthCorrection and DepthAzimuthUncertainty properties"
```

---

## Chunk 3: Pipeline Integration and Integration Tests

### Task 7: Integrate depth correction into GeoMag pipeline

**Files:**
- Modify: `src/GeoMagSharp/GeoMag.cs`

- [ ] **Step 1: Add depth correction to sync pipeline**

In the sync `MagneticCalculations` method, after the uncertainty attachment (around line 151), add:

```csharp
                    // Depth correction (SPE-128217-MS) — only when survey depth specified
                    if (_CalculationOptions.SurveyDepthMeters.HasValue && _CalculationOptions.SurveyDepthMeters.Value > 0)
                    {
                        magCalcDate.DepthCorrection = DepthCorrection.Calculate(
                            magCalcDate,
                            _CalculationOptions.SurveyDepthMeters.Value,
                            _CalculationOptions.WellboreAzimuth,
                            _CalculationOptions.WellboreInclination);

                        // Add depth-dependent uncertainty
                        if (magCalcDate.Uncertainty != null)
                        {
                            magCalcDate.Uncertainty.DepthAzimuthUncertainty =
                                magCalcDate.DepthCorrection.AzimuthErrorDeg.HasValue
                                    ? Math.Abs(magCalcDate.DepthCorrection.AzimuthErrorDeg.Value)
                                    : 0.38; // Global average from SPE-128217 Monte Carlo
                        }
                    }
```

- [ ] **Step 2: Add depth correction to async pipeline**

In the async `MagneticCalculationsAsync` method, after the uncertainty attachment (around line 366), add the same block:

```csharp
                        // Depth correction (SPE-128217-MS) — only when survey depth specified
                        if (_CalculationOptions.SurveyDepthMeters.HasValue && _CalculationOptions.SurveyDepthMeters.Value > 0)
                        {
                            magCalcDate.DepthCorrection = DepthCorrection.Calculate(
                                magCalcDate,
                                _CalculationOptions.SurveyDepthMeters.Value,
                                _CalculationOptions.WellboreAzimuth,
                                _CalculationOptions.WellboreInclination);

                            if (magCalcDate.Uncertainty != null)
                            {
                                magCalcDate.Uncertainty.DepthAzimuthUncertainty =
                                    magCalcDate.DepthCorrection.AzimuthErrorDeg.HasValue
                                        ? Math.Abs(magCalcDate.DepthCorrection.AzimuthErrorDeg.Value)
                                        : 0.38;
                            }
                        }
```

- [ ] **Step 3: Verify build and all existing tests still pass**

Run: `dotnet test -c Release --verbosity normal`
Expected: All tests PASS (depth correction only triggers when SurveyDepthMeters is set)

- [ ] **Step 4: Commit**

```bash
git add src/GeoMagSharp/GeoMag.cs
git commit -m "feat: integrate depth correction into sync and async calculation pipeline"
```

---

### Task 8: Add integration tests

**Files:**
- Modify: `tests/GeoMagSharp.Tests/DepthCorrectionUnitTest.cs`

- [ ] **Step 1: Add integration tests using WMM2025**

Append to `DepthCorrectionUnitTest` class:

```csharp
        #region Integration Tests

        private static GeoMag LoadWMM2025()
        {
            var possiblePaths = new[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "TestData"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "tests", "GeoMagSharp.Tests", "TestData"),
                @"C:\GitHub\GeoMagSharp\tests\GeoMagSharp.Tests\TestData"
            };

            foreach (var path in possiblePaths)
            {
                var candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(path, "WMM2025.COF"));
                if (System.IO.File.Exists(candidate))
                {
                    var geoMag = new GeoMag();
                    geoMag.SetModel(candidate);
                    return geoMag;
                }
            }
            Assert.Fail("Could not find WMM2025.COF in TestData directory");
            return null;
        }

        [TestMethod]
        public void Pipeline_WithoutDepth_NullCorrection()
        {
            var geoMag = LoadWMM2025();
            var options = new CalculationOptions
            {
                Latitude = 45,
                Longitude = 0,
                StartDate = new DateTime(2025, 1, 1)
            };
            options.SetElevation(0, Distance.Unit.meter, true);

            var results = geoMag.MagneticCalculations(options);

            Assert.IsTrue(results.Count > 0);
            Assert.IsNull(results[0].DepthCorrection, "DepthCorrection should be null when no depth specified");
        }

        [TestMethod]
        public void Pipeline_WithDepth_PopulatesCorrection()
        {
            var geoMag = LoadWMM2025();
            var options = new CalculationOptions
            {
                Latitude = 45,
                Longitude = 0,
                StartDate = new DateTime(2025, 1, 1),
                SurveyDepthMeters = 2000
            };
            options.SetElevation(0, Distance.Unit.meter, true);

            var results = geoMag.MagneticCalculations(options);

            Assert.IsTrue(results.Count > 0);
            var dc = results[0].DepthCorrection;
            Assert.IsNotNull(dc, "DepthCorrection should be populated when depth specified");
            Assert.AreEqual(2000, dc.DepthMeters, 0.01);
            Assert.IsTrue(dc.DipoleScalingFactor > 1.0, "Field should be stronger at depth");
            Assert.IsTrue(dc.HorizontalError > 0, "Horizontal error should be positive");
            Assert.IsTrue(dc.VerticalError > 0, "Vertical error should be positive");
            Assert.AreEqual("SPE-128217-MS", dc.Reference);
        }

        [TestMethod]
        public void Pipeline_WithWellboreGeometry_HasAzimuthError()
        {
            var geoMag = LoadWMM2025();
            var options = new CalculationOptions
            {
                Latitude = 45,
                Longitude = 0,
                StartDate = new DateTime(2025, 1, 1),
                SurveyDepthMeters = 2000,
                WellboreAzimuth = 45,
                WellboreInclination = 60
            };
            options.SetElevation(0, Distance.Unit.meter, true);

            var results = geoMag.MagneticCalculations(options);

            var dc = results[0].DepthCorrection;
            Assert.IsNotNull(dc.AzimuthErrorDeg, "Azimuth error should be computed when wellbore geometry provided");
            Assert.IsNotNull(dc.SingularityFactor);
            Assert.IsNotNull(dc.HighSideError);
        }

        [TestMethod]
        public void Pipeline_DepthUncertainty_AddedToUncertainty()
        {
            var geoMag = LoadWMM2025();
            var options = new CalculationOptions
            {
                Latitude = 45,
                Longitude = 0,
                StartDate = new DateTime(2025, 1, 1),
                SurveyDepthMeters = 2000
            };
            options.SetElevation(0, Distance.Unit.meter, true);

            var results = geoMag.MagneticCalculations(options);

            Assert.IsNotNull(results[0].Uncertainty);
            Assert.IsNotNull(results[0].Uncertainty.DepthAzimuthUncertainty,
                "DepthAzimuthUncertainty should be populated when depth specified");
            Assert.AreEqual(0.38, results[0].Uncertainty.DepthAzimuthUncertainty.Value, 0.01,
                "Without wellbore geometry, should use 0.38° global average");
        }

        [TestMethod]
        public void Pipeline_DateRange_AllStepsGetDepthCorrection()
        {
            var geoMag = LoadWMM2025();
            var options = new CalculationOptions
            {
                Latitude = 45,
                Longitude = 0,
                StartDate = new DateTime(2025, 1, 1),
                EndDate = new DateTime(2025, 7, 1),
                StepInterval = 3,
                SurveyDepthMeters = 1500
            };
            options.SetElevation(0, Distance.Unit.meter, true);

            var results = geoMag.MagneticCalculations(options);

            Assert.IsTrue(results.Count > 1, "Should have multiple date steps");
            foreach (var r in results)
            {
                Assert.IsNotNull(r.DepthCorrection, "Every date step should have depth correction");
                Assert.AreEqual(1500, r.DepthCorrection.DepthMeters, 0.01);
            }
        }

        [TestMethod]
        public void Standalone_MatchesPipeline()
        {
            var geoMag = LoadWMM2025();
            var options = new CalculationOptions
            {
                Latitude = 45,
                Longitude = 0,
                StartDate = new DateTime(2025, 1, 1),
                SurveyDepthMeters = 2000,
                WellboreAzimuth = 45,
                WellboreInclination = 60
            };
            options.SetElevation(0, Distance.Unit.meter, true);

            var pipelineResults = geoMag.MagneticCalculations(options);
            var pipelineDC = pipelineResults[0].DepthCorrection;

            // Now compute standalone using surface field (no depth in options)
            options.SurveyDepthMeters = null;
            var surfaceResults = geoMag.MagneticCalculations(options);
            var standaloneDC = DepthCorrection.Calculate(surfaceResults[0], 2000,
                wellboreAzimuthDeg: 45, wellboreInclinationDeg: 60);

            Assert.AreEqual(pipelineDC.DipoleScalingFactor, standaloneDC.DipoleScalingFactor, 1e-10);
            Assert.AreEqual(pipelineDC.HorizontalError, standaloneDC.HorizontalError, 0.01);
            Assert.AreEqual(pipelineDC.AzimuthErrorDeg.Value, standaloneDC.AzimuthErrorDeg.Value, 0.001);
        }

        [TestMethod]
        public void SHRecalc_vs_Dipole_Agreement()
        {
            // Compare: dipole correction vs. running SH model at reduced altitude
            var geoMag = LoadWMM2025();

            // Surface calculation
            var surfaceOptions = new CalculationOptions
            {
                Latitude = 45,
                Longitude = 0,
                StartDate = new DateTime(2025, 1, 1)
            };
            surfaceOptions.SetElevation(0, Distance.Unit.meter, true);
            var surfaceResults = geoMag.MagneticCalculations(surfaceOptions);
            var surfaceField = surfaceResults[0];

            // Dipole correction for 2km depth
            var dipoleResult = DepthCorrection.Calculate(surfaceField, 2000);

            // SH model at -2km altitude (below sea level)
            var depthOptions = new CalculationOptions
            {
                Latitude = 45,
                Longitude = 0,
                StartDate = new DateTime(2025, 1, 1)
            };
            depthOptions.SetElevation(-2000, Distance.Unit.meter, true);
            var depthResults = geoMag.MagneticCalculations(depthOptions);
            var shField = depthResults[0];

            // They should agree within ~1 nT for horizontal intensity
            Assert.AreEqual(shField.HorizontalIntensity.Value, dipoleResult.HorizontalIntensityAtDepth, 5.0,
                "SH and dipole should agree within 5 nT for horizontal intensity at 2km depth");
        }

        #endregion
```

- [ ] **Step 2: Run all tests**

Run: `dotnet test -c Release --verbosity normal`
Expected: All tests PASS

- [ ] **Step 3: Commit**

```bash
git add tests/GeoMagSharp.Tests/DepthCorrectionUnitTest.cs
git commit -m "test: add integration tests for depth correction pipeline"
```

---

### Task 9: Create tasks.md and push branch

**Files:**
- Create: `docs/features/3-depth-adjusted-field/tasks.md`

- [ ] **Step 1: Create tasks.md**

```markdown
# Feature: Depth-Adjusted Magnetic Field Values
Issue: #3
Branch: feature/3-depth-adjusted-field

## Tasks
- [x] Create DepthCorrectionResult class
- [x] Create DepthCorrection static class with Eq 1-8
- [x] Add tool-frame and boundary tests
- [x] Add MagneticCalculations convenience overload
- [x] Add SurveyDepthMeters and wellbore properties to CalculationOptions
- [x] Add DepthCorrection property to MagneticCalculations and DepthAzimuthUncertainty
- [x] Integrate into GeoMag sync and async pipeline
- [x] Add integration tests with WMM2025
- [ ] 2 clean Ralph Loop cycles

## Completion Criteria
- [ ] All tasks checked
- [ ] Build succeeds
- [ ] Tests pass
- [ ] 2 clean Ralph Loop cycles
```

- [ ] **Step 2: Push branch and create draft PR**

```bash
git add docs/features/3-depth-adjusted-field/tasks.md
git commit -m "docs: add tasks.md for Issue #3 depth-adjusted field"
git push -u origin feature/3-depth-adjusted-field
gh pr create --base development --draft --title "feat: add depth-adjusted magnetic field values (#3)" --body "$(cat <<'EOF'
## Summary
- Add dipole depth correction per SPE-128217-MS (Eq 1-8)
- Standalone `DepthCorrection.Calculate()` API for MSA workflows
- Pipeline-integrated: set `SurveyDepthMeters` in options
- Depth-dependent azimuth uncertainty added to `GeomagneticUncertainty`
- East-west singularity detection and flagging

## References
- SPE-128217-MS (Ekseth & Weston, Gyrodata, 2010)
- Issue #3

## Test plan
- [ ] 17 unit tests for dipole math, validation, boundary cases
- [ ] 7 integration tests with WMM2025 model
- [ ] SH recalculation vs dipole correction agreement test
- [ ] Ralph Loop review (2 clean cycles)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Verify all tests pass one final time**

Run: `dotnet test -c Release --verbosity normal`
Expected: All tests PASS, 0 failures
