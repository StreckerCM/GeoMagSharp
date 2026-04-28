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
                MaxDate = 2030.0
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
