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
    /// Reference: HDGM_Sublibrary.c:46 — int __stdcall hdgmcalc(double lt, double ln, ...).
    /// </summary>
    /// <param name="latitude">Geodetic latitude in degrees.</param>
    /// <param name="longitude">Geodetic longitude in degrees.</param>
    /// <param name="depthMeters">Depth in meters (positive down).</param>
    /// <param name="decimalYear">Date as decimal year.</param>
    /// <param name="usePomme">HDGM-RT magnetospheric flag (0 = disabled).</param>
    /// <param name="useDifi">HDGM-RT ionospheric flag (0 = disabled).</param>
    /// <param name="outData">Output array, must be at least 25 elements.</param>
    /// <returns>Status code; 0 = success.</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int HdgmCalcDelegate(
        double latitude,
        double longitude,
        double depthMeters,
        double decimalYear,
        int usePomme,
        int useDifi,
        [In, Out] double[] outData);
}
