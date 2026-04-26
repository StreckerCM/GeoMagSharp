/****************************************************************************
 * File:            FakeHdgmInvoker.cs
 * Description:     Test double for INativeHdgmInvoker used by adapter unit tests
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/
using System.Collections.Generic;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    /// <summary>
    /// Test double for INativeHdgmInvoker. Records calls and returns
    /// configurable canned responses without invoking any native code.
    /// </summary>
    internal class FakeHdgmInvoker : INativeHdgmInvoker
    {
        public double[] CannedOutData { get; set; } = new double[25];
        public int CannedReturnValue { get; set; } = 0;
        public List<CalculationCall> Calls { get; } = new List<CalculationCall>();
        public bool DisposeWasCalled { get; private set; }

        public int Calculate(double latitude, double longitude, double depthMeters, double decimalYear, double[] outData)
        {
            Calls.Add(new CalculationCall
            {
                Latitude = latitude,
                Longitude = longitude,
                DepthMeters = depthMeters,
                DecimalYear = decimalYear
            });
            System.Array.Copy(CannedOutData, outData, System.Math.Min(CannedOutData.Length, outData.Length));
            return CannedReturnValue;
        }

        public void Dispose() => DisposeWasCalled = true;
    }

    internal class CalculationCall
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DepthMeters { get; set; }
        public double DecimalYear { get; set; }
    }
}
