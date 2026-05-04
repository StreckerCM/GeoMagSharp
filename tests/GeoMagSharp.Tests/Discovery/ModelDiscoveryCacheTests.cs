/****************************************************************************
 * File:            ModelDiscoveryCacheTests.cs
 * Description:     Unit tests for ModelDiscoveryCache (atomic read/write)
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.Discovery;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class ModelDiscoveryCacheTests
    {
        private string _tempDir;
        private string _cacheFile;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GeoMagSharpTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _cacheFile = Path.Combine(_tempDir, ".models.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        private static List<ModelDiscoveryCacheEntry> SampleEntries() => new List<ModelDiscoveryCacheEntry>
        {
            new ModelDiscoveryCacheEntry
            {
                RelativePath = "WMM.COF",
                FileSize = 4647,
                FileLastWriteUtc = new DateTime(2026, 3, 31, 4, 42, 0, DateTimeKind.Utc),
                DetectedType = knownModels.WMM,
                DisplayName = "WMM2025",
                MinDate = 2025.0,
                MaxDate = 2030.0,
                MaxDegree = 12,
                ReleaseDate = new DateTime(2024, 11, 13, 0, 0, 0, DateTimeKind.Utc)
            },
            new ModelDiscoveryCacheEntry
            {
                RelativePath = "hdgm2019-64.dll",
                FileSize = 7345664,
                FileLastWriteUtc = new DateTime(2018, 11, 13, 0, 0, 0, DateTimeKind.Utc),
                DetectedType = knownModels.HDGM,
                DisplayName = "HDGM2019",
                MinDate = 1900.0,
                MaxDate = 2021.0
            }
        };

        [TestMethod]
        public void Save_ThenLoad_RoundTripsAllEntries()
        {
            ModelDiscoveryCache.Save(_cacheFile, SampleEntries(), null);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(2, loaded.Count);
            Assert.AreEqual("WMM.COF", loaded[0].RelativePath);
            Assert.AreEqual(knownModels.WMM, loaded[0].DetectedType);
            Assert.AreEqual(2025.0, loaded[0].MinDate);
            Assert.AreEqual("hdgm2019-64.dll", loaded[1].RelativePath);
            Assert.AreEqual(knownModels.HDGM, loaded[1].DetectedType);
        }

        [TestMethod]
        public void Load_MissingFile_ReturnsEmptyList()
        {
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(0, loaded.Count);
        }

        [TestMethod]
        public void Load_CorruptJson_ReturnsEmptyList_FiresOnError()
        {
            File.WriteAllText(_cacheFile, "this is { not valid JSON");
            string capturedPath = null;
            Exception capturedEx = null;
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, (p, ex) => { capturedPath = p; capturedEx = ex; });
            Assert.AreEqual(0, loaded.Count);
            Assert.AreEqual(_cacheFile, capturedPath);
            Assert.IsNotNull(capturedEx);
        }

        [TestMethod]
        public void Load_WrongSchemaVersion_ReturnsEmptyList()
        {
            File.WriteAllText(_cacheFile, "{ \"schemaVersion\": 999, \"entries\": [] }");
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(0, loaded.Count);
        }

        [TestMethod]
        public void Save_ThenLoad_RoundTripsTier1Metadata()
        {
            // Regression: ModelDiscoveryCacheEntry must carry the Tier 1 fields
            // (#31) added in 1.7.2. Without these, cache hits would reconstruct
            // descriptors with all-null new fields on the second discovery pass,
            // silently losing degree/altitude/release-date data.
            ModelDiscoveryCache.Save(_cacheFile, SampleEntries(), null);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);

            Assert.AreEqual(12, loaded[0].MaxDegree, "WMM MaxDegree");
            Assert.IsTrue(loaded[0].ReleaseDate.HasValue, "WMM ReleaseDate populated");
            Assert.AreEqual(new DateTime(2024, 11, 13, 0, 0, 0, DateTimeKind.Utc).Ticks,
                            loaded[0].ReleaseDate.Value.ToUniversalTime().Ticks,
                            "WMM ReleaseDate round-trips as UTC");

            // HDGM entry has no Tier 1 fields populated (Tier 3 deferred);
            // they must round-trip as null, not as zero / DateTime.MinValue.
            Assert.IsFalse(loaded[1].MaxDegree.HasValue, "HDGM MaxDegree stays null");
            Assert.IsFalse(loaded[1].MinAltitudeKm.HasValue, "HDGM MinAltitudeKm stays null");
            Assert.IsFalse(loaded[1].ReleaseDate.HasValue, "HDGM ReleaseDate stays null");
        }

        [TestMethod]
        public void Load_LegacyV2Cache_DiscardedAfterSchemaBumpToV3OrLater()
        {
            // Cache shape as written by GeoMagSharp 1.7.1 (schema v2) lacks the
            // Tier 1 metadata fields added in 1.7.2 (#31). After the v3+ schema
            // bumps, TryLoad must discard v2 caches verbatim so the new
            // classifier runs and populates degree/altitude/release-date.
            string v2Json = "{\"schemaVersion\":2,\"entries\":[" +
                "{\"RelativePath\":\"WMM.COF\",\"FileSize\":4647," +
                "\"FileLastWriteUtc\":\"2026-03-31T04:42:00Z\"," +
                "\"DetectedType\":1,\"DisplayName\":\"WMM2025\"," +
                "\"MinDate\":2025.0,\"MaxDate\":2030.0,\"Description\":\"\"}]}";
            File.WriteAllText(_cacheFile, v2Json);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(0, loaded.Count, "v2 caches must be discarded after schema bump");
        }

        [TestMethod]
        public void Load_LegacyV3Cache_DiscardedAfterSchemaBumpToV4()
        {
            // v3 entries cached HDGM descriptors with MaxDegree=null because
            // the Tier 3 HdgmModelMetadata lookup didn't exist yet. After the
            // v4 bump, TryLoad must discard v3 caches so HDGM entries get
            // re-classified with their CIRES-published crustal degree.
            string v3Json = "{\"schemaVersion\":3,\"entries\":[" +
                "{\"RelativePath\":\"hdgm2019.dll\",\"FileSize\":7345664," +
                "\"FileLastWriteUtc\":\"2018-11-13T00:00:00Z\"," +
                "\"DetectedType\":7,\"DisplayName\":\"HDGM2019\"," +
                "\"MinDate\":1900.0,\"MaxDate\":2020.0,\"Description\":\"\"}]}";
            File.WriteAllText(_cacheFile, v3Json);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(0, loaded.Count, "v3 caches must be discarded after schema bump to v4");
        }

        [TestMethod]
        public void Load_LegacyV1Cache_DiscardedAfterSchemaBumpToV2()
        {
            // Cache shape as written by GeoMagSharp 1.7.0 and earlier may contain
            // stale classifier output (e.g. DisplayName="IGRF00" for IGRF14.COF
            // before the multi-epoch fix in #24). After the v2 schema bump in #26,
            // TryLoad must discard v1 caches verbatim - even if file mtime/size
            // for individual entries still matches - so the new classifier runs.
            string v1Json = "{\"schemaVersion\":1,\"entries\":[" +
                "{\"RelativePath\":\"IGRF14.COF\",\"FileSize\":157950," +
                "\"FileLastWriteUtc\":\"2026-04-26T04:47:48Z\"," +
                "\"DetectedType\":3,\"DisplayName\":\"IGRF00\"," +
                "\"MinDate\":1900.0,\"MaxDate\":1905.0,\"Description\":\"\"}]}";
            File.WriteAllText(_cacheFile, v1Json);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(0, loaded.Count, "v1 caches must be discarded after schema bump to v2");
        }

        [TestMethod]
        public void Load_EmptyJsonObject_ReturnsEmptyList()
        {
            File.WriteAllText(_cacheFile, "{}");
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(0, loaded.Count);
        }

        [TestMethod]
        public void Save_PreservesEntryOrder()
        {
            var entries = SampleEntries();
            ModelDiscoveryCache.Save(_cacheFile, entries, null);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual("WMM.COF", loaded[0].RelativePath);
            Assert.AreEqual("hdgm2019-64.dll", loaded[1].RelativePath);
        }

        [TestMethod]
        public void Save_ToReadOnlyFolder_DoesNotThrow_FiresOnError()
        {
            // Use an obviously-invalid cache path inside a non-existent subdir
            string badPath = Path.Combine(_tempDir, "no_such_subdir", ".models.json");
            string capturedPath = null;
            ModelDiscoveryCache.Save(badPath, SampleEntries(), (p, ex) => { capturedPath = p; });
            Assert.AreEqual(badPath, capturedPath);
        }

        [TestMethod]
        public void Save_AtomicallyReplacesExistingFile()
        {
            // Pre-existing cache with one entry
            File.WriteAllText(_cacheFile, "{\"schemaVersion\":1,\"entries\":[]}");
            ModelDiscoveryCache.Save(_cacheFile, SampleEntries(), null);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(2, loaded.Count);
        }

        [TestMethod]
        public void Save_NoTempFileLeftBehindOnSuccess()
        {
            ModelDiscoveryCache.Save(_cacheFile, SampleEntries(), null);
            Assert.IsTrue(File.Exists(_cacheFile));
            Assert.IsFalse(File.Exists(_cacheFile + ".tmp"));
        }

        [TestMethod]
        public void TimestampsRoundTripAsUtc()
        {
            var entries = SampleEntries();
            ModelDiscoveryCache.Save(_cacheFile, entries, null);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(DateTimeKind.Utc, loaded[0].FileLastWriteUtc.Kind);
            Assert.AreEqual(entries[0].FileLastWriteUtc.Ticks, loaded[0].FileLastWriteUtc.Ticks);
        }
    }
}
