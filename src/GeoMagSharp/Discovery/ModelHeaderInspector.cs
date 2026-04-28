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
    /// store an integer year on line 1) returns DAT-typed metadata.
    /// </summary>
    internal static class ModelHeaderInspector
    {
        /// <summary>
        /// Inspects a single file and returns a <see cref="ModelDescriptor"/> populated
        /// from its first-line header. Always returns a non-null descriptor; if the file
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
