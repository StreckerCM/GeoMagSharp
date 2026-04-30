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
    /// using <see cref="ExtensionMethods.CheckStringForModel"/>. For DAT files (which
    /// store an integer year on line 1) returns DAT-typed metadata. For multi-epoch
    /// IGRF/DGRF .COF files, scans all epoch header lines to determine the latest
    /// epoch's label and the file's overall validity range.
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
            // the latest epoch's label and the overall validity range.
            if (type == knownModels.IGRF || type == knownModels.DGRF)
            {
                string multiLabel;
                double? multiMin, multiMax;
                ScanMultiEpochHeaders(filePath, out multiLabel, out multiMin, out multiMax);

                if (!string.IsNullOrEmpty(multiLabel))
                {
                    return new ModelDescriptor(filePath, type, multiLabel, multiMin, multiMax);
                }
                // Scan failed (no recognizable epoch lines) — fall through to single-line behavior
            }

            double? minDate = ExtractYearFromHeader(firstLine);
            double? maxDate = minDate.HasValue ? minDate.Value + 5.0 : (double?)null;

            string displayName = BuildDisplayName(filePath, firstLine, type);

            return new ModelDescriptor(filePath, type, displayName, minDate, maxDate);
        }

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

        /// <summary>
        /// Scans all lines of a multi-epoch IGRF/DGRF .COF file and identifies epoch
        /// header lines. Each epoch header has the form
        /// "  IGRFNN  YYYY.00 NMAX ... STARTYR.00 ENDYR.00 ...". Returns the label of
        /// the LAST (latest) epoch, the start year of the FIRST epoch, and the end year
        /// of the LAST epoch. All out-params are set to null/empty if no epoch header is found.
        /// </summary>
        private static void ScanMultiEpochHeaders(string filePath,
            out string lastEpochLabel, out double? minDate, out double? maxDate)
        {
            lastEpochLabel = null;
            minDate = null;
            maxDate = null;

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

                    if (!minDate.HasValue || startYear < minDate.Value)
                    {
                        minDate = startYear;
                    }
                    if (!maxDate.HasValue || endYear > maxDate.Value)
                    {
                        maxDate = endYear;
                        lastEpochLabel = parts[0];
                    }
                }
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
