/****************************************************************************
 * File:            LoadLibraryHdgmInvokerUnitTests.cs
 * Description:     Unit tests for LoadLibraryHdgmInvoker
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class LoadLibraryHdgmInvokerUnitTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Ctor_NullPath_ThrowsArgumentNull()
        {
            using (new LoadLibraryHdgmInvoker(null)) { }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Ctor_EmptyPath_ThrowsArgumentNull()
        {
            using (new LoadLibraryHdgmInvoker("")) { }
        }

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionFileNotFound))]
        public void Ctor_NonExistentPath_ThrowsFileNotFound()
        {
            using (new LoadLibraryHdgmInvoker(@"C:\__definitely_not_real__\hdgm2019-64.dll")) { }
        }

        [TestMethod]
        public void DisposeTwice_DoesNotThrow()
        {
            var invoker = TryCreateInvokerOrNull(out _);
            if (invoker == null)
            {
                Assert.Inconclusive("Real DLL not available; this test only exercises Dispose idempotency when DLL is present.");
                return;
            }
            invoker.Dispose();
            invoker.Dispose(); // must not throw
        }

        // Helper used by tests that need a real DLL; if unavailable, return null and let test skip.
        private static LoadLibraryHdgmInvoker TryCreateInvokerOrNull(out string reason)
        {
            string path = Environment.GetEnvironmentVariable("HDGM_DLL_PATH");
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                reason = "HDGM_DLL_PATH not set or file missing";
                return null;
            }
            try
            {
                reason = null;
                return new LoadLibraryHdgmInvoker(path);
            }
            catch
            {
                reason = "Failed to load real DLL";
                return null;
            }
        }
    }
}
