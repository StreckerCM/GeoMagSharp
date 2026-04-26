/****************************************************************************
 * File:            MagneticModelSetDisposableTests.cs
 * Description:     Tests for IDisposable and NativeInvoker on MagneticModelSet
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class MagneticModelSetDisposableTests
    {
        [TestMethod]
        public void NewInstance_NativeInvokerIsNull()
        {
            var set = new MagneticModelSet();
            Assert.IsNull(set.NativeInvoker);
        }

        [TestMethod]
        public void Dispose_OnNonHdgmModelSet_DoesNotThrow()
        {
            var set = new MagneticModelSet();
            set.Dispose(); // no-op when NativeInvoker is null
        }

        [TestMethod]
        public void Dispose_DisposesNativeInvoker()
        {
            var fake = new FakeHdgmInvoker();
            var set = new MagneticModelSet { NativeInvoker = fake };
            set.Dispose();
            Assert.IsTrue(fake.DisposeWasCalled);
        }

        [TestMethod]
        public void DisposeTwice_DoesNotThrow()
        {
            var fake = new FakeHdgmInvoker();
            var set = new MagneticModelSet { NativeInvoker = fake };
            set.Dispose();
            set.Dispose();
        }

        [TestMethod]
        public void IsDisposable()
        {
            var set = new MagneticModelSet();
            Assert.IsTrue(set is IDisposable);
        }
    }
}
