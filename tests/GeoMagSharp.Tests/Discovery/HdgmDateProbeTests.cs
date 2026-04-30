/****************************************************************************
 * File:            HdgmDateProbeTests.cs
 * Description:     Unit tests for HdgmDateProbe via FakeHdgmInvoker
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.Discovery;
using GeoMagSharp.HDGM;
using GeoMagSharp_UnitTests.HDGM;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class HdgmDateProbeTests
    {
        [TestMethod]
        public void ExtractYearFromFilename_StandardNoaaName_Returns2019()
        {
            Assert.AreEqual(2019, HdgmDateProbe.ExtractYearFromFilename("hdgm2019-64.dll"));
        }

        [TestMethod]
        public void ExtractYearFromFilename_BitnessSuffixOnly_ReturnsNull()
        {
            // "myhdgm-64" — only "64" present, not a year-like 19xx/20xx token
            Assert.IsNull(HdgmDateProbe.ExtractYearFromFilename("myhdgm-64.dll"));
        }

        [TestMethod]
        public void ExtractYearFromFilename_NoYear_ReturnsNull()
        {
            Assert.IsNull(HdgmDateProbe.ExtractYearFromFilename("halliburton_hdgm.dll"));
        }

        [TestMethod]
        public void ExtractYearFromFilename_VendorPrefixWithYear_ReturnsYear()
        {
            Assert.AreEqual(2024, HdgmDateProbe.ExtractYearFromFilename("halliburton_hdgm2024.dll"));
        }

        [TestMethod]
        public void Probe_FakeInvokerAlwaysSentinel_ReturnsNullDates()
        {
            var fake = new FakeHdgmInvoker();
            // CannedOutData defaults to all zeros; mark outData[0] = -99999 (sentinel) for every call
            fake.CannedOutData = new double[25];
            fake.CannedOutData[0] = -99999.0;

            var (min, max) = HdgmDateProbe.Probe(_ => fake, "hdgm2019-64.dll");
            Assert.IsNull(min);
            Assert.IsNull(max);
        }

        [TestMethod]
        public void Probe_FakeInvokerValidUntilYearN_ReturnsCorrectMaxDate()
        {
            // Returns valid (non-sentinel) for years 2019-2020 then sentinel
            int callCount = 0;
            var fake = new FakeHdgmInvoker();
            // Override Calculate to vary by call
            // Since FakeHdgmInvoker is a real class, we extend behaviour via a side helper
            var probingFake = new ProbingFake(year =>
            {
                callCount++;
                return year <= 2020 ? 0.0 : -99999.0;
            });

            var (min, max) = HdgmDateProbe.Probe(_ => probingFake, "hdgm2019-64.dll");
            Assert.AreEqual(1900.0, min);
            Assert.AreEqual(2021.0, max);  // exclusive upper: last valid (2020) + 1
        }

        // Test-only invoker that returns a different value depending on the date probed.
        private class ProbingFake : INativeHdgmInvoker
        {
            private readonly Func<int, double> _outData0ForYear;
            public ProbingFake(Func<int, double> outData0ForYear)
            {
                _outData0ForYear = outData0ForYear;
            }
            public int Calculate(double latitude, double longitude, double depthMeters,
                double decimalYear, double[] outData)
            {
                outData[0] = _outData0ForYear((int)decimalYear);
                return 0;
            }
            public void Dispose() { }
        }
    }
}
