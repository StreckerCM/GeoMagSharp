/****************************************************************************
 * File:            UncertaintyUnitTest.cs
 * Description:     Tests for ISCWSA geomagnetic uncertainty estimation
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using GeoMagSharp;
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

        #region GeomagneticUncertainty Tests

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
                Inclination = 0.24,
                Revision = "Rev5.13"
            };

            // Act
            var scaled = uncertainty.ScaleTo(2.0);

            // Assert
            Assert.AreEqual(0.72, scaled.Declination, 0.001);
            Assert.AreEqual(10000, scaled.BhDependentDec, 0.1);
            Assert.AreEqual(314, scaled.TotalField, 0.1);
            Assert.AreEqual(0.48, scaled.Inclination, 0.001);
            Assert.AreEqual("Rev5.13", scaled.Revision);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.LowResolution, scaled.ModelCategory);
        }

        [TestMethod]
        public void GeomagneticUncertainty_ScaleTo_IdentityFactor_ReturnsSameValues()
        {
            // Arrange
            var uncertainty = new GeoMagSharp.GeomagneticUncertainty
            {
                Declination = 0.36,
                BhDependentDec = 5000,
                TotalField = 157,
                Inclination = 0.24
            };

            // Act
            var scaled = uncertainty.ScaleTo(1.0);

            // Assert
            Assert.AreEqual(0.36, scaled.Declination, 0.0001);
            Assert.AreEqual(5000, scaled.BhDependentDec, 0.0001);
            Assert.AreEqual(157, scaled.TotalField, 0.0001);
            Assert.AreEqual(0.24, scaled.Inclination, 0.0001);
        }

        [TestMethod]
        public void GeomagneticUncertainty_ScaleTo_ZeroFactor_ReturnsZeroValues()
        {
            // Arrange
            var uncertainty = new GeoMagSharp.GeomagneticUncertainty
            {
                Declination = 0.36,
                BhDependentDec = 5000,
                TotalField = 157,
                Inclination = 0.24
            };

            // Act
            var scaled = uncertainty.ScaleTo(0.0);

            // Assert
            Assert.AreEqual(0.0, scaled.Declination, 0.0001);
            Assert.AreEqual(0.0, scaled.BhDependentDec, 0.0001);
            Assert.AreEqual(0.0, scaled.TotalField, 0.0001);
            Assert.AreEqual(0.0, scaled.Inclination, 0.0001);
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
                Inclination = 0.24,
                Revision = "Rev5.13"
            };

            // Act
            var _ = original.ScaleTo(3.0);

            // Assert — original unchanged
            Assert.AreEqual(0.36, original.Declination, 0.001);
            Assert.AreEqual(5000, original.BhDependentDec, 0.1);
        }

        #endregion

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
            Assert.AreEqual(0.24, result.Inclination, 0.001);
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
            Assert.AreEqual(0.16, result.Inclination, 0.001);
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
            Assert.AreEqual(0.20, result.Inclination, 0.001);
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
            Assert.AreEqual(0.10, result.Inclination, 0.001);
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
            Assert.AreEqual(0.10, result.Inclination, 0.001);
        }

        [TestMethod]
        public void GetUncertainty_WMMHR_ReturnsHighResolution()
        {
            // WMMHR should auto-detect to HighResolution through the full pipeline
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.WMMHR, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.HighResolution, result.ModelCategory);
            Assert.AreEqual(0.30, result.Declination, 0.001);
            Assert.AreEqual(4118, result.BhDependentDec, 0.1);
        }

        [TestMethod]
        public void GetUncertainty_AllCategories_HaveRevision()
        {
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
                Assert.AreEqual("Rev5.13", result.Revision, $"Category {cat} has wrong Revision");
            }
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
                Inclination = 0.24,
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

        #region Integration Tests

        private static string FindWMM2025Path()
        {
            var possiblePaths = new[]
            {
                System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "TestData"),
                System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", "..", "TestData"),
                System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "tests", "GeoMagSharp.Tests", "TestData"),
                @"C:\GitHub\GeoMagSharp\tests\GeoMagSharp.Tests\TestData"
            };

            foreach (var path in possiblePaths)
            {
                var candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(path, "WMM2025.COF"));
                if (System.IO.File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        [TestMethod]
        public void Integration_WMMCalculation_DefaultAuto_UsesWmmErrorModel()
        {
            // Arrange — load WMM2025 with default Auto preference → WMM error model
            var filePath = FindWMM2025Path();
            if (filePath == null)
                Assert.Inconclusive("WMM2025.COF not found in TestData folder");

            var geoMag = new GeoMagSharp.GeoMag();
            geoMag.LoadModel(filePath);

            var options = new GeoMagSharp.CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -105.0,
                StartDate = new System.DateTime(2025, 1, 1)
            };
            options.SetElevation(0, GeoMagSharp.Distance.Unit.meter);

            // Act
            geoMag.MagneticCalculations(options);

            // Assert — with Auto, WMM now uses WMM error model
            Assert.IsTrue(geoMag.ResultsOfCalculation.Count > 0);
            var result = geoMag.ResultsOfCalculation[0];
            Assert.IsNotNull(result.Uncertainty);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.WmmErrorModel, result.Uncertainty.Source);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.LowResolution, result.Uncertainty.ModelCategory);
            Assert.AreEqual(138, result.Uncertainty.TotalField, 0.1);
            Assert.AreEqual(0.20, result.Uncertainty.Inclination, 0.001);
            Assert.AreEqual(137, result.Uncertainty.NorthIntensity.Value, 0.1);
        }

        [TestMethod]
        public void Integration_WMMCalculation_WithIFROverride_HasIFR1Uncertainty()
        {
            // Arrange — load WMM but override to IFR1
            var filePath = FindWMM2025Path();
            if (filePath == null)
                Assert.Inconclusive("WMM2025.COF not found in TestData folder");

            var geoMag = new GeoMagSharp.GeoMag();
            geoMag.LoadModel(filePath);

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

        [TestMethod]
        public void Integration_DateRange_AllResultsHaveUncertainty()
        {
            // Arrange — date range with 3 steps to verify uncertainty is attached to all results
            var filePath = FindWMM2025Path();
            if (filePath == null)
                Assert.Inconclusive("WMM2025.COF not found in TestData folder");

            var geoMag = new GeoMagSharp.GeoMag();
            geoMag.LoadModel(filePath);

            var options = new GeoMagSharp.CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -105.0,
                StartDate = new System.DateTime(2025, 1, 1),
                EndDate = new System.DateTime(2025, 3, 1),
                StepInterval = 30
            };
            options.SetElevation(0, GeoMagSharp.Distance.Unit.meter);

            // Act
            geoMag.MagneticCalculations(options);

            // Assert — all results should have uncertainty, not just the first
            Assert.IsTrue(geoMag.ResultsOfCalculation.Count >= 2, "Expected at least 2 results for date range");
            foreach (var result in geoMag.ResultsOfCalculation)
            {
                Assert.IsNotNull(result.Uncertainty, $"Result for {result.Date:yyyy-MM-dd} has null Uncertainty");
                Assert.AreEqual(GeoMagSharp.UncertaintySource.WmmErrorModel, result.Uncertainty.Source);
            }
        }

        #endregion

        #region WMM Error Model Tests

        [TestMethod]
        public void WmmErrorModel_WMM2025_Constants_MatchTechnicalReport()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetWmmUncertainty(
                GeoMagSharp.knownModels.WMM, 20000);

            Assert.IsNotNull(result);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.WmmErrorModel, result.Source);
            Assert.AreEqual(137, result.NorthIntensity.Value, 0.1);
            Assert.AreEqual(89, result.EastIntensity.Value, 0.1);
            Assert.AreEqual(141, result.VerticalIntensity.Value, 0.1);
            Assert.AreEqual(133, result.HorizontalIntensity.Value, 0.1);
            Assert.AreEqual(138, result.TotalField, 0.1);
            Assert.AreEqual(0.20, result.Inclination, 0.001);
        }

        [TestMethod]
        public void WmmErrorModel_WMMHR2025_Constants_MatchTechnicalReport()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetWmmUncertainty(
                GeoMagSharp.knownModels.WMMHR, 20000);

            Assert.IsNotNull(result);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.WmmErrorModel, result.Source);
            Assert.AreEqual(135, result.NorthIntensity.Value, 0.1);
            Assert.AreEqual(85, result.EastIntensity.Value, 0.1);
            Assert.AreEqual(134, result.VerticalIntensity.Value, 0.1);
            Assert.AreEqual(130, result.HorizontalIntensity.Value, 0.1);
            Assert.AreEqual(134, result.TotalField, 0.1);
            Assert.AreEqual(0.19, result.Inclination, 0.001);
        }

        [TestMethod]
        public void WmmErrorModel_DeclinationUncertainty_OhioLocation()
        {
            // WMMHR2025 at H ≈ 19,982 nT: δD = √(0.25² + (5205/19982)²) ≈ 0.361°
            var result = GeoMagSharp.UncertaintyDataProvider.GetWmmUncertainty(
                GeoMagSharp.knownModels.WMMHR, 19982);

            Assert.AreEqual(0.361, result.Declination, 0.002);
        }

        [TestMethod]
        public void WmmErrorModel_DeclinationUncertainty_HighH_ApproachesBase()
        {
            // At very high H (equatorial max ~41875 nT), δD approaches but doesn't equal C₁
            // δD = √(0.25² + (5205/41875)²) = √(0.0625 + 0.01546) ≈ 0.279°
            var result = GeoMagSharp.UncertaintyDataProvider.GetWmmUncertainty(
                GeoMagSharp.knownModels.WMMHR, 41875);

            Assert.AreEqual(0.279, result.Declination, 0.005);
        }

        [TestMethod]
        public void WmmErrorModel_DeclinationUncertainty_LowH_Increases()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetWmmUncertainty(
                GeoMagSharp.knownModels.WMMHR, 2000);

            Assert.IsTrue(result.Declination > 2.0, "δD should be large near magnetic poles");
        }

        [TestMethod]
        public void WmmErrorModel_DeclinationUncertainty_ZeroH_Returns999()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetWmmUncertainty(
                GeoMagSharp.knownModels.WMMHR, 0);

            Assert.AreEqual(999.0, result.Declination, 0.1);
        }

        [TestMethod]
        public void WmmErrorModel_BhDependentDec_IsZero()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetWmmUncertainty(
                GeoMagSharp.knownModels.WMM, 20000);

            Assert.AreEqual(0, result.BhDependentDec, 0.001);
        }

        [TestMethod]
        [ExpectedException(typeof(System.InvalidOperationException))]
        public void WmmErrorModel_IGRF_ThrowsInvalidOperation()
        {
            GeoMagSharp.UncertaintyDataProvider.GetWmmUncertainty(
                GeoMagSharp.knownModels.IGRF, 20000);
        }

        [TestMethod]
        public void GetUncertainty_Auto_WMM_ReturnsWmmSource()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.WMM, null,
                GeoMagSharp.UncertaintyModelPreference.Auto, 20000);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.WmmErrorModel, result.Source);
        }

        [TestMethod]
        public void GetUncertainty_Auto_WMMHR_ReturnsWmmSource()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.WMMHR, null,
                GeoMagSharp.UncertaintyModelPreference.Auto, 20000);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.WmmErrorModel, result.Source);
        }

        [TestMethod]
        public void GetUncertainty_Auto_IGRF_ReturnsIscwsaSource()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.IGRF, null,
                GeoMagSharp.UncertaintyModelPreference.Auto, 20000);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.Iscwsa, result.Source);
        }

        [TestMethod]
        public void GetUncertainty_Iscwsa_WMM_ReturnsIscwsaSource()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.WMM, null,
                GeoMagSharp.UncertaintyModelPreference.Iscwsa, 20000);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.Iscwsa, result.Source);
        }

        [TestMethod]
        [ExpectedException(typeof(System.InvalidOperationException))]
        public void GetUncertainty_Native_IGRF_Throws()
        {
            GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.IGRF, null,
                GeoMagSharp.UncertaintyModelPreference.Native, 20000);
        }

        [TestMethod]
        public void WmmErrorModel_ScaleTo_ScalesAllProperties()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetWmmUncertainty(
                GeoMagSharp.knownModels.WMMHR, 20000);

            var scaled = result.ScaleTo(2.0);

            Assert.AreEqual(result.Declination * 2.0, scaled.Declination, 0.001);
            Assert.AreEqual(result.TotalField * 2.0, scaled.TotalField, 0.1);
            Assert.AreEqual(result.NorthIntensity.Value * 2.0, scaled.NorthIntensity.Value, 0.1);
            Assert.AreEqual(result.EastIntensity.Value * 2.0, scaled.EastIntensity.Value, 0.1);
            Assert.AreEqual(result.VerticalIntensity.Value * 2.0, scaled.VerticalIntensity.Value, 0.1);
            Assert.AreEqual(result.HorizontalIntensity.Value * 2.0, scaled.HorizontalIntensity.Value, 0.1);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.WmmErrorModel, scaled.Source);
        }

        #endregion

        #region WMM Error Model Integration Tests

        [TestMethod]
        public void Integration_WMMCalculation_AutoPreference_UsesWmmErrorModel()
        {
            var filePath = FindWMM2025Path();
            if (filePath == null)
                Assert.Inconclusive("WMM2025.COF not found in TestData folder");

            var geoMag = new GeoMagSharp.GeoMag();
            geoMag.LoadModel(filePath);

            var options = new GeoMagSharp.CalculationOptions
            {
                Latitude = 41.31,
                Longitude = -81.33,
                StartDate = new System.DateTime(2026, 3, 11)
            };
            options.SetElevation(0, GeoMagSharp.Distance.Unit.meter);

            geoMag.MagneticCalculations(options);

            var result = geoMag.ResultsOfCalculation[0];
            Assert.IsNotNull(result.Uncertainty);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.WmmErrorModel, result.Uncertainty.Source);
            Assert.AreEqual(137, result.Uncertainty.NorthIntensity.Value, 0.1);
            Assert.AreEqual(89, result.Uncertainty.EastIntensity.Value, 0.1);
            Assert.AreEqual(141, result.Uncertainty.VerticalIntensity.Value, 0.1);
            Assert.AreEqual(133, result.Uncertainty.HorizontalIntensity.Value, 0.1);
            Assert.AreEqual(138, result.Uncertainty.TotalField, 0.1);
            Assert.AreEqual(0.20, result.Uncertainty.Inclination, 0.001);
        }

        [TestMethod]
        public void Integration_WMMCalculation_AutoPreference_DeclinationMatchesNOAA()
        {
            // Verify δD at Ohio location: WMM2025 δD = √(0.26² + (5417/H)²)
            var filePath = FindWMM2025Path();
            if (filePath == null)
                Assert.Inconclusive("WMM2025.COF not found in TestData folder");

            var geoMag = new GeoMagSharp.GeoMag();
            geoMag.LoadModel(filePath);

            var options = new GeoMagSharp.CalculationOptions
            {
                Latitude = 41.31,
                Longitude = -81.33,
                StartDate = new System.DateTime(2026, 3, 11)
            };
            options.SetElevation(0, GeoMagSharp.Distance.Unit.meter);

            geoMag.MagneticCalculations(options);

            var result = geoMag.ResultsOfCalculation[0];
            // WMM2025: δD = √(0.26² + (5417/H)²) where H ≈ 19,982 nT
            // = √(0.0676 + 0.0735) = √(0.1411) ≈ 0.376°
            Assert.AreEqual(0.376, result.Uncertainty.Declination, 0.02);
        }

        [TestMethod]
        public void Integration_WMMCalculation_IscwsaOverride_PreservesIscwsaBehavior()
        {
            var filePath = FindWMM2025Path();
            if (filePath == null)
                Assert.Inconclusive("WMM2025.COF not found in TestData folder");

            var geoMag = new GeoMagSharp.GeoMag();
            geoMag.LoadModel(filePath);

            var options = new GeoMagSharp.CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -105.0,
                StartDate = new System.DateTime(2025, 1, 1),
                UncertaintyModel = GeoMagSharp.UncertaintyModelPreference.Iscwsa
            };
            options.SetElevation(0, GeoMagSharp.Distance.Unit.meter);

            geoMag.MagneticCalculations(options);

            var result = geoMag.ResultsOfCalculation[0];
            Assert.IsNotNull(result.Uncertainty);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.Iscwsa, result.Uncertainty.Source);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.LowResolution, result.Uncertainty.ModelCategory);
            Assert.AreEqual(0.36, result.Uncertainty.Declination, 0.001);
            Assert.AreEqual(5000, result.Uncertainty.BhDependentDec, 0.1);
            Assert.AreEqual(157, result.Uncertainty.TotalField, 0.1);
            Assert.AreEqual(0.24, result.Uncertainty.Inclination, 0.001);
        }

        [TestMethod]
        [ExpectedException(typeof(System.ArgumentOutOfRangeException))]
        public void ComputeDeclinationUncertainty_NegativeH_Throws()
        {
            GeoMagSharp.UncertaintyDataProvider.ComputeDeclinationUncertainty(0.26, 5417, -1.0);
        }

        [TestMethod]
        [ExpectedException(typeof(System.ArgumentOutOfRangeException))]
        public void ComputeDeclinationUncertainty_NaN_Throws()
        {
            GeoMagSharp.UncertaintyDataProvider.ComputeDeclinationUncertainty(0.26, 5417, double.NaN);
        }

        [TestMethod]
        [ExpectedException(typeof(System.ArgumentOutOfRangeException))]
        public void ComputeDeclinationUncertainty_PositiveInfinity_Throws()
        {
            GeoMagSharp.UncertaintyDataProvider.ComputeDeclinationUncertainty(0.26, 5417, double.PositiveInfinity);
        }

        [TestMethod]
        public void ComputeDeclinationUncertainty_ZeroH_Returns999()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.ComputeDeclinationUncertainty(0.26, 5417, 0.0);
            Assert.AreEqual(999.0, result, 0.001);
        }

        [TestMethod]
        public void ScaleTo_PreservesNullNullableProperties()
        {
            // ISCWSA source has null NorthIntensity, EastIntensity, etc.
            var iscwsa = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.WMM, null);

            Assert.IsNull(iscwsa.NorthIntensity);

            var scaled = iscwsa.ScaleTo(2.0);
            Assert.IsNull(scaled.NorthIntensity);
            Assert.IsNull(scaled.EastIntensity);
            Assert.IsNull(scaled.VerticalIntensity);
            Assert.IsNull(scaled.HorizontalIntensity);
        }

        [TestMethod]
        public void ScaleTo_ScalesNonNullNullableProperties()
        {
            // WMM source has non-null component properties
            var wmm = GeoMagSharp.UncertaintyDataProvider.GetWmmUncertainty(
                GeoMagSharp.knownModels.WMM, 20000);

            Assert.IsNotNull(wmm.NorthIntensity);

            var scaled = wmm.ScaleTo(2.0);
            Assert.AreEqual(wmm.NorthIntensity.Value * 2.0, scaled.NorthIntensity.Value, 0.1);
            Assert.AreEqual(wmm.HorizontalIntensity.Value * 2.0, scaled.HorizontalIntensity.Value, 0.1);
        }

        #endregion

        #region Backward Compatibility Tests

        [TestMethod]
        public void CalculationOptions_UncertaintyModel_DefaultsToAuto()
        {
            var options = new GeoMagSharp.CalculationOptions();
            Assert.AreEqual(GeoMagSharp.UncertaintyModelPreference.Auto, options.UncertaintyModel);
        }

        [TestMethod]
        public void CalculationOptions_CopyConstructor_CopiesUncertaintyModel()
        {
            var original = new GeoMagSharp.CalculationOptions
            {
                UncertaintyModel = GeoMagSharp.UncertaintyModelPreference.Iscwsa
            };
            var copy = new GeoMagSharp.CalculationOptions(original);
            Assert.AreEqual(GeoMagSharp.UncertaintyModelPreference.Iscwsa, copy.UncertaintyModel);
        }

        [TestMethod]
        public void GetUncertainty_TwoParamOverload_AlwaysReturnsIscwsa()
        {
            var result = GeoMagSharp.UncertaintyDataProvider.GetUncertainty(
                GeoMagSharp.knownModels.WMM, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.Iscwsa, result.Source);
            Assert.AreEqual(0.36, result.Declination, 0.001);
        }

        [TestMethod]
        public void Integration_WMMCalculation_CategoryOverride_ForcesIscwsa()
        {
            // When ModelCategoryOverride is set, it should force ISCWSA even with Auto
            var filePath = FindWMM2025Path();
            if (filePath == null)
                Assert.Inconclusive("WMM2025.COF not found in TestData folder");

            var geoMag = new GeoMagSharp.GeoMag();
            geoMag.LoadModel(filePath);

            var options = new GeoMagSharp.CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -105.0,
                StartDate = new System.DateTime(2025, 1, 1),
                ModelCategoryOverride = GeoMagSharp.GeomagneticModelCategory.InFieldReference1
            };
            options.SetElevation(0, GeoMagSharp.Distance.Unit.meter);

            geoMag.MagneticCalculations(options);

            var result = geoMag.ResultsOfCalculation[0];
            Assert.IsNotNull(result.Uncertainty);
            Assert.AreEqual(GeoMagSharp.UncertaintySource.Iscwsa, result.Uncertainty.Source);
            Assert.AreEqual(GeoMagSharp.GeomagneticModelCategory.InFieldReference1, result.Uncertainty.ModelCategory);
            Assert.AreEqual(0.15, result.Uncertainty.Declination, 0.001);
        }

        #endregion
    }
}
