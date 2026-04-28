/****************************************************************************
 * File:            HdgmDateProbe.cs
 * Description:     Discovers HDGM DLL date-range bounds via forward probing
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using GeoMagSharp.HDGM;

namespace GeoMagSharp.Discovery
{
    /// <summary>
    /// Probes an HDGM DLL to determine its valid date range without trusting
    /// filename year alone. Loads the DLL via the supplied factory, calls
    /// hdgmcalc with a known-NSD-covered point at year, year+1, ... up to 8
    /// times, and treats the first sentinel result as the upper bound.
    /// </summary>
    internal static class HdgmDateProbe
    {
        private const int MaxForwardYearsToProbe = 8;
        private const double Sentinel = -99999.0;
        private const double ProbeLatitude = 40.0;        // mid-North-America; well-NSD-covered
        private const double ProbeLongitude = -100.0;
        private const double ProbeDepthMeters = 0.0;
        private const double KnownStartYear = 1900.0;     // HDGM convention back to 1900

        /// <summary>
        /// Extracts a 4-digit year (19xx or 20xx) from a filename. Avoids matching
        /// a "-64" bitness suffix or other short numeric tokens. Returns null if no
        /// year-shaped token is found.
        /// </summary>
        public static int? ExtractYearFromFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return null;
            var name = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(name)) return null;

            var match = Regex.Match(name, @"(?:^|[^0-9])(19\d{2}|20\d{2})(?:[^0-9]|$)");
            if (!match.Success) return null;
            return int.Parse(match.Groups[1].Value);
        }

        /// <summary>
        /// Probes an HDGM DLL and returns its (min, max) decimal-year bounds. The factory
        /// receives the dllPath and produces an INativeHdgmInvoker (in production this is
        /// LoadLibraryHdgmInvoker; tests inject a fake). Catches all exceptions from the
        /// factory and the probe loop; returns (null, null) on any failure.
        /// </summary>
        /// <param name="invokerFactory">Factory that produces an INativeHdgmInvoker for a path.</param>
        /// <param name="dllPath">Path to the HDGM DLL.</param>
        /// <returns>Tuple (minDate, maxDate). Both null if probe failed or all probes sentineled.</returns>
        public static (double? minDate, double? maxDate) Probe(
            Func<string, INativeHdgmInvoker> invokerFactory, string dllPath)
        {
            int startYear = ExtractYearFromFilename(dllPath) ?? DateTime.UtcNow.Year;

            try
            {
                using (var invoker = invokerFactory(dllPath))
                {
                    if (invoker == null) return (null, null);

                    var outData = new double[25];
                    int maxValidYear = startYear - 1;

                    for (int year = startYear; year < startYear + MaxForwardYearsToProbe; year++)
                    {
                        outData[0] = 0.0;  // reset before each call
                        invoker.Calculate(ProbeLatitude, ProbeLongitude, ProbeDepthMeters,
                            (double)year + 0.5, outData);
                        if (outData[0] == Sentinel) break;
                        maxValidYear = year;
                    }

                    if (maxValidYear < startYear) return (null, null);
                    return (KnownStartYear, (double)(maxValidYear + 1));
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException) && !(ex is ThreadAbortException))
            {
                // LoadLibraryEx fail (bitness / AV / corrupt), missing symbol, or anything
                // else from the native side. Fall back to null bounds; runtime sentinel is
                // the authoritative guard. Fatal exceptions are allowed to propagate.
                return (null, null);
            }
        }
    }
}
