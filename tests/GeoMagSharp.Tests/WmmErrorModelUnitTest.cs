/****************************************************************************
 * File:            WmmErrorModelUnitTest.cs
 * Description:     Unit tests for the WMM native error model — formula,
 *                  boundary cases, dispatch logic, and value-table integrity.
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using GeoMagSharp;

namespace GeoMagSharp_UnitTests
{
    [TestClass]
    public class WmmErrorModelUnitTest
    {
        // ─── ComputeDeclinationUncertainty: δD = √(C₁² + (C₂/H)²) ───

        [TestMethod]
        public void ComputeDeclination_LargeH_ApproachesC1()
        {
            // At very large H, the (C₂/H)² term vanishes and δD → C₁.
            double dD = UncertaintyDataProvider.ComputeDeclinationUncertainty(
                declinationBase: 0.26, declinationCoeff: 5417, horizontalIntensity: 1_000_000);
            Assert.AreEqual(0.26, dD, 0.0001);
        }

        [TestMethod]
        public void ComputeDeclination_TypicalMidLatitudeH_MatchesExpected()
        {
            // H ≈ 25,000 nT (typical mid-latitudes). Spec: √(0.26² + (5417/25000)²) ≈ 0.339°.
            double dD = UncertaintyDataProvider.ComputeDeclinationUncertainty(
                declinationBase: 0.26, declinationCoeff: 5417, horizontalIntensity: 25000);
            double expected = Math.Sqrt(0.26 * 0.26 + (5417.0 / 25000.0) * (5417.0 / 25000.0));
            Assert.AreEqual(expected, dD, 1e-9);
        }

        [TestMethod]
        public void ComputeDeclination_ZeroH_ReturnsSentinel999()
        {
            // At the magnetic dip pole H → 0; declination is mathematically undefined.
            // Sentinel 999.0 lets consumers detect this case and surface it appropriately.
            double dD = UncertaintyDataProvider.ComputeDeclinationUncertainty(
                declinationBase: 0.26, declinationCoeff: 5417, horizontalIntensity: 0.0);
            Assert.AreEqual(999.0, dD);
        }

        [DataTestMethod]
        [DataRow(double.NaN)]
        [DataRow(double.PositiveInfinity)]
        [DataRow(double.NegativeInfinity)]
        [DataRow(-1.0)]
        public void ComputeDeclination_InvalidH_Throws(double horizontalIntensity)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                UncertaintyDataProvider.ComputeDeclinationUncertainty(0.26, 5417, horizontalIntensity));
        }

        // ─── ShouldUseWmmErrorModel: dispatch logic ───

        [DataTestMethod]
        [DataRow(knownModels.WMM,   UncertaintyModelPreference.Auto,    true)]
        [DataRow(knownModels.WMMHR, UncertaintyModelPreference.Auto,    true)]
        [DataRow(knownModels.IGRF,  UncertaintyModelPreference.Auto,    false)]
        [DataRow(knownModels.HDGM,  UncertaintyModelPreference.Auto,    false)]
        [DataRow(knownModels.WMM,   UncertaintyModelPreference.Iscwsa,  false)]
        [DataRow(knownModels.WMMHR, UncertaintyModelPreference.Iscwsa,  false)]
        [DataRow(knownModels.WMM,   UncertaintyModelPreference.Native,  true)]
        [DataRow(knownModels.WMMHR, UncertaintyModelPreference.Native,  true)]
        public void ShouldUseWmmErrorModel_DispatchTable(knownModels model, UncertaintyModelPreference pref, bool expected)
        {
            Assert.AreEqual(expected, UncertaintyDataProvider.ShouldUseWmmErrorModel(model, pref));
        }

        [TestMethod]
        public void ShouldUseWmmErrorModel_NativeOnNonWmmModel_Throws()
        {
            // Native is strict: requesting it on a model that has no native
            // error model is a programmer error, not a silent fallback.
            Assert.ThrowsException<InvalidOperationException>(() =>
                UncertaintyDataProvider.ShouldUseWmmErrorModel(knownModels.IGRF, UncertaintyModelPreference.Native));
            Assert.ThrowsException<InvalidOperationException>(() =>
                UncertaintyDataProvider.ShouldUseWmmErrorModel(knownModels.HDGM, UncertaintyModelPreference.Native));
        }

        // ─── GetWmmUncertainty: end-to-end value table ───

        [TestMethod]
        public void GetWmmUncertainty_Wmm2025_PopulatesAllSevenFields()
        {
            // Mid-latitude H ≈ 25,000 nT. Constants from WMM2025-2030 Tech Report Section 3.4:
            // δX=137, δY=89, δZ=141, δH=133, δF=138, δI=0.20, C₁=0.26, C₂=5417.
            var u = UncertaintyDataProvider.GetWmmUncertainty(knownModels.WMM, 25000.0);

            Assert.AreEqual(UncertaintySource.WmmErrorModel, u.Source);
            Assert.AreEqual("WMM2025-TR", u.Revision);
            Assert.AreEqual(137, u.NorthComp.Value, 0.1);
            Assert.AreEqual(89,  u.EastComp.Value,  0.1);
            Assert.AreEqual(141, u.VerticalComp.Value, 0.1);
            Assert.AreEqual(133, u.HorizontalIntensity.Value, 0.1);
            Assert.AreEqual(138, u.TotalField, 0.1);
            Assert.AreEqual(0.20, u.Inclination, 0.001);
            Assert.AreEqual(0, u.BhDependentDec);
            // Declination is computed, location-dependent:
            double expectedD = Math.Sqrt(0.26 * 0.26 + (5417.0 / 25000.0) * (5417.0 / 25000.0));
            Assert.AreEqual(expectedD, u.Declination, 1e-9);
        }

        [TestMethod]
        public void GetWmmUncertainty_Wmmhr2025_PopulatesAllSevenFields()
        {
            // WMMHR has tighter constants than WMM. Tech Report Section 3.4:
            // δX=135, δY=85, δZ=134, δH=130, δF=134, δI=0.19, C₁=0.25, C₂=5205.
            var u = UncertaintyDataProvider.GetWmmUncertainty(knownModels.WMMHR, 25000.0);

            Assert.AreEqual(UncertaintySource.WmmErrorModel, u.Source);
            Assert.AreEqual("WMMHR2025-TR", u.Revision);
            Assert.AreEqual(135, u.NorthComp.Value, 0.1);
            Assert.AreEqual(85,  u.EastComp.Value,  0.1);
            Assert.AreEqual(134, u.VerticalComp.Value, 0.1);
            Assert.AreEqual(130, u.HorizontalIntensity.Value, 0.1);
            Assert.AreEqual(134, u.TotalField, 0.1);
            Assert.AreEqual(0.19, u.Inclination, 0.001);
            Assert.AreEqual(0, u.BhDependentDec);
        }

        [TestMethod]
        public void GetWmmUncertainty_NonWmmModel_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                UncertaintyDataProvider.GetWmmUncertainty(knownModels.IGRF, 25000.0));
        }

        [TestMethod]
        public void GetWmmUncertainty_AtZeroH_DeclinationIsSentinel()
        {
            // Magnetic dip pole — declination undefined.
            var u = UncertaintyDataProvider.GetWmmUncertainty(knownModels.WMM, 0.0);
            Assert.AreEqual(999.0, u.Declination);
            // Other fields still populated normally:
            Assert.AreEqual(138, u.TotalField, 0.1);
        }

        // ─── 4-arg GetUncertainty: end-to-end dispatch ───

        [TestMethod]
        public void GetUncertainty_AutoOnWmm_UsesWmmModel()
        {
            var u = UncertaintyDataProvider.GetUncertainty(
                knownModels.WMM, null, UncertaintyModelPreference.Auto, 25000.0);
            Assert.AreEqual(UncertaintySource.WmmErrorModel, u.Source);
        }

        [TestMethod]
        public void GetUncertainty_IscwsaForcedOnWmm_UsesIscwsa()
        {
            var u = UncertaintyDataProvider.GetUncertainty(
                knownModels.WMM, null, UncertaintyModelPreference.Iscwsa, 25000.0);
            Assert.AreEqual(UncertaintySource.Iscwsa, u.Source);
            Assert.AreEqual(0.36, u.Declination, 0.001);
        }

        [TestMethod]
        public void GetUncertainty_OverrideCategory_AlwaysUsesIscwsa()
        {
            // An explicit ModelCategoryOverride implies "use ISCWSA Level 1 with this category"
            // — the override doesn't carry meaning under the WMM/HDGM error models.
            var u = UncertaintyDataProvider.GetUncertainty(
                knownModels.WMM,
                GeomagneticModelCategory.InFieldReference1,
                UncertaintyModelPreference.Auto,
                25000.0);
            Assert.AreEqual(UncertaintySource.Iscwsa, u.Source);
            Assert.AreEqual(GeomagneticModelCategory.InFieldReference1, u.ModelCategory);
        }

        [TestMethod]
        public void GetUncertainty_AutoOnIgrf_UsesIscwsa()
        {
            var u = UncertaintyDataProvider.GetUncertainty(
                knownModels.IGRF, null, UncertaintyModelPreference.Auto, 25000.0);
            Assert.AreEqual(UncertaintySource.Iscwsa, u.Source);
        }
    }
}
