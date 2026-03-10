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
    }
}
