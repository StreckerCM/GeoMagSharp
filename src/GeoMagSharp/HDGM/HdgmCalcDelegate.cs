/****************************************************************************
 * File:            HdgmCalcDelegate.cs
 * Description:     Native delegate matching NOAA hdgmcalc() function signature
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System.Runtime.InteropServices;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Delegate matching the NOAA hdgmcalc() function signature.
    /// Reference: hdgmdll.c:46 in the HDGM2019 developer package — the
    /// standard (non-ACTIVATERT) shipped DLL has 9 numeric parameters plus
    /// the output array. Mismatching this signature corrupts the native
    /// call frame and produces AccessViolationException.
    /// </summary>
    /// <param name="latitude">Geodetic latitude in degrees.</param>
    /// <param name="longitude">Geodetic longitude in degrees.</param>
    /// <param name="depthMeters">Depth in meters (positive down).</param>
    /// <param name="day">Calendar day (1–31). Ignored if useDecimalYear == 1.</param>
    /// <param name="month">Calendar month (1–12). Ignored if useDecimalYear == 1.</param>
    /// <param name="year">Calendar year (e.g. 2020). Ignored if useDecimalYear == 1.</param>
    /// <param name="decimalYear">Date as decimal year (e.g. 2020.5). Used when useDecimalYear == 1.</param>
    /// <param name="useGeoid">0 = treat depth as height above WGS84 ellipsoid; 1 = treat depth as MSL (apply EGM96).</param>
    /// <param name="useDecimalYear">1 = use decimalYear; 0 = use day/month/year.</param>
    /// <param name="outData">Output array, must be at least 25 elements.</param>
    /// <returns>Status code; 0 = success.</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int HdgmCalcDelegate(
        double latitude,
        double longitude,
        double depthMeters,
        double day,
        double month,
        double year,
        double decimalYear,
        int useGeoid,
        int useDecimalYear,
        [In, Out] double[] outData);
}
