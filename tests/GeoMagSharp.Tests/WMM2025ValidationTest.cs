/****************************************************************************
 * File:            WMM2025ValidationTest.cs
 * Description:     Precision validation tests against NOAA WMM2025 official test values
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 * Notes:           Reference values from NOAA WMM2025 (December 2024)
 *                  https://www.ncei.noaa.gov/products/world-magnetic-model
 *                  PDF: https://www.ncei.noaa.gov/sites/default/files/2025-02/WMM2025testvalues.pdf
 *                  TXT: https://www.ncei.noaa.gov/sites/default/files/2025-02/WMM2025_TEST_VALUES.txt
 *
 *                  NOAA notes: "The computation was carried out with double precision
 *                  arithmetic. Single precision arithmetic can cause differences of
 *                  up to 0.1 nT."
 *  ****************************************************************************/

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using GeoMagSharp;

namespace GeoMagSharp_UnitTests
{
    [TestClass]
    public class WMM2025ValidationTest
    {
        // Tolerances based on NOAA's single-precision note (0.1 nT)
        // with margin for minor implementation differences
        private const double IntensityTolerance = 1.0;    // nT
        private const double AngleTolerance = 0.01;       // degrees

        private static MagneticModelSet _wmm2025;

        [ClassInitialize]
        public static void ClassInit(TestContext _)
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "TestData"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "tests", "GeoMagSharp.Tests", "TestData"),
                @"C:\GitHub\GeoMagSharp\tests\GeoMagSharp.Tests\TestData"
            };

            string modelPath = null;
            foreach (var path in possiblePaths)
            {
                var candidate = Path.GetFullPath(Path.Combine(path, "WMM2025.COF"));
                if (File.Exists(candidate))
                {
                    modelPath = candidate;
                    break;
                }
            }

            Assert.IsNotNull(modelPath, "Could not find WMM2025.COF in TestData directory");
            _wmm2025 = ModelReader.Read(modelPath);
            Assert.IsNotNull(_wmm2025, "ModelReader.Read returned null for WMM2025.COF");
        }

        //                              date     height  lat    lon     X        Y        Z         H        F        I      D
        [DataRow(2025.0, 0,   80,  0,   6521.6,  145.9,   54791.5,  6523.2,  55178.5,  83.21,  1.28,  DisplayName = "2025.0 0km 80N 0E")]
        [DataRow(2025.0, 0,   0,   120, 39677.8, -109.6,  -10580.2, 39677.9, 41064.3,  -14.93, -0.16, DisplayName = "2025.0 0km 0N 120E")]
        [DataRow(2025.0, 0,   -80, 240, 6117.5,  15751.9, -52022.5, 16898.1, 54698.2,  -72.00, 68.78, DisplayName = "2025.0 0km 80S 240E")]
        [DataRow(2025.0, 100, 80,  0,   6216.0,  92.4,    52598.8,  6216.7,  52964.9,  83.26,  0.85,  DisplayName = "2025.0 100km 80N 0E")]
        [DataRow(2025.0, 100, 0,   120, 37688.6, -96.2,   -10152.1, 37688.7, 39032.1,  -15.08, -0.15, DisplayName = "2025.0 100km 0N 120E")]
        [DataRow(2025.0, 100, -80, 240, 5907.6,  14780.3, -49540.7, 15917.1, 52035.0,  -72.19, 68.21, DisplayName = "2025.0 100km 80S 240E")]
        [DataRow(2027.5, 0,   80,  0,   6500.8,  294.5,   54869.4,  6507.5,  55253.9,  83.24,  2.59,  DisplayName = "2027.5 0km 80N 0E")]
        [DataRow(2027.5, 0,   0,   120, 39701.6, -167.4,  -10381.8, 39702.0, 41036.9,  -14.65, -0.24, DisplayName = "2027.5 0km 0N 120E")]
        [DataRow(2027.5, 0,   -80, 240, 6200.7,  15730.3, -51783.7, 16908.3, 54474.2,  -71.92, 68.49, DisplayName = "2027.5 0km 80S 240E")]
        [DataRow(2027.5, 100, 80,  0,   6196.7,  233.8,   52670.5,  6201.1,  53034.3,  83.29,  2.16,  DisplayName = "2027.5 100km 80N 0E")]
        [DataRow(2027.5, 100, 0,   120, 37711.5, -148.7,  -9969.8,  37711.8, 39007.4,  -14.81, -0.23, DisplayName = "2027.5 100km 0N 120E")]
        [DataRow(2027.5, 100, -80, 240, 5984.0,  14760.1, -49317.7, 15927.0, 51825.7,  -72.10, 67.93, DisplayName = "2027.5 100km 80S 240E")]
        [TestMethod]
        public void MainField_MatchesNOAATestValues(
            double decimalDate, double heightKm, double lat, double lon,
            double expectedX, double expectedY, double expectedZ,
            double expectedH, double expectedF, double expectedI, double expectedD)
        {
            // Arrange
            var dateOfCalc = decimalDate.ToDateTime();
            var calcOptions = new CalculationOptions
            {
                Latitude = lat,
                Longitude = lon,
                StartDate = dateOfCalc,
                SecularVariation = false
            };
            calcOptions.SetElevation(heightKm, Distance.Unit.kilometer, true);

            var internalSH = new Coefficients();
            var externalSH = new Coefficients();
            _wmm2025.GetIntExt(decimalDate, out internalSH, out externalSH);

            // Act
            var result = Calculator.SpotCalculation(calcOptions, dateOfCalc, _wmm2025, internalSH, externalSH);

            // Assert
            var label = $"({lat}, {lon}) h={heightKm}km date={decimalDate}";
            Assert.AreEqual(expectedX, result.NorthComp.Value, IntensityTolerance, $"X (North) at {label}");
            Assert.AreEqual(expectedY, result.EastComp.Value, IntensityTolerance, $"Y (East) at {label}");
            Assert.AreEqual(expectedZ, result.VerticalComp.Value, IntensityTolerance, $"Z (Vertical) at {label}");
            Assert.AreEqual(expectedH, result.HorizontalIntensity.Value, IntensityTolerance, $"H (Horizontal) at {label}");
            Assert.AreEqual(expectedF, result.TotalField.Value, IntensityTolerance, $"F (Total) at {label}");
            Assert.AreEqual(expectedI, result.Inclination.Value, AngleTolerance, $"I (Inclination) at {label}");
            Assert.AreEqual(expectedD, result.Declination.Value, AngleTolerance, $"D (Declination) at {label}");
        }

        //                              date     height  lat    lon     Xdot   Ydot    Zdot   Hdot   Fdot    Idot   Ddot
        [DataRow(2025.0, 0,   80,  0,   -8.3,   59.5,   31.1,  -7.0,  30.1,   0.01,  0.52,  DisplayName = "SV 2025.0 0km 80N 0E")]
        [DataRow(2025.0, 0,   0,   120, 9.5,    -23.1,  79.4,  9.6,   -11.2,  0.11,  -0.03, DisplayName = "SV 2025.0 0km 0N 120E")]
        [DataRow(2025.0, 0,   -80, 240, 33.3,   -8.6,   95.5,  4.0,   -89.6,  0.03,  -0.12, DisplayName = "SV 2025.0 0km 80S 240E")]
        [DataRow(2025.0, 100, 80,  0,   -7.7,   56.5,   28.7,  -6.9,  27.6,   0.01,  0.52,  DisplayName = "SV 2025.0 100km 80N 0E")]
        [DataRow(2025.0, 100, 0,   120, 9.2,    -21.0,  72.9,  9.2,   -10.0,  0.11,  -0.03, DisplayName = "SV 2025.0 100km 0N 120E")]
        [DataRow(2025.0, 100, -80, 240, 30.6,   -8.0,   89.2,  3.9,   -83.8,  0.03,  -0.11, DisplayName = "SV 2025.0 100km 80S 240E")]
        [DataRow(2027.5, 0,   80,  0,   -8.3,   59.5,   31.1,  -5.6,  30.3,   0.01,  0.53,  DisplayName = "SV 2027.5 0km 80N 0E")]
        [DataRow(2027.5, 0,   0,   120, 9.5,    -23.1,  79.4,  9.6,   -10.7,  0.11,  -0.03, DisplayName = "SV 2027.5 0km 0N 120E")]
        [DataRow(2027.5, 0,   -80, 240, 33.3,   -8.6,   95.5,  4.2,   -89.5,  0.04,  -0.12, DisplayName = "SV 2027.5 0km 80S 240E")]
        [DataRow(2027.5, 100, 80,  0,   -7.7,   56.5,   28.7,  -5.6,  27.8,   0.01,  0.52,  DisplayName = "SV 2027.5 100km 80N 0E")]
        [DataRow(2027.5, 100, 0,   120, 9.2,    -21.0,  72.9,  9.3,   -9.7,   0.11,  -0.03, DisplayName = "SV 2027.5 100km 0N 120E")]
        [DataRow(2027.5, 100, -80, 240, 30.6,   -8.0,   89.2,  4.0,   -83.7,  0.03,  -0.11, DisplayName = "SV 2027.5 100km 80S 240E")]
        [TestMethod]
        public void SecularVariation_MatchesNOAATestValues(
            double decimalDate, double heightKm, double lat, double lon,
            double expectedXdot, double expectedYdot, double expectedZdot,
            double expectedHdot, double expectedFdot, double expectedIdot, double expectedDdot)
        {
            // Arrange
            var dateOfCalc = decimalDate.ToDateTime();
            var calcOptions = new CalculationOptions
            {
                Latitude = lat,
                Longitude = lon,
                StartDate = dateOfCalc,
                SecularVariation = true
            };
            calcOptions.SetElevation(heightKm, Distance.Unit.kilometer, true);

            var internalSH = new Coefficients();
            var externalSH = new Coefficients();
            _wmm2025.GetIntExt(decimalDate, out internalSH, out externalSH);

            // Act
            var result = Calculator.SpotCalculation(calcOptions, dateOfCalc, _wmm2025, internalSH, externalSH);

            // Assert
            var label = $"({lat}, {lon}) h={heightKm}km date={decimalDate}";
            Assert.AreEqual(expectedXdot, result.NorthComp.ChangePerYear, IntensityTolerance, $"Xdot at {label}");
            Assert.AreEqual(expectedYdot, result.EastComp.ChangePerYear, IntensityTolerance, $"Ydot at {label}");
            Assert.AreEqual(expectedZdot, result.VerticalComp.ChangePerYear, IntensityTolerance, $"Zdot at {label}");
            Assert.AreEqual(expectedHdot, result.HorizontalIntensity.ChangePerYear, IntensityTolerance, $"Hdot at {label}");
            Assert.AreEqual(expectedFdot, result.TotalField.ChangePerYear, IntensityTolerance, $"Fdot at {label}");
            Assert.AreEqual(expectedIdot, result.Inclination.ChangePerYear, AngleTolerance, $"Idot at {label}");
            Assert.AreEqual(expectedDdot, result.Declination.ChangePerYear, AngleTolerance, $"Ddot at {label}");
        }
    }
}
