/****************************************************************************
 * File:            ModelDescriptorTests.cs
 * Description:     Unit tests for ModelDescriptor immutability and round-trip
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class ModelDescriptorTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullFilePath_Throws()
        {
            var _ = new ModelDescriptor(null, knownModels.NONE, "x", null, null);
        }

        [TestMethod]
        public void Constructor_NullDisplayName_DefaultsEmpty()
        {
            var d = new ModelDescriptor("path", knownModels.NONE, null, null, null);
            Assert.AreEqual(string.Empty, d.DisplayName);
        }

        [TestMethod]
        public void Constructor_AllFieldsSet_PropertiesRoundTrip()
        {
            var d = new ModelDescriptor("WMM.COF", knownModels.WMM, "WMM2025", 2025.0, 2030.0, "test");
            Assert.AreEqual("WMM.COF", d.FilePath);
            Assert.AreEqual(knownModels.WMM, d.DetectedType);
            Assert.AreEqual("WMM2025", d.DisplayName);
            Assert.AreEqual(2025.0, d.MinDate);
            Assert.AreEqual(2030.0, d.MaxDate);
            Assert.AreEqual("test", d.Description);
        }

        [TestMethod]
        public void Properties_HaveNoSetters()
        {
            var props = typeof(ModelDescriptor).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsTrue(props.Length > 0, "expected public instance properties on ModelDescriptor");
            foreach (var p in props)
            {
                Assert.IsFalse(p.CanWrite, "ModelDescriptor." + p.Name + " must be read-only");
            }
        }

        [TestMethod]
        public void ToString_IncludesKeyFields()
        {
            var d = new ModelDescriptor("WMM.COF", knownModels.WMM, "WMM2025", 2025.0, 2030.0);
            var s = d.ToString();
            Assert.IsTrue(s.IndexOf("WMM2025", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(s.IndexOf("WMM.COF", StringComparison.Ordinal) >= 0);
        }

        [TestMethod]
        public void NullDateBounds_AllowedForUnknownRange()
        {
            var d = new ModelDescriptor("hdgm.dll", knownModels.HDGM, "HDGM", null, null);
            Assert.IsNull(d.MinDate);
            Assert.IsNull(d.MaxDate);
        }
    }
}
