/****************************************************************************
 * File:            ModelDiscoveryCache.cs
 * Description:     Atomic read/write of .models.json discovery cache
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GeoMagSharp.Discovery
{
    /// <summary>
    /// Reads and writes the .models.json cache file atomically. Schema-versioned;
    /// any failure (missing, corrupt, wrong version, IO error) treats the cache as
    /// empty and invokes the supplied error callback.
    /// </summary>
    internal static class ModelDiscoveryCache
    {
        private const int CurrentSchemaVersion = 1;

        /// <summary>
        /// Loads the cache file. Returns an empty list if the file is missing,
        /// corrupt, or has an incompatible schema version. Invokes onError on
        /// any non-missing failure but never throws.
        /// </summary>
        public static List<ModelDiscoveryCacheEntry> TryLoad(string cacheFilePath,
            Action<string, Exception> onError)
        {
            if (string.IsNullOrEmpty(cacheFilePath)) return new List<ModelDiscoveryCacheEntry>();
            if (!File.Exists(cacheFilePath)) return new List<ModelDiscoveryCacheEntry>();

            try
            {
                string json = File.ReadAllText(cacheFilePath);
                if (string.IsNullOrWhiteSpace(json)) return new List<ModelDiscoveryCacheEntry>();

                var jo = JObject.Parse(json);
                int schema = jo["schemaVersion"]?.Value<int>() ?? 0;
                if (schema != CurrentSchemaVersion)
                {
                    return new List<ModelDiscoveryCacheEntry>();
                }

                var entriesToken = jo["entries"];
                if (entriesToken == null || entriesToken.Type != JTokenType.Array)
                {
                    return new List<ModelDiscoveryCacheEntry>();
                }

                var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.DateTime };
                var list = entriesToken.ToObject<List<ModelDiscoveryCacheEntry>>(JsonSerializer.Create(settings));
                return list ?? new List<ModelDiscoveryCacheEntry>();
            }
            catch (Exception ex)
            {
                onError?.Invoke(cacheFilePath, ex);
                return new List<ModelDiscoveryCacheEntry>();
            }
        }

        /// <summary>
        /// Atomically writes the cache file: serialize to a sibling .tmp, then File.Move
        /// (overwrite) onto the target. Never throws; invokes onError on any IO failure.
        /// </summary>
        public static void Save(string cacheFilePath,
            IList<ModelDiscoveryCacheEntry> entries,
            Action<string, Exception> onError)
        {
            if (string.IsNullOrEmpty(cacheFilePath)) return;
            entries = entries ?? new List<ModelDiscoveryCacheEntry>();

            try
            {
                var payload = new
                {
                    schemaVersion = CurrentSchemaVersion,
                    generatedBy = "GeoMagSharp",
                    generatedAt = DateTime.UtcNow,
                    entries = entries
                };
                string json = JsonConvert.SerializeObject(payload, Formatting.Indented);

                string tempPath = cacheFilePath + ".tmp";

                // Write temp file. Using FileMode.Create truncates if a leftover .tmp exists.
                File.WriteAllText(tempPath, json);

                // Atomic-rename onto target. On Windows, File.Move with overwrite=true
                // is atomic at the NTFS layer.
#if NET48 || NETSTANDARD2_0
                if (File.Exists(cacheFilePath)) File.Delete(cacheFilePath);
                File.Move(tempPath, cacheFilePath);
#else
                File.Move(tempPath, cacheFilePath, overwrite: true);
#endif
            }
            catch (Exception ex)
            {
                onError?.Invoke(cacheFilePath, ex);
                // Best-effort cleanup of leftover temp file
                try
                {
                    string tempPath = cacheFilePath + ".tmp";
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { /* swallow */ }
            }
        }
    }
}
