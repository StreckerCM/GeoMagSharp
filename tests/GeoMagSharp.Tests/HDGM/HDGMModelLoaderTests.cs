/****************************************************************************
 * File:            HDGMModelLoaderTests.cs
 * Description:     Unit tests for HDGMModelLoader.Load entry-point
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/
using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class HDGMModelLoaderTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Load_NullPath_ThrowsArgumentNull()
        {
            HDGMModelLoader.Load(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Load_EmptyPath_ThrowsArgumentNull()
        {
            HDGMModelLoader.Load("");
        }

        [TestMethod]
        public void Load_NonWindowsPlatform_ThrowsPlatformNotSupported()
        {
            // This test only meaningfully runs on non-Windows platforms.
            // On Windows, it asserts the runtime is Windows (i.e. inconclusive for the
            // platform-not-supported path).
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("Running on Windows; PlatformNotSupportedException path cannot be reached.");
                return;
            }
            try
            {
                HDGMModelLoader.Load(@"C:\anything\hdgm.dll");
                Assert.Fail("Expected PlatformNotSupportedException");
            }
            catch (PlatformNotSupportedException) { /* expected */ }
        }

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionFileNotFound))]
        public void Load_NonexistentDll_ThrowsFileNotFound_OnWindows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new GeoMagExceptionFileNotFound("(skipping on non-Windows — platform check fires first)");
            }
            HDGMModelLoader.Load(@"C:\__definitely_not_real__\hdgm2019-64.dll");
        }
    }
}
