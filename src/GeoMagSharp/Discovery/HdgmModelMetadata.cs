/****************************************************************************
 * File:            HdgmModelMetadata.cs
 * Description:     Filename-keyed lookup of published HDGM crustal degree
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System.IO;
using System.Text.RegularExpressions;

namespace GeoMagSharp.Discovery
{
    /// <summary>
    /// Resolves HDGM model metadata from filename.
    /// </summary>
    /// <remarks>
    /// The HDGM DLL exports only <c>hdgmcalc</c> — no metadata getters, no
    /// VERSIONINFO resource, faked PE timestamp. The crustal spherical-harmonic
    /// degree is the model's defining published characteristic and the only
    /// metadata we can attribute to a publicly citable source.
    ///
    /// Source: CIRES Geomagnetic and Electric Field Models page,
    /// https://geomag.colorado.edu/geomagnetic-and-electric-field-models
    ///
    /// HDGM eras (per CIRES):
    ///   2017–2020: degree 720
    ///   2021–2025: degree 790
    ///   2026     : degree 1040
    ///
    /// For files outside this range (pre-2017 or post-2026), the lookup
    /// returns null rather than guessing.
    /// </remarks>
    internal static class HdgmModelMetadata
    {
        // Captures the 4-digit year that follows "hdgm" in the filename
        // (e.g. "hdgm2019.dll", "hdgm2019-64.dll", "HDGM2026-RT.dll").
        private static readonly Regex VersionYearRegex = new Regex(
            @"hdgm(\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Returns the published crustal-model spherical-harmonic degree for
        /// the HDGM build identified by filename, or null if the year cannot
        /// be parsed or falls outside the CIRES-published range.
        /// </summary>
        public static int? GetMaxDegreeFromFilename(string filePath)
        {
            int? year = TryParseHdgmYear(filePath);
            if (!year.HasValue) return null;

            int y = year.Value;
            if (y >= 2017 && y <= 2020) return 720;
            if (y >= 2021 && y <= 2025) return 790;
            if (y == 2026) return 1040;
            return null;
        }

        private static int? TryParseHdgmYear(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            string filename;
            try
            {
                filename = Path.GetFileName(filePath);
            }
            catch (System.ArgumentException)
            {
                return null;
            }
            if (string.IsNullOrEmpty(filename)) return null;

            var match = VersionYearRegex.Match(filename);
            if (!match.Success) return null;

            int year;
            if (!int.TryParse(match.Groups[1].Value, out year)) return null;
            return year;
        }
    }
}
