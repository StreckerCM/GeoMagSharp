/****************************************************************************
 * File:            ModelDiscoveryTests.cs
 * Description:     End-to-end functional tests for ModelDiscovery
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class ModelDiscoveryTests
    {
        // Task 10 starter tests

        [TestMethod]
        public void DescribeFile_NewWmmFile_ReturnsFreshDescriptor()
        {
            using (var fx = new TestFolderFixture())
            {
                var path = fx.CopyFixture("WMM2025_sample.COF");
                var d = ModelDiscovery.DescribeFile(path);
                Assert.IsNotNull(d);
                Assert.AreEqual(knownModels.WMM, d.DetectedType);
                Assert.AreEqual(2025.0, d.MinDate);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DescribeFile_NullPath_Throws()
        {
            ModelDiscovery.DescribeFile(null);
        }

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionFileNotFound))]
        public void DescribeFile_FileNotFound_Throws()
        {
            ModelDiscovery.DescribeFile(@"C:\definitely_not_real\nope.COF");
        }

        [TestMethod]
        public void DescribeFile_UnknownExtension_ReturnsNull()
        {
            using (var fx = new TestFolderFixture())
            {
                var path = fx.WriteFile("garbage.xyz", "anything");
                var d = ModelDiscovery.DescribeFile(path);
                Assert.IsNull(d);
            }
        }

        // Task 11 — folder enumeration without cache

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DiscoverModels_NullFolderPath_Throws()
        {
            ModelDiscovery.DiscoverModels(null).ToList();
        }

        [TestMethod]
        public void DiscoverModels_FolderDoesNotExist_ReturnsEmpty()
        {
            var results = ModelDiscovery.DiscoverModels(@"C:\definitely_not_real_folder").ToList();
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void DiscoverModels_QuickMode_RecognizesCofAndDllByFilename()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                fx.WriteFile("hdgm2019-64.dll", new string('x', 32));
                fx.WriteFile("notes.txt", "irrelevant");

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath,
                    new ModelDiscoveryOptions { Mode = ScanMode.Quick }).ToList();

                Assert.AreEqual(2, results.Count);
                Assert.IsTrue(results.Any(d => d.FilePath.EndsWith("WMM.COF")));
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.HDGM));
            }
        }

        [TestMethod]
        public void DiscoverModels_QuickMode_CofDetectedTypeIsNone()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath,
                    new ModelDiscoveryOptions { Mode = ScanMode.Quick }).ToList();
                var cof = results.Single();
                Assert.AreEqual(knownModels.NONE, cof.DetectedType);
                Assert.IsNull(cof.MinDate);
            }
        }

        [TestMethod]
        public void DiscoverModels_FullMode_PopulatesCofMetadata()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath,
                    new ModelDiscoveryOptions { Mode = ScanMode.Full }).ToList();
                var cof = results.Single();
                Assert.AreEqual(knownModels.WMM, cof.DetectedType);
                Assert.AreEqual(2025.0, cof.MinDate);
                Assert.AreEqual(2030.0, cof.MaxDate);
            }
        }

        [TestMethod]
        public void DiscoverModels_FullMode_MixedFolder_HandlesAllCases()
        {
            // Full mode is strict: classifiable files appear in results, unclassifiable
            // ones (corrupt headers, empty .cof/.dat, non-model extensions) do not.
            // See #27 - prior versions returned NONE-typed descriptors for unclassifiable
            // .cof/.dat files, which leaked into consumer model lists as ghost entries.
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                fx.CopyFixture("IGRF14_sample.COF", "IGRF14.COF");
                fx.CopyFixture("corrupt_header.COF", "broken.COF");
                fx.WriteFile("notes.txt", "ignored");

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath).ToList();
                Assert.AreEqual(2, results.Count);
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.WMM));
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.IGRF));
                Assert.IsFalse(results.Any(d => d.DetectedType == knownModels.NONE),
                    "Full-mode discovery must not yield NONE-typed descriptors");
            }
        }

        [TestMethod]
        public void DiscoverModels_FullMode_EmptyCofExcluded()
        {
            // Regression for #27: a 0-byte .cof file in the scanned folder must not
            // appear in DiscoverModels output. Previously yielded a descriptor with
            // DetectedType=NONE and DisplayName="bad", which downstream consumers
            // (e.g. WinForms combobox bindings) couldn't easily distinguish from valid
            // models, allowing unloadable entries to be presented as user-selectable.
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                fx.CopyFixture("empty.COF", "bad.cof");

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath).ToList();
                Assert.AreEqual(1, results.Count, "only the WMM file should be discovered");
                Assert.IsFalse(results.Any(d => Path.GetFileName(d.FilePath)
                    .Equals("bad.cof", StringComparison.OrdinalIgnoreCase)),
                    "empty/unclassifiable .cof file must not appear in results");
            }
        }

        [TestMethod]
        public void DiscoverModels_NonHdgmDllSkipped()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.WriteFile("randomlib.dll", new string('x', 16));
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath).ToList();
                Assert.AreEqual(0, results.Count);
            }
        }

        [TestMethod]
        public void DiscoverModels_UnknownExtension_Skipped()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.WriteFile("notes.txt", "not a model");
                fx.WriteFile("readme.md", "ignored");
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath).ToList();
                Assert.AreEqual(0, results.Count);
            }
        }

        [TestMethod]
        public void DiscoverModels_Recursive_TraversesSubfolders()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var sub = fx.CreateSubdir("nested");
                File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Discovery", "Fixtures", "IGRF14_sample.COF"),
                    Path.Combine(sub, "IGRF14.COF"));

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath,
                    new ModelDiscoveryOptions { Recursive = true }).ToList();
                Assert.AreEqual(2, results.Count);
            }
        }

        [TestMethod]
        public void DiscoverModels_NonRecursive_StopsAtTopLevel()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var sub = fx.CreateSubdir("nested");
                File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Discovery", "Fixtures", "IGRF14_sample.COF"),
                    Path.Combine(sub, "IGRF14.COF"));

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath).ToList();
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(knownModels.WMM, results[0].DetectedType);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public void DiscoverModels_CancellationTokenTriggered_Throws()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                using (var cts = new CancellationTokenSource())
                {
                    cts.Cancel();
                    ModelDiscovery.DiscoverModels(fx.FolderPath,
                        new ModelDiscoveryOptions { CancellationToken = cts.Token }).ToList();
                }
            }
        }

        // Task 12 — UseCache flow

        [TestMethod]
        public void DiscoverModels_UseCache_FirstRun_WritesCacheFile()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                ModelDiscovery.DiscoverModels(fx.FolderPath,
                    new ModelDiscoveryOptions { UseCache = true }).ToList();
                Assert.IsTrue(File.Exists(Path.Combine(fx.FolderPath, ".models.json")));
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_SecondRunUnchangedFolder_HitsCache()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                // Second run; if cache works, results match first run
                var second = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.AreEqual(1, second.Count);
                Assert.AreEqual(knownModels.WMM, second[0].DetectedType);
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_FileMtimeChanged_RescansThatFile()
        {
            using (var fx = new TestFolderFixture())
            {
                var p = fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();

                // Touch mtime: rewrite the file with same content but new timestamp
                File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddMinutes(1));

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(knownModels.WMM, results[0].DetectedType);
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_NewFileAdded_DeepScansOnlyNewFile()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();

                fx.CopyFixture("IGRF14_sample.COF", "IGRF14.COF");
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.AreEqual(2, results.Count);
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.WMM));
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.IGRF));
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_FileDeleted_DropsFromCacheOnNextScan()
        {
            using (var fx = new TestFolderFixture())
            {
                var p1 = fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                fx.CopyFixture("IGRF14_sample.COF", "IGRF14.COF");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();

                File.Delete(p1);
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(knownModels.IGRF, results[0].DetectedType);
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_CorruptCache_RecoversByRewriting()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                File.WriteAllText(Path.Combine(fx.FolderPath, ".models.json"), "garbage{");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(knownModels.WMM, results[0].DetectedType);
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_CacheFileNotInResults()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.IsFalse(results.Any(d => d.FilePath.EndsWith(".models.json")));
            }
        }
    }
}
