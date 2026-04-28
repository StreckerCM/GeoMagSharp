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
    }
}
