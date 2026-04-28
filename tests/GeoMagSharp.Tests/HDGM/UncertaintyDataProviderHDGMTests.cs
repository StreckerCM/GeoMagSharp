/****************************************************************************
 * File:            UncertaintyDataProviderHDGMTests.cs
 * Description:     Tests for HDGM case in UncertaintyDataProvider (HRGM-tier values)
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class UncertaintyDataProviderHDGMTests
    {
        [TestMethod]
        public void GetUncertainty_ForHDGMType_ReturnsHRGMTierValues()
        {
            var u = UncertaintyDataProvider.GetUncertainty(knownModels.HDGM, null);
            Assert.IsNotNull(u);
            // ISCWSA HRGM-tier values per openbrain KB#70 / KB#105
            Assert.AreEqual(GeomagneticModelCategory.HighResolution, u.ModelCategory);
            Assert.AreEqual(107.0, u.TotalField, 1e-6, "MFI (TotalField) should be 107 nT for HRGM");
            Assert.AreEqual(0.16, u.DipAngle, 1e-6, "MDI (DipAngle) should be 0.16° for HRGM");
            Assert.AreEqual(0.30, u.Declination, 1e-6, "DEC constant should be 0.30° for HRGM");
            Assert.AreEqual(4118.0, u.BhDependentDec, 1e-6, "DBH should be 4118 deg·nT for HRGM");
        }
    }
}
