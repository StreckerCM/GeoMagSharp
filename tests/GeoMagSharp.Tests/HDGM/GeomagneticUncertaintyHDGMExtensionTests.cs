/****************************************************************************
 * File:            GeomagneticUncertaintyHDGMExtensionTests.cs
 * Description:     Tests for the per-component / per-point uncertainty fields
 *                  (consolidated under the value-side names in 1.7.2 — see #13)
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
        public void NewInstance_PerComponentFieldsAreNull()
        {
            // Per-component σ fields are nullable so callers can distinguish
            // "ISCWSA didn't provide this" from "value happens to be 0".
            var u = new GeomagneticUncertainty();
            Assert.IsNull(u.HorizontalIntensity);
            Assert.IsNull(u.NorthComp);
            Assert.IsNull(u.EastComp);
            Assert.IsNull(u.VerticalComp);
            Assert.IsNull(u.HighResolutionCoverage);
        }

        [TestMethod]
        public void NewInstance_AlwaysPopulatedFieldsAreZero()
        {
            // Declination/Inclination/TotalField are non-nullable — every
            // uncertainty source provides them. They default to 0 on a fresh
            // instance (consumers should consult Source to know provenance).
            var u = new GeomagneticUncertainty();
            Assert.AreEqual(0.0, u.Declination);
            Assert.AreEqual(0.0, u.Inclination);
            Assert.AreEqual(0.0, u.TotalField);
        }

        [TestMethod]
        public void SetPerComponent_RoundTrips()
        {
            var u = new GeomagneticUncertainty
            {
                Source = UncertaintySource.Hdgm,
                Declination = 0.123,
                Inclination = 0.16,
                TotalField = 107,
                HorizontalIntensity = 100,
                NorthComp = 50,
                EastComp = 60,
                VerticalComp = 70
            };
            Assert.AreEqual(UncertaintySource.Hdgm, u.Source);
            Assert.AreEqual(0.123, u.Declination);
            Assert.AreEqual(100.0, u.HorizontalIntensity);
            Assert.AreEqual(50.0, u.NorthComp);
        }

        [TestMethod]
        public void SetHighResolutionCoverage_RoundTrips()
        {
            var u = new GeomagneticUncertainty { HighResolutionCoverage = true };
            Assert.AreEqual(true, u.HighResolutionCoverage);
        }

        [TestMethod]
        public void ScaleTo_PropagatesAllFields()
        {
            var u = new GeomagneticUncertainty
            {
                Source = UncertaintySource.Hdgm,
                Declination = 0.1,
                Inclination = 0.2,
                TotalField = 110,
                HorizontalIntensity = 100,
                NorthComp = 50,
                EastComp = 60,
                VerticalComp = 70,
                HighResolutionCoverage = true
            };
            var scaled = u.ScaleTo(2.0);
            Assert.AreEqual(UncertaintySource.Hdgm, scaled.Source);
            Assert.AreEqual(0.2, scaled.Declination);
            Assert.AreEqual(0.4, scaled.Inclination);
            Assert.AreEqual(220, scaled.TotalField);
            Assert.AreEqual(200.0, scaled.HorizontalIntensity);
            Assert.AreEqual(100.0, scaled.NorthComp);
            Assert.AreEqual(120.0, scaled.EastComp);
            Assert.AreEqual(140.0, scaled.VerticalComp);
            Assert.AreEqual(true, scaled.HighResolutionCoverage, "boolean flag is not scaled, just propagated");
        }

        [TestMethod]
        public void ScaleTo_NullPerComponentFieldsRemainNull()
        {
            // ISCWSA case: per-component fields stay null after scaling.
            var u = new GeomagneticUncertainty
            {
                Source = UncertaintySource.Iscwsa,
                Declination = 0.36,
                Inclination = 0.24,
                TotalField = 157
            };
            var scaled = u.ScaleTo(2.0);
            Assert.IsNull(scaled.HorizontalIntensity);
            Assert.IsNull(scaled.NorthComp);
            Assert.IsNull(scaled.EastComp);
            Assert.IsNull(scaled.VerticalComp);
            Assert.IsNull(scaled.HighResolutionCoverage);
        }

        [TestMethod]
        public void ScaleTo_ZeroFactor_AllValuesAreZero()
        {
            var u = new GeomagneticUncertainty
            {
                Declination = 0.5, Inclination = 0.5, TotalField = 110,
                HorizontalIntensity = 100, NorthComp = 50, EastComp = 60, VerticalComp = 70
            };
            var scaled = u.ScaleTo(0.0);
            Assert.AreEqual(0.0, scaled.Declination);
            Assert.AreEqual(0.0, scaled.HorizontalIntensity);
            Assert.AreEqual(0.0, scaled.NorthComp);
        }

        [TestMethod]
        public void ScaleTo_NegativeFactor_ProducesNegativeValues()
        {
            // Documented behavior: scaling is a linear multiply; negative scale
            // yields negative values, even though σ is conceptually non-negative.
            var u = new GeomagneticUncertainty { Declination = 0.5, HorizontalIntensity = 100 };
            var scaled = u.ScaleTo(-1.0);
            Assert.AreEqual(-0.5, scaled.Declination);
            Assert.AreEqual(-100.0, scaled.HorizontalIntensity);
        }

        [TestMethod]
        public void DipAngle_Obsolete_ForwardsToInclination()
        {
            // Bridge for legacy callers: setting DipAngle stores in Inclination,
            // and reading DipAngle returns Inclination's value.
            var u = new GeomagneticUncertainty { Inclination = 0.42 };
#pragma warning disable CS0618 // intentionally exercising the obsolete alias
            Assert.AreEqual(0.42, u.DipAngle);
            u.DipAngle = 0.55;
#pragma warning restore CS0618
            Assert.AreEqual(0.55, u.Inclination);
        }
    }
}
