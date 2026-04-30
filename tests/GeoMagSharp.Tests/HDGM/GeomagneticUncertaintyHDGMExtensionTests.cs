/****************************************************************************
 * File:            GeomagneticUncertaintyHDGMExtensionTests.cs
 * Description:     Tests for per-point sigma and coverage flag extensions to GeomagneticUncertainty
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class GeomagneticUncertaintyHDGMExtensionTests
    {
        [TestMethod]
        public void NewInstance_AllPerPointSigmasAreNull()
        {
            var u = new GeomagneticUncertainty();
            Assert.IsNull(u.SigmaD);
            Assert.IsNull(u.SigmaI);
            Assert.IsNull(u.SigmaH);
            Assert.IsNull(u.SigmaX);
            Assert.IsNull(u.SigmaY);
            Assert.IsNull(u.SigmaZ);
            Assert.IsNull(u.SigmaF);
        }

        [TestMethod]
        public void NewInstance_HighResolutionCoverageIsNull()
        {
            var u = new GeomagneticUncertainty();
            Assert.IsNull(u.HighResolutionCoverage);
        }

        [TestMethod]
        public void SetSigmaD_RoundTrips()
        {
            var u = new GeomagneticUncertainty { SigmaD = 0.123 };
            Assert.AreEqual(0.123, u.SigmaD);
        }

        [TestMethod]
        public void SetHighResolutionCoverage_RoundTrips()
        {
            var u = new GeomagneticUncertainty { HighResolutionCoverage = true };
            Assert.AreEqual(true, u.HighResolutionCoverage);
        }

        [TestMethod]
        public void ScaleTo_PropagatesPerPointSigmas()
        {
            var u = new GeomagneticUncertainty
            {
                SigmaD = 0.1, SigmaI = 0.2, SigmaH = 100, SigmaX = 50, SigmaY = 60, SigmaZ = 70, SigmaF = 110,
                HighResolutionCoverage = true
            };
            var scaled = u.ScaleTo(2.0);
            Assert.AreEqual(0.2, scaled.SigmaD);
            Assert.AreEqual(0.4, scaled.SigmaI);
            Assert.AreEqual(200, scaled.SigmaH);
            Assert.AreEqual(100, scaled.SigmaX);
            Assert.AreEqual(120, scaled.SigmaY);
            Assert.AreEqual(140, scaled.SigmaZ);
            Assert.AreEqual(220, scaled.SigmaF);
            Assert.AreEqual(true, scaled.HighResolutionCoverage);
        }

        [TestMethod]
        public void ScaleTo_NullSigmasRemainNull()
        {
            var u = new GeomagneticUncertainty();
            var scaled = u.ScaleTo(2.0);
            Assert.IsNull(scaled.SigmaD);
            Assert.IsNull(scaled.HighResolutionCoverage);
        }

        [TestMethod]
        public void ScaleTo_ZeroFactor_AllPerPointSigmasAreZero()
        {
            var u = new GeomagneticUncertainty
            {
                SigmaD = 0.5, SigmaI = 0.5, SigmaH = 100, SigmaX = 50, SigmaY = 60, SigmaZ = 70, SigmaF = 110
            };
            var scaled = u.ScaleTo(0.0);
            Assert.AreEqual(0.0, scaled.SigmaD);
            Assert.AreEqual(0.0, scaled.SigmaI);
            Assert.AreEqual(0.0, scaled.SigmaH);
            Assert.AreEqual(0.0, scaled.SigmaX);
            Assert.AreEqual(0.0, scaled.SigmaY);
            Assert.AreEqual(0.0, scaled.SigmaZ);
            Assert.AreEqual(0.0, scaled.SigmaF);
        }

        [TestMethod]
        public void ScaleTo_NegativeFactor_ProducesNegativeSigmas()
        {
            var u = new GeomagneticUncertainty { SigmaD = 0.5, SigmaH = 100 };
            var scaled = u.ScaleTo(-1.0);
            Assert.AreEqual(-0.5, scaled.SigmaD);
            Assert.AreEqual(-100, scaled.SigmaH);
        }
    }
}
