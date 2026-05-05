/****************************************************************************
 * File:            ModelDescriptor.cs
 * Description:     Immutable snapshot of a discovered magnetic model file
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/geomagsharp
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
        /// <param name="maxDegree">Main field spherical harmonic degree, or null if not extracted.</param>
        /// <param name="secularVariationDegree">Secular variation degree, or null if not applicable / not extracted.</param>
        /// <param name="minAltitudeKm">Lower altitude validity bound (km MSL), or null if not specified.</param>
        /// <param name="maxAltitudeKm">Upper altitude validity bound (km MSL), or null if not specified.</param>
        /// <param name="releaseDate">Date the model was published (distinct from validity range), or null if not extracted.</param>
        /// <param name="epochCount">Number of distinct coefficient epochs in the model file. 1 for single-epoch models (WMM, WMMHR, EMM, BGGM, HDGM); the count of epoch header lines for multi-epoch IGRF/DGRF files. Null if not extracted.</param>
        public ModelDescriptor(
            string filePath,
            knownModels detectedType,
            string displayName,
            double? minDate,
            double? maxDate,
            string description = null,
            int? maxDegree = null,
            int? secularVariationDegree = null,
            double? minAltitudeKm = null,
            double? maxAltitudeKm = null,
            DateTime? releaseDate = null,
            int? epochCount = null)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            DetectedType = detectedType;
            DisplayName = displayName ?? string.Empty;
            MinDate = minDate;
            MaxDate = maxDate;
            Description = description ?? string.Empty;
            MaxDegree = maxDegree;
            SecularVariationDegree = secularVariationDegree;
            MinAltitudeKm = minAltitudeKm;
            MaxAltitudeKm = maxAltitudeKm;
            ReleaseDate = releaseDate;
            EpochCount = epochCount;
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

        /// <summary>
        /// Maximum spherical harmonic degree of the main field, or null if not extracted.
        /// For multi-epoch IGRF/DGRF files this is the degree of the latest epoch
        /// (e.g. 13 for IGRF14's 2025 epoch, 10 for older epochs).
        /// </summary>
        public int? MaxDegree { get; }

        /// <summary>
        /// Maximum spherical harmonic degree of the secular variation, or null if
        /// not applicable / not extracted. For IGRF this typically differs from the
        /// main field degree (e.g. 8 for IGRF14's 2025 epoch with main degree 13).
        /// </summary>
        public int? SecularVariationDegree { get; }

        /// <summary>
        /// Lower altitude validity bound in km above mean sea level, or null when
        /// the file does not specify. IGRF/DGRF .COF headers carry this; WMM .COF
        /// headers do not (the WMM technical report states 0–850 km but the value
        /// is not in the file).
        /// </summary>
        public double? MinAltitudeKm { get; }

        /// <summary>Upper altitude validity bound in km above mean sea level, or null when not specified.</summary>
        public double? MaxAltitudeKm { get; }

        /// <summary>
        /// Date the model was published, or null when not extracted. Distinct from
        /// validity range — a model published in late 2024 may have a 2025-2030
        /// validity range. WMM/WMMHR .COF files carry this on the first line; IGRF/DGRF
        /// typically do not.
        /// </summary>
        public DateTime? ReleaseDate { get; }

        /// <summary>
        /// Number of distinct coefficient epochs in the model file, or null if
        /// not extracted. 1 for single-epoch models (WMM, WMMHR, EMM, BGGM, HDGM —
        /// these encode time evolution via secular variation rather than discrete
        /// epoch snapshots). For multi-epoch IGRF/DGRF .COF files, this is the
        /// number of 5-year epoch header lines (e.g. 26 for IGRF14 covering
        /// 1900–2030 in 5-year steps).
        /// </summary>
        public int? EpochCount { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0} ({1}) {2}..{3} [{4}]",
                DisplayName, DetectedType, MinDate, MaxDate, FilePath);
        }
    }
}
