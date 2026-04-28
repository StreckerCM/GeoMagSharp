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
    }
}
