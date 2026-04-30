/****************************************************************************
 * File:            Win32NativeMethods.cs
 * Description:     P/Invoke wrappers for Windows DLL loading APIs
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Runtime.InteropServices;

namespace GeoMagSharp.HDGM.Native
{
    /// <summary>
    /// Thin P/Invoke wrappers for Win32 DLL loading APIs.
    /// Used by <see cref="LoadLibraryHdgmInvoker"/> to load user-supplied HDGM DLLs from
    /// arbitrary file paths (LoadLibraryEx) and resolve native function pointers (GetProcAddress).
    /// </summary>
    internal static class Win32NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true, BestFitMapping = false)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = false)]
        internal static extern uint FormatMessage(
            uint dwFlags,
            IntPtr lpSource,
            uint dwMessageId,
            uint dwLanguageId,
            System.Text.StringBuilder lpBuffer,
            uint nSize,
            IntPtr arguments);

        internal const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x1000;
        internal const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x200;

        /// <summary>
        /// LoadLibraryEx flag: when path is fully qualified, use the directory
        /// of the DLL as the search path for its dependencies, and DO NOT use
        /// the default search order (which includes cwd / PATH).
        /// Mitigates DLL planting attacks.
        /// </summary>
        internal const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

        /// <summary>Returns a human-readable description of a Win32 error code.</summary>
        internal static string GetWin32ErrorMessage(int errorCode)
        {
            var buffer = new System.Text.StringBuilder(512);
            uint len = FormatMessage(
                FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                IntPtr.Zero,
                (uint)errorCode,
                0,
                buffer,
                (uint)buffer.Capacity,
                IntPtr.Zero);
            return len > 0 ? buffer.ToString().TrimEnd('\r', '\n', ' ') : $"(unknown Win32 error {errorCode})";
        }
    }
}
