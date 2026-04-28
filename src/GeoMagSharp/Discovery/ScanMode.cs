/****************************************************************************
 * File:            ScanMode.cs
 * Description:     Discovery scan-depth selector
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

namespace GeoMagSharp
{
    /// <summary>
    /// Scan depth for <see cref="ModelDiscovery.DiscoverModels(string, ModelDiscoveryOptions)"/>.
    /// </summary>
    public enum ScanMode
    {
        /// <summary>
        /// Identify files by extension and filename only. Fast (filesystem-stat-only).
        /// <see cref="ModelDescriptor.DetectedType"/> remains <see cref="knownModels.NONE"/> for
        /// COF/DAT files, and <see cref="ModelDescriptor.MinDate"/>/<see cref="ModelDescriptor.MaxDate"/>
        /// are null.
        /// </summary>
        Quick,

        /// <summary>
        /// Open each candidate to read header (COF/DAT) or probe via LoadLibraryEx (HDGM .dll).
        /// Slower but populates <see cref="ModelDescriptor.DetectedType"/>, display name, and date range.
        /// </summary>
        Full
    }
}
