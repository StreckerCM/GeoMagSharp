using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class KnownModelsHDGMTests
    {
        [TestMethod]
        public void HDGMEnumValue_Equals6()
        {
            Assert.AreEqual(6, (int)knownModels.HDGM);
        }

        [TestMethod]
        public void HDGMEnumValue_DoesNotCollideWithExisting()
        {
            // sanity: other enum values are unchanged
            Assert.AreEqual(0, (int)knownModels.NONE);
            Assert.AreEqual(1, (int)knownModels.DGRF);
            Assert.AreEqual(2, (int)knownModels.EMM);
            Assert.AreEqual(3, (int)knownModels.IGRF);
            Assert.AreEqual(4, (int)knownModels.WMM);
            Assert.AreEqual(5, (int)knownModels.WMMHR);
        }
    }
}
