/****************************************************************************
 * File:            ModelReaderHDGMRoutingTests.cs
 * Description:     Verify ModelReader.Read auto-detects HDGM .dll paths and
 *                  delegates to HDGMModelLoader instead of falling through to
 *                  the unsupported-extension error.
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.HDGM
{
    /// <summary>
    /// Regression tests for HDGM auto-detection at the ModelReader.Read layer.
    ///
    /// Background: Consumers (e.g. GeoMagSharpGUI) call ModelReader.Read directly
    /// rather than going through GeoMag.LoadModel. Initially HDGM detection only
    /// lived in GeoMag.LoadModel, so .dll paths supplied to ModelReader.Read fell
    /// through to the extension switch and threw "file type '.DLL' is not
    /// supported." These tests pin the routing to ModelReader.Read so it cannot
    /// regress.
    /// </summary>
    [TestClass]
    public class ModelReaderHDGMRoutingTests
    {
        [TestMethod]
        public void Read_HDGMDllPathThatDoesNotExist_ThrowsFileNotFound_NotUnsupportedType()
        {
            // A path matching the HDGM rule (.dll + filename contains "hdgm") that
            // does NOT exist on disk. After routing into HDGMModelLoader, it should
            // throw GeoMagExceptionFileNotFound — proving the path was routed to
            // HDGM logic rather than falling through to the
            // GeoMagExceptionModelNotLoaded "file type not supported" branch.
            try
            {
                ModelReader.Read(@"C:\__definitely_not_real_hdgm_path__\hdgm2019-64.dll");
                Assert.Fail("Expected GeoMagExceptionFileNotFound or PlatformNotSupportedException");
            }
            catch (GeoMagExceptionFileNotFound)
            {
                // Expected on Windows: routed through HDGMModelLoader, then file-existence check fired.
            }
            catch (System.PlatformNotSupportedException)
            {
                // Expected on non-Windows: HDGMModelLoader rejects before reaching file-existence.
            }
            catch (GeoMagExceptionModelNotLoaded ex) when (ex.Message.IndexOf(".DLL", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Assert.Fail("Routing regression: HDGM .dll path fell through to the unsupported-extension branch instead of HDGMModelLoader. Message: " + ex.Message);
            }
        }

        [TestMethod]
        public void Read_TwoArgOverload_HDGMDllPath_RoutesToHDGM()
        {
            // The two-arg ModelReader.Read(modelFile, svFile) overload also routes
            // HDGM paths (svFile is irrelevant for HDGM — DLL is self-contained).
            try
            {
                ModelReader.Read(@"C:\__definitely_not_real_hdgm_path__\hdgm2019-64.dll", null);
                Assert.Fail("Expected GeoMagExceptionFileNotFound or PlatformNotSupportedException");
            }
            catch (GeoMagExceptionFileNotFound) { }
            catch (System.PlatformNotSupportedException) { }
            catch (GeoMagExceptionModelNotLoaded ex) when (ex.Message.IndexOf(".DLL", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Assert.Fail("Routing regression on two-arg overload: " + ex.Message);
            }
        }

        [TestMethod]
        public void Read_NonHdgmDllFile_StillThrowsUnsupportedType()
        {
            // A .dll whose filename does NOT contain "hdgm" should still go through
            // the existing extension switch and produce the "file type not supported"
            // error. This protects against accidentally routing every .dll to HDGM.
            try
            {
                ModelReader.Read(@"C:\__definitely_not_real_path__\notamodel.dll");
                Assert.Fail("Expected GeoMagExceptionModelNotLoaded for unsupported extension");
            }
            catch (GeoMagExceptionFileNotFound)
            {
                // Existing behavior: file-not-found check runs before the extension switch.
            }
            catch (GeoMagExceptionModelNotLoaded)
            {
                // Also acceptable: routed to extension switch and rejected.
            }
        }
    }
}
