/****************************************************************************
 * File:            HDGMCalculationAdapterTests.cs
 * Description:     Unit tests for HDGMCalculationAdapter outData index mapping
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class HDGMCalculationAdapterTests
    {
        private CalculationOptions DefaultOpts() => new CalculationOptions
        {
            Latitude = 40.0,
            Longitude = -100.0,
            StartDate = new DateTime(2020, 6, 1)
        };

        private FakeHdgmInvoker FakeReturning(double[] outData)
        {
            return new FakeHdgmInvoker { CannedOutData = outData, CannedReturnValue = 0 };
        }

        private double[] OutDataAllZero() => new double[25];

        // ── Index mapping: field values ─────────────────────────────────

        [TestMethod]
        public void Calculate_MapsDeclination_FromOutData0()
        {
            var data = OutDataAllZero(); data[0] = 12.345;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(12.345, result.Declination.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsInclination_FromOutData1()
        {
            var data = OutDataAllZero(); data[1] = 67.890;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(67.890, result.Inclination.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsTotalField_FromOutData2()
        {
            var data = OutDataAllZero(); data[2] = 53210.5;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(53210.5, result.TotalField.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsHorizontalIntensity_FromOutData3()
        {
            var data = OutDataAllZero(); data[3] = 21000.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(21000.0, result.HorizontalIntensity.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsNorthComp_FromOutData4()
        {
            var data = OutDataAllZero(); data[4] = 19500.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(19500.0, result.NorthComp.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsEastComp_FromOutData5()
        {
            var data = OutDataAllZero(); data[5] = -2000.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(-2000.0, result.EastComp.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsVerticalComp_FromOutData6()
        {
            var data = OutDataAllZero(); data[6] = 48000.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(48000.0, result.VerticalComp.Value, 1e-9);
        }

        // ── Index mapping: secular variations (skip GV at indices 7 and 15) ──

        [TestMethod]
        public void Calculate_MapsDeclinationChangePerYear_FromOutData8_NotOutData7()
        {
            var data = OutDataAllZero();
            data[7] = 999.0;     // Grid Variation — must be ignored
            data[8] = 0.123;     // dD/dt
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(0.123, result.Declination.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsInclinationChangePerYear_FromOutData9()
        {
            var data = OutDataAllZero(); data[9] = -0.05;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(-0.05, result.Inclination.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsTotalFieldChangePerYear_FromOutData10()
        {
            var data = OutDataAllZero(); data[10] = 22.5;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(22.5, result.TotalField.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsHorizontalIntensityChangePerYear_FromOutData11()
        {
            var data = OutDataAllZero(); data[11] = 5.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(5.0, result.HorizontalIntensity.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsNorthCompChangePerYear_FromOutData12()
        {
            var data = OutDataAllZero(); data[12] = 1.5;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(1.5, result.NorthComp.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsEastCompChangePerYear_FromOutData13()
        {
            var data = OutDataAllZero(); data[13] = -0.5;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(-0.5, result.EastComp.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsVerticalCompChangePerYear_FromOutData14()
        {
            var data = OutDataAllZero(); data[14] = 3.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(3.0, result.VerticalComp.ChangePerYear, 1e-9);
        }

        // ── Index mapping: NSD coverage flag and per-point sigma ─────────

        [TestMethod]
        public void Calculate_MapsCoverageFlag_FromOutData16_HighRes_True()
        {
            var data = OutDataAllZero(); data[16] = 0; // 0 = high-res covered
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(true, result.Uncertainty?.HighResolutionCoverage);
        }

        [TestMethod]
        public void Calculate_MapsCoverageFlag_FromOutData16_Fallback_False()
        {
            var data = OutDataAllZero(); data[16] = 1; // 1 = satellite-fallback
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(false, result.Uncertainty?.HighResolutionCoverage);
        }

        [TestMethod]
        public void Calculate_MapsSigmaD_FromOutData17()
        {
            var data = OutDataAllZero(); data[17] = 0.13;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(0.13, result.Uncertainty?.SigmaD ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaI_FromOutData18()
        {
            var data = OutDataAllZero(); data[18] = 0.16;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(0.16, result.Uncertainty?.SigmaI ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaH_FromOutData19()
        {
            var data = OutDataAllZero(); data[19] = 100.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(100.0, result.Uncertainty?.SigmaH ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaX_FromOutData20()
        {
            var data = OutDataAllZero(); data[20] = 50.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(50.0, result.Uncertainty?.SigmaX ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaY_FromOutData21()
        {
            var data = OutDataAllZero(); data[21] = 60.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(60.0, result.Uncertainty?.SigmaY ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaZ_FromOutData22()
        {
            var data = OutDataAllZero(); data[22] = 70.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(70.0, result.Uncertainty?.SigmaZ ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaF_FromOutData23()
        {
            var data = OutDataAllZero(); data[23] = 107.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(107.0, result.Uncertainty?.SigmaF ?? double.NaN, 1e-9);
        }

        // ── Native return-code handling ──────────────────────────────────

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionOutOfRange))]
        public void Calculate_NonZeroNativeStatus_ThrowsOutOfRange()
        {
            var fake = new FakeHdgmInvoker { CannedOutData = new double[25], CannedReturnValue = 1 };
            HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
        }

        // ── Sentinel handling ────────────────────────────────────────────

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionOutOfRange))]
        public void Calculate_SentinelMinus99999_ThrowsOutOfRange()
        {
            var data = OutDataAllZero(); data[0] = -99999;
            var fake = FakeReturning(data);
            HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
        }

        // ── Inputs passed correctly to native ──────────────────────────

        [TestMethod]
        public void Calculate_LatitudePassedToInvoker()
        {
            var fake = FakeReturning(OutDataAllZero());
            var opts = DefaultOpts(); opts.Latitude = 35.5;
            HDGMCalculationAdapter.Calculate(opts, opts.StartDate, fake);
            Assert.AreEqual(35.5, fake.Calls[0].Latitude, 1e-9);
        }

        [TestMethod]
        public void Calculate_LongitudePassedToInvoker()
        {
            var fake = FakeReturning(OutDataAllZero());
            var opts = DefaultOpts(); opts.Longitude = -75.25;
            HDGMCalculationAdapter.Calculate(opts, opts.StartDate, fake);
            Assert.AreEqual(-75.25, fake.Calls[0].Longitude, 1e-9);
        }

        [TestMethod]
        public void Calculate_DepthInMetersPassedToInvoker_FromAltitudeFeet()
        {
            var fake = FakeReturning(OutDataAllZero());
            var opts = DefaultOpts();
            opts.SetElevation(value: 1000, unit: Distance.Unit.foot, isAltitude: true);
            HDGMCalculationAdapter.Calculate(opts, opts.StartDate, fake);
            // 1000 ft altitude → 1000 * 0.3048 m above MSL → -304.8 m depth (negative for altitude)
            Assert.AreEqual(-304.8, fake.Calls[0].DepthMeters, 1e-9);
        }

        [TestMethod]
        public void Calculate_DepthInMetersPassedToInvoker_FromDepthMeters()
        {
            var fake = FakeReturning(OutDataAllZero());
            var opts = DefaultOpts();
            opts.SetElevation(value: 1500, unit: Distance.Unit.meter, isAltitude: false);
            HDGMCalculationAdapter.Calculate(opts, opts.StartDate, fake);
            // 1500 m depth → +1500 m
            Assert.AreEqual(1500.0, fake.Calls[0].DepthMeters, 1e-9);
        }

        [TestMethod]
        public void Calculate_DateConvertedToDecimalYear()
        {
            var fake = FakeReturning(OutDataAllZero());
            var opts = DefaultOpts();
            var date = new DateTime(2020, 7, 1); // mid-year, decimal year ≈ 2020.4986
            HDGMCalculationAdapter.Calculate(opts, date, fake);
            Assert.IsTrue(fake.Calls[0].DecimalYear > 2020.49 && fake.Calls[0].DecimalYear < 2020.51,
                $"DecimalYear={fake.Calls[0].DecimalYear} not in expected range");
        }
    }
}
