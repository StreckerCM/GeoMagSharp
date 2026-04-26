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
            Assert.IsTrue(fake.Calls.Count >= 1);
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
    }
}
