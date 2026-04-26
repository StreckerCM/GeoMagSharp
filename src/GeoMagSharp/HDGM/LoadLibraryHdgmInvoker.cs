/****************************************************************************
 * File:            LoadLibraryHdgmInvoker.cs
 * Description:     Production INativeHdgmInvoker implementation backed by the
 *                  Win32 LoadLibraryEx + GetProcAddress + delegate pattern.
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Runtime.InteropServices;
using GeoMagSharp.HDGM.Native;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Loads a NOAA HDGM DLL from a user-supplied path via Win32 LoadLibraryEx,
    /// resolves the hdgmcalc symbol, and exposes invocation through INativeHdgmInvoker.
    /// </summary>
    /// <remarks>
    /// Windows-only. The caller is responsible for picking the DLL matching the
    /// process bitness (hdgm2019-64.dll for 64-bit; hdgm2019.dll for 32-bit). A
    /// bitness mismatch surfaces as Win32 error 193 ("not a valid Win32 application").
    /// </remarks>
    internal sealed class LoadLibraryHdgmInvoker : INativeHdgmInvoker
    {
        private IntPtr _hModule;
        private HdgmCalcDelegate _delegate;
        private readonly object _syncRoot = new object();
        private bool _disposed;

        public LoadLibraryHdgmInvoker(string dllPath)
        {
            if (string.IsNullOrWhiteSpace(dllPath))
                throw new ArgumentNullException(nameof(dllPath), "DLL path cannot be null or empty");

            if (!File.Exists(dllPath))
                throw new GeoMagExceptionFileNotFound(string.Format(
                    "Error: The HDGM DLL '{0}' was not found", dllPath));

            // dwFlags = 0 — default LoadLibraryEx behavior. NOAA's DLL doesn't expect altered search rules.
            _hModule = Win32NativeMethods.LoadLibraryEx(dllPath, IntPtr.Zero, 0);
            if (_hModule == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                string description = Win32NativeMethods.GetWin32ErrorMessage(err);
                string hint = err == 193
                    ? string.Format(" (process is {0}-bit; use the matching HDGM DLL — hdgm2019.dll for 32-bit, hdgm2019-64.dll for 64-bit)",
                        IntPtr.Size == 8 ? "64" : "32")
                    : ". If the file exists and is the correct bitness, check that antivirus has not quarantined it.";
                throw new GeoMagExceptionModelNotLoaded(string.Format(
                    "Error: Failed to load HDGM DLL '{0}': Win32 error {1} — {2}{3}",
                    dllPath, err, description, hint));
            }

            IntPtr fnPtr = Win32NativeMethods.GetProcAddress(_hModule, "hdgmcalc");
            if (fnPtr == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Win32NativeMethods.FreeLibrary(_hModule);
                _hModule = IntPtr.Zero;
                throw new GeoMagExceptionModelNotLoaded(string.Format(
                    "Error: DLL '{0}' loaded but 'hdgmcalc' symbol not found (Win32 error {1}). " +
                    "The file may not be a valid HDGM DLL, or the version may be unsupported.",
                    dllPath, err));
            }

            _delegate = (HdgmCalcDelegate)Marshal.GetDelegateForFunctionPointer(fnPtr, typeof(HdgmCalcDelegate));
        }

        /// <inheritdoc/>
        public int Calculate(double latitude, double longitude, double depthMeters, double decimalYear, double[] outData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LoadLibraryHdgmInvoker));
            if (outData == null) throw new ArgumentNullException(nameof(outData));
            if (outData.Length < 25) throw new ArgumentException("outData must have at least 25 elements", nameof(outData));

            // The NOAA DLL is not documented as thread-safe; serialize at the native boundary.
            lock (_syncRoot)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(LoadLibraryHdgmInvoker));
                return _delegate(latitude, longitude, depthMeters, decimalYear,
                    /* usePomme */ 0, /* useDifi */ 0, outData);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Explicit disposal: safe to take the lock and clean up managed state.
                lock (_syncRoot)
                {
                    if (_disposed) return;
                    _disposed = true;
                    FreeNativeHandle();
                    _delegate = null;
                }
            }
            else
            {
                // Finalizer path: must NOT take a lock. Just free the native handle.
                if (_disposed) return;
                _disposed = true;
                FreeNativeHandle();
                _delegate = null;
            }
        }

        private void FreeNativeHandle()
        {
            if (_hModule != IntPtr.Zero)
            {
                Win32NativeMethods.FreeLibrary(_hModule);
                _hModule = IntPtr.Zero;
            }
        }

        ~LoadLibraryHdgmInvoker() { Dispose(false); }
    }
}
