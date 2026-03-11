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

        #region Input Validation

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

        #endregion

        #region Dipole Scaling and Field at Depth

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
            // Eq 3: ΔB_h = 3·B₀·cos(φ)·D/R ≈ 28.2 nT
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

        #endregion

        #region Null Wellbore Geometry

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

        #endregion

        #region Magnetic Pole Edge Case

        [TestMethod]
        public void Calculate_ZeroHorizontalIntensity_MagneticPole()
        {
            // At magnetic pole, B_h ≈ 0, B_v is large
            var result = DepthCorrection.Calculate(0.1, 60000, 60000, DepthM);
            Assert.AreEqual(90.0, result.GeomagneticLatitudeDeg, 1.0);
        }

        #endregion

        #region Tool-Frame Errors (Eq 5-7)

        [TestMethod]
        public void Calculate_WorkedExample_ToolFrameErrors()
        {
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM,
                wellboreAzimuthDeg: Azimuth, wellboreInclinationDeg: Inclination);

            Assert.IsNotNull(result.HighSideError);
            Assert.IsNotNull(result.HighSideRightError);
            Assert.IsNotNull(result.AlongHoleError);

            // Compute expected values from equations
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

        #endregion

        #region Azimuth Error and Singularity (Eq 8)

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

        #endregion

        #region Convenience Overload

        [TestMethod]
        public void Calculate_ConvenienceOverload_MatchesPrimitive()
        {
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

        #endregion

        #region Metadata

        [TestMethod]
        public void Calculate_Reference_AlwaysSPE128217()
        {
            var result = DepthCorrection.Calculate(Bh, Bv, F, DepthM);
            Assert.AreEqual("SPE-128217-MS", result.Reference);
        }

        #endregion

        #region CalculationOptions Properties

        [TestMethod]
        public void CalculationOptions_DepthProperties_DefaultToNull()
        {
            var options = new CalculationOptions();
            Assert.IsNull(options.SurveyDepthMeters);
            Assert.IsNull(options.WellboreAzimuthDeg);
            Assert.IsNull(options.WellboreInclinationDeg);
        }

        [TestMethod]
        public void CalculationOptions_CopyConstructor_CopiesDepthProperties()
        {
            var original = new CalculationOptions
            {
                SurveyDepthMeters = 3000.0,
                WellboreAzimuthDeg = 45.0,
                WellboreInclinationDeg = 60.0
            };

            var copy = new CalculationOptions(original);

            Assert.AreEqual(3000.0, copy.SurveyDepthMeters);
            Assert.AreEqual(45.0, copy.WellboreAzimuthDeg);
            Assert.AreEqual(60.0, copy.WellboreInclinationDeg);
        }

        #endregion

        #region MagneticCalculations.DepthCorrection

        [TestMethod]
        public void MagneticCalculations_DepthCorrection_DefaultsToNull()
        {
            var calc = new MagneticCalculations();
            Assert.IsNull(calc.DepthCorrection);
        }

        [TestMethod]
        public void MagneticCalculations_CopyConstructor_CopiesDepthCorrection()
        {
            var depthResult = DepthCorrection.Calculate(Bh, Bv, F, DepthM);
            var original = new MagneticCalculations { DepthCorrection = depthResult };
            var copy = new MagneticCalculations(original);

            Assert.IsNotNull(copy.DepthCorrection);
            Assert.AreEqual(original.DepthCorrection.DipoleScalingFactor, copy.DepthCorrection.DipoleScalingFactor);
        }

        #endregion

        #region GeomagneticUncertainty.DepthAzimuthUncertainty

        [TestMethod]
        public void GeomagneticUncertainty_DepthAzimuthUncertainty_DefaultsToNull()
        {
            var unc = new GeomagneticUncertainty();
            Assert.IsNull(unc.DepthAzimuthUncertainty);
        }

        [TestMethod]
        public void GeomagneticUncertainty_ScaleTo_ScalesDepthAzimuthUncertainty()
        {
            var unc = new GeomagneticUncertainty { DepthAzimuthUncertainty = 0.38 };
            var scaled = unc.ScaleTo(2.0);
            Assert.AreEqual(0.76, scaled.DepthAzimuthUncertainty.Value, 1e-10);
        }

        [TestMethod]
        public void GeomagneticUncertainty_ScaleTo_NullDepthAzimuth_StaysNull()
        {
            var unc = new GeomagneticUncertainty { DepthAzimuthUncertainty = null };
            var scaled = unc.ScaleTo(2.0);
            Assert.IsNull(scaled.DepthAzimuthUncertainty);
        }

        #endregion
    }
}
