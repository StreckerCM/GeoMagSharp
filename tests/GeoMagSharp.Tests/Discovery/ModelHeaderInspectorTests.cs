/****************************************************************************
 * File:            ModelHeaderInspectorTests.cs
 * Description:     Functional tests for ModelHeaderInspector
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.Discovery;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class ModelHeaderInspectorTests
    {
        private static string FixturesDir => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Discovery", "Fixtures");

        private static string Fixture(string name) => Path.Combine(FixturesDir, name);

        [TestMethod]
        public void Inspect_ValidWmmCof_ReturnsWMMTypeAndYear()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("WMM2025_sample.COF"));
            Assert.AreEqual(knownModels.WMM, d.DetectedType);
            Assert.AreEqual(2025.0, d.MinDate);
            Assert.AreEqual(2030.0, d.MaxDate);
        }

        [TestMethod]
        public void Inspect_ValidIgrfCof_ReturnsIGRFTypeAndYear()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("IGRF14_sample.COF"));
            Assert.AreEqual(knownModels.IGRF, d.DetectedType);
            Assert.AreEqual(2025.0, d.MinDate);
        }

        [TestMethod]
        public void Inspect_ValidEmmCof_ReturnsEMMType()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("EMM_sample.COF"));
            Assert.AreEqual(knownModels.EMM, d.DetectedType);
        }

        [TestMethod]
        public void Inspect_CorruptHeader_ReturnsNoneType()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("corrupt_header.COF"));
            Assert.AreEqual(knownModels.NONE, d.DetectedType);
            Assert.IsNull(d.MinDate);
            Assert.IsNull(d.MaxDate);
        }

        [TestMethod]
        public void Inspect_EmptyFile_ReturnsNoneType()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("empty.COF"));
            Assert.AreEqual(knownModels.NONE, d.DetectedType);
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void Inspect_FileNotFound_Throws()
        {
            ModelHeaderInspector.Inspect(Path.Combine(FixturesDir, "nonexistent.COF"));
        }

        [TestMethod]
        public void Inspect_DatExtension_DoesNotThrow()
        {
            // The inspector should accept .DAT files using the same first-line peek; we don't
            // ship a .DAT fixture (DAT format is an integer year on line 1) so just verify
            // a synthetic .DAT path with a valid first line works.
            var dat = Path.Combine(FixturesDir, "synthetic.DAT");
            try
            {
                File.WriteAllText(dat, "1900\n2025\n");
                var d = ModelHeaderInspector.Inspect(dat);
                Assert.IsNotNull(d);
            }
            finally
            {
                if (File.Exists(dat)) File.Delete(dat);
            }
        }

        [TestMethod]
        public void Inspect_FilePath_PopulatedFromInput()
        {
            var path = Fixture("WMM2025_sample.COF");
            var d = ModelHeaderInspector.Inspect(path);
            Assert.AreEqual(path, d.FilePath);
        }

        [TestMethod]
        public void Inspect_Igrf14MultiEpoch_DisplayNameIsLatestEpochLabel()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("IGRF14_multiepoch.COF"));
            Assert.AreEqual(knownModels.IGRF, d.DetectedType);
            Assert.AreEqual("IGRF2025", d.DisplayName);
            Assert.AreEqual(1900.0, d.MinDate);
            Assert.AreEqual(2030.0, d.MaxDate);
        }

        [TestMethod]
        public void Inspect_Igrf13MultiEpoch_DisplayNameIsLatestEpochLabel()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("IGRF13_multiepoch.COF"));
            Assert.AreEqual(knownModels.IGRF, d.DetectedType);
            Assert.AreEqual("IGRF2020", d.DisplayName);
            Assert.AreEqual(1900.0, d.MinDate);
            Assert.AreEqual(2025.0, d.MaxDate);
        }

        [TestMethod]
        public void Inspect_Igrf12MultiEpoch_DisplayNameIsLatestEpochLabel()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("IGRF12_multiepoch.COF"));
            Assert.AreEqual(knownModels.IGRF, d.DetectedType);
            Assert.AreEqual("IGRF2015", d.DisplayName);
            Assert.AreEqual(1900.0, d.MinDate);
            Assert.AreEqual(2020.0, d.MaxDate);
        }

        [TestMethod]
        public void Inspect_WmmCof_NotAffectedByMultiEpochScan()
        {
            // Regression: ensure single-epoch model files use the existing fast-path
            // (no multi-epoch scan, MaxDate = MinDate + 5).
            var d = ModelHeaderInspector.Inspect(Fixture("WMM2025_sample.COF"));
            Assert.AreEqual(knownModels.WMM, d.DetectedType);
            Assert.AreEqual(2025.0, d.MinDate);
            Assert.AreEqual(2030.0, d.MaxDate);
        }

        // ─── #31 Tier 1: richer metadata extraction ─────────────────────

        [TestMethod]
        public void Inspect_Igrf14MultiEpoch_PopulatesDegreeAndAltitude()
        {
            // Latest epoch in fixture is "IGRF2025  2025.00 13  8  0 2025.00 2030.00   -1.0  600.0"
            // Expected fields from #31 Tier 1: MaxDegree=13, SecularVariationDegree=8,
            // MinAltitudeKm=-1.0, MaxAltitudeKm=600.0
            var d = ModelHeaderInspector.Inspect(Fixture("IGRF14_multiepoch.COF"));
            Assert.AreEqual(13, d.MaxDegree, "Main field degree from latest epoch parts[2]");
            Assert.AreEqual(8, d.SecularVariationDegree, "SV degree from latest epoch parts[3]");
            Assert.AreEqual(-1.0, d.MinAltitudeKm);
            Assert.AreEqual(600.0, d.MaxAltitudeKm);
            Assert.IsNull(d.ReleaseDate, "IGRF/DGRF headers don't carry release date");
        }

        [TestMethod]
        public void Inspect_Wmm2025Sample_ParsesReleaseDate()
        {
            // Sample fixture has only the header line: "2025.0  WMM-2025  12/10/2024"
            // Expected: ReleaseDate = 2024-12-10. No coefficient lines, so MaxDegree is null.
            var d = ModelHeaderInspector.Inspect(Fixture("WMM2025_sample.COF"));
            Assert.IsNotNull(d.ReleaseDate);
            Assert.AreEqual(new System.DateTime(2024, 12, 10), d.ReleaseDate.Value);
            Assert.IsNull(d.MaxDegree, "Sample fixture has no coefficient lines to scan");
            Assert.IsNull(d.MinAltitudeKm, "WMM headers don't include altitude validity");
        }

        [TestMethod]
        public void Inspect_WmmWithCoefficients_ScansMaxDegreeFromCoefficientLines()
        {
            // Fixture has WMM header + coefficient rows up to n=12.
            // Expected: MaxDegree = 12 from scanning coefficient lines.
            var d = ModelHeaderInspector.Inspect(Fixture("WMM_with_coefficients.COF"));
            Assert.AreEqual(knownModels.WMM, d.DetectedType);
            Assert.AreEqual(12, d.MaxDegree, "Scanned from highest n in coefficient rows");
            Assert.AreEqual(new System.DateTime(2024, 12, 10), d.ReleaseDate.Value);
        }

        [TestMethod]
        public void Inspect_RealBundledIgrf14_PopulatesAltitudeAndDegree()
        {
            // DIAGNOSTIC: validate against the actual production-bundled IGRF14.COF
            // (the one shipped in the package and used by GUI consumers), not just
            // the synthetic fixture. If this passes but consumers still see null
            // altitude, the issue is downstream of the inspector.
            string realCof = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "coefficient", "IGRF14.COF");
            if (!File.Exists(realCof))
            {
                Assert.Inconclusive("Production IGRF14.COF not present at " + realCof);
                return;
            }
            var d = ModelHeaderInspector.Inspect(realCof);
            Assert.AreEqual(knownModels.IGRF, d.DetectedType);
            Assert.AreEqual(13, d.MaxDegree, "Real IGRF14.COF latest epoch degree");
            Assert.AreEqual(8, d.SecularVariationDegree, "Real IGRF14.COF latest epoch SV degree");
            Assert.AreEqual(-1.0, d.MinAltitudeKm);
            Assert.AreEqual(600.0, d.MaxAltitudeKm);
        }

        [TestMethod]
        public void Inspect_CorruptHeader_LeavesNewMetadataNull()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("corrupt_header.COF"));
            Assert.IsNull(d.MaxDegree);
            Assert.IsNull(d.SecularVariationDegree);
            Assert.IsNull(d.MinAltitudeKm);
            Assert.IsNull(d.MaxAltitudeKm);
            Assert.IsNull(d.ReleaseDate);
        }
    }
}
