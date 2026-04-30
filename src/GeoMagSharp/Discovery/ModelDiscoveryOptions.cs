/****************************************************************************
 * File:            ModelDiscoveryOptions.cs
 * Description:     Options for ModelDiscovery.DiscoverModels
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Threading;

namespace GeoMagSharp
{
    /// <summary>
    /// Options for <see cref="ModelDiscovery.DiscoverModels(string, ModelDiscoveryOptions)"/>.
    /// All fields have safe defaults; instances may be mutated freely before passing.
    /// </summary>
    public class ModelDiscoveryOptions
    {
        /// <summary>Scan depth. Default <see cref="ScanMode.Full"/> (header peek + HDGM probe).</summary>
        public ScanMode Mode { get; set; } = ScanMode.Full;

        /// <summary>Recurse subdirectories. Default false.</summary>
        public bool Recursive { get; set; } = false;

        /// <summary>
        /// If true, read .models.json from the scanned folder, validate cached entries against
        /// current mtime/size, deep-scan only new or changed files, and write the refreshed cache
        /// back at the end. Default false.
        /// </summary>
        public bool UseCache { get; set; } = false;

        /// <summary>Cache filename inside the scanned folder. Default ".models.json".</summary>
        public string CacheFileName { get; set; } = ".models.json";

        /// <summary>
        /// Cancellation token. Checked once per file. Default <see cref="CancellationToken.None"/>.
        /// Cancellation aborts enumeration before the post-walk cache rewrite, so a partial
        /// scan will not overwrite the existing <c>.models.json</c>.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Callback invoked when an individual file or cache operation fails. Receives the path
        /// that triggered the failure and the exception. Default null (silent).
        /// </summary>
        public Action<string, Exception> OnError { get; set; }
    }
}
