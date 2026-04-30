/****************************************************************************
 * File:            HdgmDateProbeIntegrationTests.cs
 * Description:     Integration tests for HDGM probe with the real NOAA DLL
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.Discovery;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.Discovery
{
    /// <summary>
    /// Integration tests requiring the real NOAA HDGM DLL. Gated on the
    /// HDGM_DLL_PATH environment variable; CI excludes them via
    /// --filter "TestCategory!=RequiresHDGMDll".
    /// </summary>
    [TestClass]
    public class HdgmDateProbeIntegrationTests
    {
        private static string DllPath => Environment.GetEnvironmentVariable("HDGM_DLL_PATH");

        [TestInitialize]
        public void RequireDll()
        {
            if (string.IsNullOrWhiteSpace(DllPath) || !File.Exists(DllPath))
                Assert.Inconclusive("HDGM_DLL_PATH not set; integration tests skipped.");
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_RealHdgmDll_Probes_ReturnsValidDateRange()
        {
            var (min, max) = HdgmDateProbe.Probe(p => new LoadLibraryHdgmInvoker(p), DllPath);
            Assert.IsTrue(min.HasValue, "expected min date populated");
            Assert.IsTrue(max.HasValue, "expected max date populated");
            Assert.AreEqual(1900.0, min.Value);
            Assert.IsTrue(max.Value >= 2019.0, "expected upper bound to cover at least 2019");
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_DiscoverModels_FolderWithRealHdgmDll_ReturnsHdgmDescriptor()
        {
            string tempDir = Path.Combine(Path.GetTempPath(),
                "GeoMagSharpInt_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                string copied = Path.Combine(tempDir, Path.GetFileName(DllPath));
                File.Copy(DllPath, copied, overwrite: true);

                var results = ModelDiscovery.DiscoverModels(tempDir).ToList();
                var hdgm = results.SingleOrDefault(d => d.DetectedType == knownModels.HDGM);
                Assert.IsNotNull(hdgm, "expected an HDGM descriptor in the results");
                Assert.IsTrue(hdgm.MinDate.HasValue && hdgm.MinDate.Value == 1900.0);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_DiscoverModels_TwoConsecutiveCallsWithCache_SecondCallSkipsProbe()
        {
            string tempDir = Path.Combine(Path.GetTempPath(),
                "GeoMagSharpInt_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                string copied = Path.Combine(tempDir, Path.GetFileName(DllPath));
                File.Copy(DllPath, copied, overwrite: true);

                var opts = new ModelDiscoveryOptions { UseCache = true };
                var first = ModelDiscovery.DiscoverModels(tempDir, opts).ToList();
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, ".models.json")));

                var second = ModelDiscovery.DiscoverModels(tempDir, opts).ToList();
                Assert.AreEqual(first.Count, second.Count);
                Assert.AreEqual(first[0].MinDate, second[0].MinDate);
                Assert.AreEqual(first[0].MaxDate, second[0].MaxDate);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}
