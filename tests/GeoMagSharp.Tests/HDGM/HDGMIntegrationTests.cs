/****************************************************************************
 * File:            HDGMIntegrationTests.cs
 * Description:     HDGM integration tests (env-var-gated, CI excluded)
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class HDGMIntegrationTests
    {
        private static string DllPath => Environment.GetEnvironmentVariable("HDGM_DLL_PATH");
        private static string TestValuesPath => Environment.GetEnvironmentVariable("HDGM_TEST_VALUES_PATH");

        [TestInitialize]
        public void RequireEnvironment()
        {
            if (string.IsNullOrWhiteSpace(DllPath) || !File.Exists(DllPath))
                Assert.Inconclusive("HDGM_DLL_PATH not set or file missing; integration tests skipped.");
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_LoadsRealDll_NoException()
        {
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath); // must not throw
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_SinglePoint_ReturnsPlausibleValues()
        {
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath);
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0,
                    Longitude = -100.0,
                    StartDate = new DateTime(2020, 6, 1)
                });

                Assert.AreEqual(1, geo.ResultsOfCalculation.Count);
                var r = geo.ResultsOfCalculation[0];

                // Plausibility ranges for mid-North-America in 2020:
                Assert.IsTrue(r.Declination.Value > 0 && r.Declination.Value < 15,
                    $"Declination {r.Declination.Value}° not in plausible range 0..15°");
                Assert.IsTrue(r.TotalField.Value > 30000 && r.TotalField.Value < 70000,
                    $"TotalField {r.TotalField.Value} nT not in plausible range 30000..70000");
                Assert.IsTrue(r.Inclination.Value > 50 && r.Inclination.Value < 80,
                    $"Inclination {r.Inclination.Value}° not in plausible range 50..80°");
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_SamplePointsFromTestValues_AllWithinTolerance()
        {
            if (string.IsNullOrWhiteSpace(TestValuesPath) || !File.Exists(TestValuesPath))
            {
                Assert.Inconclusive("HDGM_TEST_VALUES_PATH not set; numerical tolerance test skipped.");
                return;
            }

            // Format: "Date Depth Lat Lon D I H X Y Z F dD dI dH dX dY dZ dF" (18 cols, whitespace-separated)
            // Field indices in the file: 0..17
            string[] lines = File.ReadAllLines(TestValuesPath)
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
                .Take(20) // limit to first 20 rows for runtime
                .ToArray();

            Assert.IsTrue(lines.Length >= 5, $"Expected at least 5 sample rows in {TestValuesPath}");

            int passed = 0;
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath);
                foreach (var line in lines)
                {
                    var f = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (f.Length < 11) continue;

                    double year = double.Parse(f[0], System.Globalization.CultureInfo.InvariantCulture);
                    double depth = double.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture);
                    double lat = double.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture);
                    double lon = double.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture);
                    double expectedD = double.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture);
                    double expectedI = double.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture);
                    double expectedF = double.Parse(f[10], System.Globalization.CultureInfo.InvariantCulture);

                    int yearInt = (int)year;
                    DateTime date = new DateTime(yearInt, 1, 1).AddDays((year - yearInt) * 365.25);

                    var opts = new CalculationOptions { Latitude = lat, Longitude = lon, StartDate = date };
                    opts.SetElevation(value: depth, unit: Distance.Unit.meter, isAltitude: false);

                    geo.MagneticCalculations(opts);
                    var r = geo.ResultsOfCalculation.Last();

                    Assert.AreEqual(expectedD, r.Declination.Value, 0.0001,
                        $"D mismatch at year={year}, depth={depth}, lat={lat}, lon={lon}");
                    Assert.AreEqual(expectedI, r.Inclination.Value, 0.0001,
                        $"I mismatch at year={year}, depth={depth}, lat={lat}, lon={lon}");
                    Assert.AreEqual(expectedF, r.TotalField.Value, 0.05,
                        $"F mismatch at year={year}, depth={depth}, lat={lat}, lon={lon}");

                    passed++;
                }
            }
            Assert.IsTrue(passed >= 5, $"Validated {passed} sample points; expected >= 5");
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_OutOfRangeDate_ThrowsOutOfRange()
        {
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath);
                try
                {
                    geo.MagneticCalculations(new CalculationOptions
                    {
                        Latitude = 40.0, Longitude = -100.0,
                        StartDate = new DateTime(1500, 1, 1) // far before HDGM range
                    });
                    Assert.Fail("Expected GeoMagExceptionOutOfRange");
                }
                catch (GeoMagExceptionOutOfRange) { /* expected */ }
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_DisposeFreesDll()
        {
            // Soft check: load and dispose multiple times; should not throw or leak observably.
            for (int i = 0; i < 3; i++)
            {
                using (var geo = new GeoMag())
                {
                    geo.LoadModel(DllPath);
                }
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_SigmaValuesPopulated()
        {
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath);
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0, StartDate = new DateTime(2020, 6, 1)
                });
                var u = geo.ResultsOfCalculation[0].Uncertainty;
                Assert.IsNotNull(u);
                Assert.IsNotNull(u.SigmaD);
                Assert.IsNotNull(u.SigmaI);
                Assert.IsNotNull(u.SigmaF);
                Assert.IsNotNull(u.HighResolutionCoverage); // bool? — should be either true or false, not null
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_NSDCoverageFlag_ReturnsBoolForKnownLocations()
        {
            // North America — should be high-res covered
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath);
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0, StartDate = new DateTime(2020, 6, 1)
                });
                Assert.AreEqual(true, geo.ResultsOfCalculation[0].Uncertainty.HighResolutionCoverage,
                    "North America should be NSD high-res covered");
            }
        }
    }
}
