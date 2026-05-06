/****************************************************************************
 * File:            HdgmModelMetadataTests.cs
 * Description:     Unit tests for HdgmModelMetadata filename-keyed lookup
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp.Discovery;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class HdgmModelMetadataTests
    {
        // CIRES-published crustal degrees per HDGM era:
        //   2017–2020 → 720
        //   2021–2025 → 790
        //   2026      → 1040
        // https://geomag.colorado.edu/geomagnetic-and-electric-field-models

        [DataTestMethod]
        [DataRow("hdgm2017.dll", 720)]
        [DataRow("hdgm2018.dll", 720)]
        [DataRow("hdgm2019.dll", 720)]
        [DataRow("hdgm2020.dll", 720)]
        [DataRow("HDGM2021.dll", 790)]
        [DataRow("hdgm2022.dll", 790)]
        [DataRow("hdgm2023.dll", 790)]
        [DataRow("hdgm2024.dll", 790)]
        [DataRow("hdgm2025.dll", 790)]
        [DataRow("hdgm2026.dll", 1040)]
        public void GetMaxDegreeFromFilename_KnownVersion_ReturnsCiresPublishedDegree(string filename, int expectedDegree)
        {
            int? actual = HdgmModelMetadata.GetMaxDegreeFromFilename(filename);
            Assert.AreEqual(expectedDegree, actual);
        }

        [DataTestMethod]
        [DataRow("hdgm2019-64.dll", 720)]
        [DataRow("HDGM2019-RT.dll", 720)]
        [DataRow("hdgm2026-rt-64.dll", 1040)]
        public void GetMaxDegreeFromFilename_VariantSuffix_ResolvesToBaseYear(string filename, int expectedDegree)
        {
            // RT variants and bitness-suffixed filenames share the same crustal
            // degree as their base year — we extract the year from the filename
            // and ignore everything after.
            int? actual = HdgmModelMetadata.GetMaxDegreeFromFilename(filename);
            Assert.AreEqual(expectedDegree, actual);
        }

        [DataTestMethod]
        [DataRow("hdgm2014.dll")]   // pre-2017: not on CIRES public table
        [DataRow("hdgm2016.dll")]   // pre-2017: not on CIRES public table
        [DataRow("hdgm2027.dll")]   // future/unverified
        [DataRow("hdgm9999.dll")]   // implausible year
        public void GetMaxDegreeFromFilename_OutOfRangeYear_ReturnsNull(string filename)
        {
            // Versions outside the CIRES-published 2017–2026 range return null
            // rather than guessing — caller stays in null-state for unverified
            // builds.
            int? actual = HdgmModelMetadata.GetMaxDegreeFromFilename(filename);
            Assert.IsNull(actual);
        }

        [DataTestMethod]
        [DataRow("WMM2025.COF")]                  // not HDGM
        [DataRow("IGRF14.COF")]                   // not HDGM
        [DataRow("hdgm.dll")]                     // no year
        [DataRow("hdgm-build.dll")]               // no digits after hdgm
        [DataRow("")]                             // empty path
        [DataRow(null)]                           // null path
        public void GetMaxDegreeFromFilename_NotHdgmOrUnparseable_ReturnsNull(string filename)
        {
            int? actual = HdgmModelMetadata.GetMaxDegreeFromFilename(filename);
            Assert.IsNull(actual);
        }

        [TestMethod]
        public void GetMaxDegreeFromFilename_FullPath_StillResolves()
        {
            // The lookup keys off the filename, but callers pass full paths.
            int? actual = HdgmModelMetadata.GetMaxDegreeFromFilename(
                @"C:\models\geomag\hdgm2019-64.dll");
            Assert.AreEqual(720, actual);
        }
    }
}
