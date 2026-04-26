/****************************************************************************
 * File:            ModelPathDetectorTests.cs
 * Description:     Unit tests for ModelPathDetector.IsHdgmPath
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class ModelPathDetectorTests
    {
        [TestMethod]
        public void IsHdgmPath_StandardNoaaName_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("hdgm2019-64.dll"));

        [TestMethod]
        public void IsHdgmPath_UpperCaseExtension_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("hdgm2019-64.DLL"));

        [TestMethod]
        public void IsHdgmPath_UpperCaseFilename_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("HDGM2019-64.dll"));

        [TestMethod]
        public void IsHdgmPath_MixedCase_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("HdGm2019-64.Dll"));

        [TestMethod]
        public void IsHdgmPath_VendorRename_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("halliburton_hdgm.dll"));

        [TestMethod]
        public void IsHdgmPath_AbsoluteWindowsPath_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath(@"C:\foo\bar\hdgm.dll"));

        [TestMethod]
        public void IsHdgmPath_RelativePath_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("./coefficients/hdgm2024.dll"));

        [TestMethod]
        public void IsHdgmPath_NoHdgmInName_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("WMM.dll"));

        [TestMethod]
        public void IsHdgmPath_HdgmInDirectoryNotFile_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath(@"hdgm/wmm.dll"));

        [TestMethod]
        public void IsHdgmPath_HdgmExe_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("hdgm2019_file.exe"));

        [TestMethod]
        public void IsHdgmPath_HdgmTxt_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("HDGM_readme.txt"));

        [TestMethod]
        public void IsHdgmPath_HdgmCofFile_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("HDGM2019.COF"));

        [TestMethod]
        public void IsHdgmPath_NoExtension_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("hdgm"));

        [TestMethod]
        public void IsHdgmPath_EmptyString_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath(""));

        [TestMethod]
        public void IsHdgmPath_Whitespace_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("   "));

        [TestMethod]
        public void IsHdgmPath_Null_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath(null));
    }
}
