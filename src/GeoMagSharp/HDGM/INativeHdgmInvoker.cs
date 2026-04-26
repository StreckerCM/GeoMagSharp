/****************************************************************************
 * File:            INativeHdgmInvoker.cs
 * Description:     Contract for invoking the NOAA HDGM native function
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
        int Calculate(double latitude, double longitude, double depthMeters, double decimalYear, double[] outData);
    }
}
