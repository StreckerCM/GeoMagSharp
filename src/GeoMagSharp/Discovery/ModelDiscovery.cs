/****************************************************************************
 * File:            ModelDiscovery.cs
 * Description:     Public discovery API: DiscoverModels, DescribeFile
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GeoMagSharp.Discovery;
using GeoMagSharp.HDGM;

namespace GeoMagSharp
{
    /// <summary>
    /// Library-level discovery API for enumerating loadable model files in a folder.
    /// Discovery is identification-only; consumers call <see cref="GeoMag.LoadModel(string)"/>
    /// when they actually want to use a model.
    /// </summary>
    public static class ModelDiscovery
    {
        /// <summary>Convenience overload: <see cref="ScanMode.Full"/>, no recursion, no cache.</summary>
        public static IEnumerable<ModelDescriptor> DiscoverModels(string folderPath)
        {
            return DiscoverModels(folderPath, new ModelDiscoveryOptions());
        }

        /// <summary>
        /// Enumerates loadable model files in folderPath. Returns empty if the folder does
        /// not exist. Per-file failures invoke options.OnError but do not stop enumeration.
        /// </summary>
        public static IEnumerable<ModelDescriptor> DiscoverModels(string folderPath, ModelDiscoveryOptions options)
        {
            if (folderPath == null) throw new ArgumentNullException(nameof(folderPath));
            if (options == null) throw new ArgumentNullException(nameof(options));

            return DiscoverModelsImpl(folderPath, options);
        }

        private static IEnumerable<ModelDescriptor> DiscoverModelsImpl(string folderPath, ModelDiscoveryOptions options)
        {
            if (!Directory.Exists(folderPath)) yield break;

            var searchOption = options.Recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            string cacheFilePath = options.UseCache
                ? Path.Combine(folderPath, options.CacheFileName ?? ".models.json")
                : null;
            string cacheFileFullPath = cacheFilePath != null ? Path.GetFullPath(cacheFilePath) : null;

            // Load existing cache (empty if missing/corrupt/wrong-version)
            Dictionary<string, ModelDiscoveryCacheEntry> cachedByRelPath =
                new Dictionary<string, ModelDiscoveryCacheEntry>(StringComparer.OrdinalIgnoreCase);
            if (cacheFilePath != null)
            {
                foreach (var entry in ModelDiscoveryCache.TryLoad(cacheFilePath, options.OnError))
                {
                    if (!string.IsNullOrEmpty(entry.RelativePath))
                        cachedByRelPath[entry.RelativePath] = entry;
                }
            }

            // We collect entries to write back so the cache reflects the live folder.
            var liveEntries = options.UseCache ? new List<ModelDiscoveryCacheEntry>() : null;

            foreach (string filePath in Directory.EnumerateFiles(folderPath, "*.*", searchOption))
            {
                options.CancellationToken.ThrowIfCancellationRequested();

                if (cacheFileFullPath != null &&
                    string.Equals(Path.GetFullPath(filePath), cacheFileFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ModelDescriptor descriptor = null;
                ModelDiscoveryCacheEntry cacheEntryToWrite = null;

                try
                {
                    string relPath = MakeRelativePath(folderPath, filePath);
                    var fileInfo = new FileInfo(filePath);

                    ModelDiscoveryCacheEntry cached;
                    bool cacheHit = options.UseCache
                        && cachedByRelPath.TryGetValue(relPath, out cached)
                        && cached.FileSize == fileInfo.Length
                        && AreUtcTimestampsEqual(cached.FileLastWriteUtc, fileInfo.LastWriteTimeUtc);

                    if (cacheHit)
                    {
                        var c = cachedByRelPath[relPath];
                        descriptor = new ModelDescriptor(filePath, c.DetectedType, c.DisplayName,
                            c.MinDate, c.MaxDate, c.Description);
                        cacheEntryToWrite = c;
                    }
                    else
                    {
                        descriptor = ClassifyFile(filePath, options);
                        if (descriptor != null && options.UseCache)
                        {
                            cacheEntryToWrite = new ModelDiscoveryCacheEntry
                            {
                                RelativePath = relPath,
                                FileSize = fileInfo.Length,
                                FileLastWriteUtc = DateTime.SpecifyKind(fileInfo.LastWriteTimeUtc, DateTimeKind.Utc),
                                DetectedType = descriptor.DetectedType,
                                DisplayName = descriptor.DisplayName,
                                MinDate = descriptor.MinDate,
                                MaxDate = descriptor.MaxDate,
                                Description = descriptor.Description
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    options.OnError?.Invoke(filePath, ex);
                    continue;
                }

                if (descriptor != null)
                {
                    if (liveEntries != null && cacheEntryToWrite != null)
                        liveEntries.Add(cacheEntryToWrite);
                    yield return descriptor;
                }
            }

            if (cacheFilePath != null && liveEntries != null)
            {
                ModelDiscoveryCache.Save(cacheFilePath, liveEntries, options.OnError);
            }
        }

        private static string MakeRelativePath(string folderPath, string filePath)
        {
            string folderFull = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fileFull = Path.GetFullPath(filePath);
            if (fileFull.StartsWith(folderFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return fileFull.Substring(folderFull.Length + 1);
            return Path.GetFileName(filePath);
        }

        private static bool AreUtcTimestampsEqual(DateTime a, DateTime b)
        {
            // Truncate to nearest second to avoid sub-second precision differences across filesystems.
            return DateTimeToUnixSeconds(a) == DateTimeToUnixSeconds(b);
        }

        private static long DateTimeToUnixSeconds(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return (utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks / TimeSpan.TicksPerSecond;
        }

        private static ModelDescriptor ClassifyFile(string filePath, ModelDiscoveryOptions options)
        {
            string ext = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext)) return null;
            string extUpper = ext.ToUpperInvariant();

            if (extUpper == ".COF" || extUpper == ".DAT")
            {
                if (options.Mode == ScanMode.Quick)
                {
                    return new ModelDescriptor(filePath, knownModels.NONE,
                        Path.GetFileNameWithoutExtension(filePath), null, null);
                }
                return ModelHeaderInspector.Inspect(filePath);
            }

            if (extUpper == ".DLL" && ModelPathDetector.IsHdgmPath(filePath))
            {
                if (options.Mode == ScanMode.Quick)
                {
                    return new ModelDescriptor(filePath, knownModels.HDGM,
                        BuildHdgmDisplayName(filePath), null, null);
                }
                var (minDate, maxDate) = HdgmDateProbe.Probe(
                    path => CreateRealInvokerOrNull(path), filePath);
                return new ModelDescriptor(filePath, knownModels.HDGM,
                    BuildHdgmDisplayName(filePath), minDate, maxDate);
            }

            return null;
        }

        /// <summary>
        /// Performs a Full-mode inspection on a single file and returns its descriptor.
        /// Returns null if the file's extension is not a recognized model format.
        /// </summary>
        /// <exception cref="ArgumentNullException">filePath is null.</exception>
        /// <exception cref="GeoMagExceptionFileNotFound">File does not exist.</exception>
        public static ModelDescriptor DescribeFile(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new GeoMagExceptionFileNotFound("Error: The file '" + filePath + "' was not found");

            string ext = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext)) return null;
            string extUpper = ext.ToUpperInvariant();

            if (extUpper == ".COF" || extUpper == ".DAT")
            {
                return ModelHeaderInspector.Inspect(filePath);
            }

            if (extUpper == ".DLL" && ModelPathDetector.IsHdgmPath(filePath))
            {
                var result = HdgmDateProbe.Probe(
                    path => CreateRealInvokerOrNull(path), filePath);
                return new ModelDescriptor(filePath, knownModels.HDGM,
                    BuildHdgmDisplayName(filePath), result.minDate, result.maxDate);
            }

            return null;
        }

        // ----- private helpers -----

        private static INativeHdgmInvoker CreateRealInvokerOrNull(string dllPath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
            try { return new LoadLibraryHdgmInvoker(dllPath); }
            catch { return null; }
        }

        private static string BuildHdgmDisplayName(string dllPath)
        {
            int? year = HdgmDateProbe.ExtractYearFromFilename(dllPath);
            return year.HasValue ? "HDGM" + year.Value : "HDGM";
        }
    }
}
