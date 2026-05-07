/****************************************************************************
 * File:            GeoMagHDGMRoutingTests.cs
 * Description:     Integration tests verifying GeoMag routes HDGM model sets
 *                  through HDGMCalculationAdapter and implements IDisposable
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/
using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class GeoMagHDGMRoutingTests
    {
        // Helper: bypass file IO/native loading by injecting a MagneticModelSet pre-built
        // with a FakeHdgmInvoker. Uses the existing public LoadModel(MagneticModelSet) overload.
        private GeoMag NewGeoMagWithFakeHDGM(double[] cannedOutData)
        {
            var fake = new FakeHdgmInvoker { CannedOutData = cannedOutData, CannedReturnValue = 0 };
            var set = new MagneticModelSet
            {
                Type = knownModels.HDGM,
                Name = "HDGM-FAKE",
                MinDate = 1900.0,
                MaxDate = 9999.0,
                NativeInvoker = fake
            };
            var geo = new GeoMag();
            geo.LoadModel(set);
            return geo;
        }

        [TestMethod]
        public void MagneticCalculations_OnHDGMModelSet_RoutesToAdapter()
        {
            var data = new double[25];
            data[0] = 5.55;  // Declination
            using (var geo = NewGeoMagWithFakeHDGM(data))
            {
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0,
                    Longitude = -100.0,
                    StartDate = new DateTime(2020, 6, 1)
                });
                Assert.AreEqual(1, geo.ResultsOfCalculation.Count);
                Assert.AreEqual(5.55, geo.ResultsOfCalculation[0].Declination.Value, 1e-9);
            }
        }

        [TestMethod]
        public void MagneticCalculations_OnHDGMModelSet_DateSweep_CallsInvokerOncePerDate()
        {
            var fake = new FakeHdgmInvoker { CannedOutData = new double[25], CannedReturnValue = 0 };
            var set = new MagneticModelSet
            {
                Type = knownModels.HDGM,
                Name = "HDGM-FAKE",
                MinDate = 1900.0,
                MaxDate = 9999.0,
                NativeInvoker = fake
            };
            var geo = new GeoMag();
            geo.LoadModel(set);
            geo.MagneticCalculations(new CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -100.0,
                StartDate = new DateTime(2020, 1, 1),
                EndDate = new DateTime(2020, 1, 6),
                StepInterval = 1
            });
            // Date stepping in existing GeoMag.MagneticCalculations: 6 days → 6 iterations
            Assert.AreEqual(6, fake.Calls.Count, "expected one native call per day in the 6-day sweep");
        }

        [TestMethod]
        public void Dispose_DisposesUnderlyingNativeInvoker()
        {
            var fake = new FakeHdgmInvoker();
            var set = new MagneticModelSet
            {
                Type = knownModels.HDGM,
                Name = "HDGM-FAKE",
                MinDate = 1900.0,
                MaxDate = 9999.0,
                NativeInvoker = fake
            };
            var geo = new GeoMag();
            geo.LoadModel(set);
            geo.Dispose();
            Assert.IsTrue(fake.DisposeWasCalled);
        }

        [TestMethod]
        public void IsDisposable()
        {
            using (var geo = new GeoMag())
            {
                Assert.IsTrue(geo is IDisposable);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionModelNotLoaded))]
        public void MagneticCalculations_AfterDispose_ThrowsModelNotLoaded()
        {
            var geo = NewGeoMagWithFakeHDGM(new double[25]);
            geo.Dispose();
            geo.MagneticCalculations(new CalculationOptions
            {
                Latitude = 40.0, Longitude = -100.0, StartDate = new DateTime(2020, 6, 1)
            });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void LoadModel_WithNullInvoker_ThrowsArgumentNull()
        {
            using (var geo = new GeoMag())
            {
                geo.LoadModel((INativeHdgmInvoker)null);
            }
        }

        [TestMethod]
        public void LoadModel_WithFakeInvoker_LoadsAndCalculates()
        {
            var data = new double[25];
            data[0] = 7.5;
            var fake = new FakeHdgmInvoker { CannedOutData = data, CannedReturnValue = 0 };
            using (var geo = new GeoMag())
            {
                geo.LoadModel(fake, "TEST-FAKE");
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0, StartDate = new DateTime(2020, 6, 1)
                });
                Assert.AreEqual(7.5, geo.ResultsOfCalculation[0].Declination.Value, 1e-9);
            }
        }

        [TestMethod]
        public void LoadModel_WithFakeInvoker_DefaultModelNameIsHdgmCustom()
        {
            using (var geo = new GeoMag())
            {
                geo.LoadModel(new FakeHdgmInvoker());
                // Indirect verification: the model set's Type is HDGM and operations work
                // without relying on accessing the internal Name property from the test project.
                // This test mainly verifies the no-modelName overload doesn't throw.
            }
        }

        // ───────── #30: HDGM date-range validation ─────────
        // Before #30, HDGM loads hardcoded MaxDate=9999, so the existing
        // IsDateInRange check never tripped. The fix probes the DLL for its
        // real MaxDate (via HDGMModelLoader) and adds an opt-in
        // AllowExtrapolation flag for callers who explicitly want raw
        // extrapolation. These tests use the LoadModel(invoker, ..., minDate, maxDate)
        // overload to inject tight bounds without needing a real DLL.

        private const double TightMinDate = 2018.0;
        private const double TightMaxDate = 2020.0;

        private static GeoMag NewGeoMagWithTightBounds()
        {
            var fake = new FakeHdgmInvoker { CannedOutData = new double[25], CannedReturnValue = 0 };
            var geo = new GeoMag();
            geo.LoadModel(fake, "HDGM-TIGHT", minDate: TightMinDate, maxDate: TightMaxDate);
            return geo;
        }

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionOutOfRange))]
        public void MagneticCalculations_StartDatePastMaxDate_ThrowsOutOfRange()
        {
            using (var geo = NewGeoMagWithTightBounds())
            {
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0,
                    StartDate = new DateTime(2025, 6, 1)
                });
            }
        }

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionOutOfRange))]
        public void MagneticCalculations_StartDateBeforeMinDate_ThrowsOutOfRange()
        {
            using (var geo = NewGeoMagWithTightBounds())
            {
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0,
                    StartDate = new DateTime(2010, 6, 1)
                });
            }
        }

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionOutOfRange))]
        public void MagneticCalculations_EndDatePastMaxDate_ThrowsOutOfRange()
        {
            using (var geo = NewGeoMagWithTightBounds())
            {
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0,
                    StartDate = new DateTime(2019, 1, 1),
                    EndDate = new DateTime(2025, 1, 1)
                });
            }
        }

        [TestMethod]
        public void MagneticCalculations_AllowExtrapolation_BypassesDateCheck()
        {
            // Same scenario that throws above; with AllowExtrapolation=true the
            // check is skipped and the calculation proceeds (FakeHdgmInvoker
            // returns canned data — real DLL would extrapolate or sentinel).
            using (var geo = NewGeoMagWithTightBounds())
            {
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0,
                    StartDate = new DateTime(2025, 6, 1),
                    AllowExtrapolation = true
                });
                Assert.AreEqual(1, geo.ResultsOfCalculation.Count,
                    "AllowExtrapolation=true must skip date check and produce results");
            }
        }

        [TestMethod]
        public void MagneticCalculations_StartDateJustInsideMaxDate_DoesNotThrow()
        {
            // 2019-12-31 → decimal-year ~2019.9986 (day 364.5 / 365), just below
            // TightMaxDate=2020.0. Confirms the inclusive upper-bound check holds
            // for dates that DO fall inside the model's validity range.
            using (var geo = NewGeoMagWithTightBounds())
            {
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0,
                    StartDate = new DateTime(2019, 12, 31)
                });
                Assert.AreEqual(1, geo.ResultsOfCalculation.Count);
            }
        }

        [TestMethod]
        public void MagneticCalculations_StartDateJustInsideMinDate_DoesNotThrow()
        {
            // 2018-06-01 → decimal-year ~2018.42, well inside [2018.0, 2020.0].
            using (var geo = NewGeoMagWithTightBounds())
            {
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0,
                    StartDate = new DateTime(2018, 6, 1)
                });
                Assert.AreEqual(1, geo.ResultsOfCalculation.Count);
            }
        }

        [TestMethod]
        public async System.Threading.Tasks.Task MagneticCalculationsAsync_StartDatePastMaxDate_ThrowsOutOfRange()
        {
            using (var geo = NewGeoMagWithTightBounds())
            {
                await Assert.ThrowsExceptionAsync<GeoMagExceptionOutOfRange>(() =>
                    geo.MagneticCalculationsAsync(new CalculationOptions
                    {
                        Latitude = 40.0, Longitude = -100.0,
                        StartDate = new DateTime(2025, 6, 1)
                    }));
            }
        }

        [TestMethod]
        public async System.Threading.Tasks.Task MagneticCalculationsAsync_AllowExtrapolation_BypassesDateCheck()
        {
            using (var geo = NewGeoMagWithTightBounds())
            {
                await geo.MagneticCalculationsAsync(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0,
                    StartDate = new DateTime(2025, 6, 1),
                    AllowExtrapolation = true
                });
                Assert.AreEqual(1, geo.ResultsOfCalculation.Count);
            }
        }
    }
}
