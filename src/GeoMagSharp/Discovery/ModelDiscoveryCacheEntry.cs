/****************************************************************************
 * File:            ModelDiscoveryCacheEntry.cs
 * Description:     DTO for one entry in .models.json
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;

namespace GeoMagSharp.Discovery
{
    /// <summary>
    /// Single entry in the .models.json cache. Stores the file's invalidation key
    /// (size + UTC mtime) alongside the descriptor it produced. Mutable for JSON
    /// deserialization; not exposed publicly.
    /// </summary>
    internal class ModelDiscoveryCacheEntry
    {
        /// <summary>File path relative to the scanned folder. Allows the cache to follow folder renames.</summary>
        public string RelativePath { get; set; }

        /// <summary>File size in bytes at last scan.</summary>
        public long FileSize { get; set; }

        /// <summary>UTC last-write time at last scan.</summary>
        public DateTime FileLastWriteUtc { get; set; }

        // Mirrors of ModelDescriptor's fields (we don't serialize ModelDescriptor directly so
        // its public constructor stays minimal and the cache schema is independently versioned).

        /// <summary>Detected model type at last scan.</summary>
        public knownModels DetectedType { get; set; }

        /// <summary>Display name at last scan.</summary>
        public string DisplayName { get; set; }

        /// <summary>Min date at last scan, null if unknown.</summary>
        public double? MinDate { get; set; }

        /// <summary>Max date at last scan, null if unknown.</summary>
        public double? MaxDate { get; set; }

        /// <summary>Optional description carried through.</summary>
        public string Description { get; set; }

        // Tier 1 metadata (#31, schema v3). Null for entries written by older
        // schema versions; null on disk for formats where the field is not
        // available (e.g. ReleaseDate is null for IGRF/DGRF, altitude bounds
        // are null for WMM/WMMHR).

        /// <summary>Main field spherical harmonic degree, null if unknown.</summary>
        public int? MaxDegree { get; set; }

        /// <summary>Secular variation degree, null if unknown or not applicable.</summary>
        public int? SecularVariationDegree { get; set; }

        /// <summary>Lower altitude validity bound in km, null if unknown.</summary>
        public double? MinAltitudeKm { get; set; }

        /// <summary>Upper altitude validity bound in km, null if unknown.</summary>
        public double? MaxAltitudeKm { get; set; }

        /// <summary>Model release date, null if unknown or not present in file.</summary>
        public DateTime? ReleaseDate { get; set; }
    }
}
