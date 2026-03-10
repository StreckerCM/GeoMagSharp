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
    }
}
