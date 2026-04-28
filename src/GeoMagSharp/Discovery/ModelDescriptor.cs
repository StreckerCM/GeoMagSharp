/****************************************************************************
 * File:            ModelDescriptor.cs
 * Description:     Immutable snapshot of a discovered magnetic model file
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;

namespace GeoMagSharp
{
    /// <summary>
    /// Snapshot of a discovered magnetic model file. All properties are
    /// populated at construction; the instance is read-only.
    /// </summary>
    public sealed class ModelDescriptor
    {
        /// <summary>Constructs a new descriptor.</summary>
        /// <param name="filePath">Path to the file as discovered. Required.</param>
        /// <param name="detectedType">Detected model type, or <see cref="knownModels.NONE"/> if unidentified.</param>
        /// <param name="displayName">Human-friendly name (e.g. "WMM2025"). Null is normalised to empty.</param>
        /// <param name="minDate">Earliest valid decimal year, or null if unknown.</param>
        /// <param name="maxDate">Latest valid decimal year (exclusive), or null if unknown.</param>
        /// <param name="description">Optional free-form description.</param>
        public ModelDescriptor(
            string filePath,
            knownModels detectedType,
            string displayName,
            double? minDate,
            double? maxDate,
            string description = null)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            DetectedType = detectedType;
            DisplayName = displayName ?? string.Empty;
            MinDate = minDate;
            MaxDate = maxDate;
            Description = description ?? string.Empty;
        }

        /// <summary>Absolute or relative path to the file as discovered.</summary>
        public string FilePath { get; }

        /// <summary>Detected model type. <see cref="knownModels.NONE"/> when Quick mode skipped header peek or the header was unparseable.</summary>
        public knownModels DetectedType { get; }

        /// <summary>Human-friendly name for display. Falls back to filename-without-extension when no header parse was performed.</summary>
        public string DisplayName { get; }

        /// <summary>Earliest valid decimal year. Null when unknown (Quick mode for COF/DAT, or HDGM probe failure).</summary>
        public double? MinDate { get; }

        /// <summary>Latest valid decimal year (exclusive). Null when unknown.</summary>
        public double? MaxDate { get; }

        /// <summary>Optional free-form description (origin, notes).</summary>
        public string Description { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0} ({1}) {2}..{3} [{4}]",
                DisplayName, DetectedType, MinDate, MaxDate, FilePath);
        }
    }
}
