/****************************************************************************
 * File:            ModelHeaderInspector.cs
 * Description:     Reads first line of a .COF / .DAT file to classify model
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Globalization;
using System.IO;

namespace GeoMagSharp.Discovery
{
    /// <summary>
    /// Reads the first non-blank line of a model coefficient file and classifies it
    /// using <see cref="ExtensionMethods.CheckStringForModel"/>. Surfaces metadata
    /// available in the file (degree, secular variation degree, altitude validity,
    /// release date) per #31. For multi-epoch IGRF/DGRF .COF files, scans all epoch
    /// header lines to determine the latest epoch's label, validity range, and degree.
    /// </summary>
    internal static class ModelHeaderInspector
    {
        /// <summary>
        /// Inspects a single file and returns a <see cref="ModelDescriptor"/> populated
        /// from its header. Always returns a non-null descriptor; if the file
        /// is unparseable the descriptor's DetectedType is <see cref="knownModels.NONE"/>
        /// and date bounds are null.
        /// </summary>
        /// <exception cref="ArgumentNullException">filePath is null.</exception>
        /// <exception cref="FileNotFoundException">File does not exist.</exception>
        public static ModelDescriptor Inspect(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found: " + filePath, filePath);

            string firstLine = ReadFirstNonBlankLine(filePath);
            if (string.IsNullOrEmpty(firstLine))
            {
                return new ModelDescriptor(filePath, knownModels.NONE,
                    Path.GetFileNameWithoutExtension(filePath), null, null);
            }

            knownModels type = firstLine.CheckStringForModel();

            // IGRF and DGRF .COF files contain many 5-year epoch blocks. The first
            // line describes only the oldest epoch — scan the whole file to find
            // the latest epoch's label, validity range, degree, and altitude bounds.
            if (type == knownModels.IGRF || type == knownModels.DGRF)
            {
                MultiEpochInfo info;
                ScanMultiEpochHeaders(filePath, out info);

                if (!string.IsNullOrEmpty(info.LastEpochLabel))
                {
                    return new ModelDescriptor(filePath, type,
                        info.LastEpochLabel, info.MinDate, info.MaxDate,
                        description: null,
                        maxDegree: info.MaxDegree,
                        secularVariationDegree: info.SecularVariationDegree,
                        minAltitudeKm: info.MinAltitudeKm,
                        maxAltitudeKm: info.MaxAltitudeKm,
                        releaseDate: null,
                        epochCount: info.EpochCount);
                }
                // Scan failed (no recognizable epoch lines) — fall through to single-line behavior
            }

            double? minDate = ExtractYearFromHeader(firstLine);
            double? maxDate = minDate.HasValue ? minDate.Value + 5.0 : (double?)null;
            string displayName = BuildDisplayName(filePath, firstLine, type);

            // For WMM/WMMHR/EMM/BGGM single-epoch .COF files: the first line carries
            // a release date in parts[2] (e.g. "11/13/2024" for WMM2025). Degree is
            // not in the header but can be extracted by scanning coefficient rows for
            // the maximum n value.
            DateTime? releaseDate = ExtractReleaseDateFromFirstLine(firstLine);
            int? maxDegree = (type != knownModels.NONE)
                ? ScanMaxDegreeFromCoefficients(filePath)
                : null;

            // Single-epoch .COF formats (WMM/WMMHR/EMM/BGGM) carry one coefficient
            // set per file. EpochCount stays null for unclassified files to signal
            // "we couldn't parse this" rather than "this is one epoch".
            int? epochCount = (type != knownModels.NONE) ? (int?)1 : null;

            return new ModelDescriptor(filePath, type, displayName, minDate, maxDate,
                description: null,
                maxDegree: maxDegree,
                secularVariationDegree: null,
                minAltitudeKm: null,
                maxAltitudeKm: null,
                releaseDate: releaseDate,
                epochCount: epochCount);
        }

        // ─── Multi-epoch IGRF/DGRF scan ──────────────────────────────────

        /// <summary>Aggregated metadata from a full-file walk of an IGRF/DGRF .COF file.</summary>
        private struct MultiEpochInfo
        {
            public string LastEpochLabel;
            public double? MinDate;
            public double? MaxDate;
            public int? MaxDegree;              // from latest epoch's parts[2]
            public int? SecularVariationDegree; // from latest epoch's parts[3]
            public double? MinAltitudeKm;       // from latest epoch's parts[7]
            public double? MaxAltitudeKm;       // from latest epoch's parts[8]
            public int EpochCount;              // count of valid epoch header lines
        }

        /// <summary>
        /// Scans all lines of a multi-epoch IGRF/DGRF .COF file and identifies epoch
        /// header lines. Each epoch header has the form
        /// "  IGRFNN  YYYY.00 NMAX NMAX_SV ... STARTYR.00 ENDYR.00 MIN_ALT MAX_ALT".
        /// </summary>
        private static void ScanMultiEpochHeaders(string filePath, out MultiEpochInfo info)
        {
            info = default(MultiEpochInfo);

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write))
            using (var reader = new StreamReader(fs))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string trimmed = line.Trim();

                    if (!trimmed.StartsWith("IGRF", StringComparison.OrdinalIgnoreCase) &&
                        !trimmed.StartsWith("DGRF", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 7) continue;

                    double startYear, endYear;
                    if (!double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out startYear)) continue;
                    if (!double.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out endYear)) continue;
                    if (startYear < 1900.0 || startYear > 2100.0) continue;
                    if (endYear < startYear || endYear > 2100.0) continue;

                    // Count every successfully-parsed epoch header, not just the
                    // latest — out-of-order epochs in a malformed file should still
                    // contribute to EpochCount so consumers see the actual file size.
                    info.EpochCount++;

                    if (!info.MinDate.HasValue || startYear < info.MinDate.Value)
                    {
                        info.MinDate = startYear;
                    }
                    if (!info.MaxDate.HasValue || endYear > info.MaxDate.Value)
                    {
                        info.MaxDate = endYear;
                        info.LastEpochLabel = parts[0];

                        // Extract latest-epoch metadata
                        int n;
                        if (int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                        {
                            if (n > 0 && n < 1000) info.MaxDegree = n;
                        }
                        if (parts.Length > 3)
                        {
                            int nSv;
                            if (int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out nSv))
                            {
                                if (nSv >= 0 && nSv < 1000) info.SecularVariationDegree = nSv;
                            }
                        }
                        if (parts.Length > 8)
                        {
                            double aMin, aMax;
                            if (double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out aMin)
                                && double.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out aMax))
                            {
                                info.MinAltitudeKm = aMin;
                                info.MaxAltitudeKm = aMax;
                            }
                        }
                    }
                }
            }
        }

        // ─── Single-epoch (WMM/WMMHR/EMM/BGGM) helpers ───────────────────

        /// <summary>
        /// Extracts release date from a WMM/WMMHR-style first line:
        /// "    2025.0            WMM-2025     11/13/2024".
        /// Returns null if parts[2] does not parse as a date in M/d/yyyy or
        /// d/M/yyyy form.
        /// </summary>
        private static DateTime? ExtractReleaseDateFromFirstLine(string firstLine)
        {
            var parts = firstLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;

            // Try common formats observed in NOAA-published WMM/WMMHR .COF files
            string[] formats = { "M/d/yyyy", "MM/dd/yyyy", "d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
            DateTime parsed;
            if (DateTime.TryParseExact(parts[2], formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsed))
            {
                return parsed;
            }
            return null;
        }

        /// <summary>
        /// Walks coefficient lines and returns the maximum n value, which is the
        /// model's spherical harmonic main field degree. Used for WMM/WMMHR/EMM/BGGM
        /// where the first-line header doesn't carry the degree explicitly.
        /// Returns null if no coefficient line is parseable.
        /// </summary>
        private static int? ScanMaxDegreeFromCoefficients(string filePath)
        {
            int maxN = 0;
            bool anyFound = false;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write))
            using (var reader = new StreamReader(fs))
            {
                bool firstNonBlankSkipped = false;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Skip the very first non-blank line (model header)
                    if (!firstNonBlankSkipped)
                    {
                        firstNonBlankSkipped = true;
                        continue;
                    }

                    string trimmed = line.Trim();
                    var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    int n;
                    if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) continue;
                    if (n < 0 || n > 1000) continue;

                    int m;
                    if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out m)) continue;
                    if (m < 0 || m > n) continue;

                    anyFound = true;
                    if (n > maxN) maxN = n;
                }
            }

            return anyFound ? (int?)maxN : null;
        }

        // ─── Existing helpers ────────────────────────────────────────────

        private static string ReadFirstNonBlankLine(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write))
            using (var reader = new StreamReader(fs))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
                }
                return null;
            }
        }

        private static double? ExtractYearFromHeader(string line)
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                double v;
                if (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                {
                    if (v >= 1900.0 && v <= 2100.0) return v;
                }
            }
            return null;
        }

        private static string BuildDisplayName(string filePath, string firstLine, knownModels type)
        {
            if (type == knownModels.NONE)
                return Path.GetFileNameWithoutExtension(filePath);

            // Pull the model token plus year if present (matches "WMM-2025", "IGRF14", "EMM-2017", etc.).
            var trimmed = firstLine.Trim();
            int idx = trimmed.IndexOf(type.ToString(), StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return Path.GetFileNameWithoutExtension(filePath);

            int end = idx;
            while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end])) end++;
            return trimmed.Substring(idx, end - idx);
        }
    }
}
