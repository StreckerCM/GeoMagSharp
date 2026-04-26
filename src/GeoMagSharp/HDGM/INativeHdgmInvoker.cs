/****************************************************************************
 * File:            INativeHdgmInvoker.cs
 * Description:     Contract for invoking the NOAA HDGM native function
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Contract for calling NOAA's HDGM native calculation function.
    /// Production implementation: <see cref="LoadLibraryHdgmInvoker"/> (internal).
    /// Test implementations may be substituted via the public API for unit testing.
    /// </summary>
    public interface INativeHdgmInvoker : IDisposable
    {
        /// <summary>
        /// Invokes the native hdgmcalc function and returns its 25-element output array.
        /// </summary>
        /// <param name="latitude">Geodetic latitude in decimal degrees (-90 to +90).</param>
        /// <param name="longitude">Geodetic longitude in decimal degrees (-180 to +180).</param>
        /// <param name="depthMeters">Depth in meters, positive downward (negative for altitude).</param>
        /// <param name="decimalYear">Date as a decimal year (e.g., 2020.5).</param>
        /// <param name="outData">Output buffer, length 25. Must be allocated by caller.</param>
        /// <returns>Native function status code; 0 = success.</returns>
        /// <remarks>
        /// HDGM-RT magnetospheric/ionospheric flags (UsePomme, UseDifi) are
        /// not exposed in this v1 interface — the production implementation
        /// always passes 0 for both. HDGM-RT support is a deferred follow-up
        /// per the design's Section 11; if added, it will likely surface as
        /// a flag on CalculationOptions rather than as additional method
        /// parameters here, to preserve binary compatibility for consumers
        /// that implement this interface.
        /// </remarks>
        int Calculate(double latitude, double longitude, double depthMeters, double decimalYear, double[] outData);
    }
}
