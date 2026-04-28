/****************************************************************************
 * File:            HDGMModelLoader.cs
 * Description:     Loads a NOAA HDGM DLL into a MagneticModelSet
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Loads a NOAA HDGM DLL from a user-supplied path into a <see cref="MagneticModelSet"/>
    /// configured with <see cref="knownModels.HDGM"/>, a permissive date range, and a
    /// <see cref="LoadLibraryHdgmInvoker"/> populated as the model set's NativeInvoker.
    /// </summary>
    internal static class HDGMModelLoader
    {
        public static MagneticModelSet Load(string dllPath)
        {
            if (string.IsNullOrWhiteSpace(dllPath))
                throw new ArgumentNullException(nameof(dllPath), "DLL path cannot be null or empty");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException(string.Format(
                    "HDGM is supported only on Windows. The NOAA HDGM DLL ('{0}') is not " +
                    "available for Linux or macOS. All other GeoMagSharp models remain " +
                    "cross-platform.", dllPath));

            if (!File.Exists(dllPath))
                throw new GeoMagExceptionFileNotFound(string.Format(
                    "Error: The HDGM DLL '{0}' was not found", dllPath));

            var invoker = new LoadLibraryHdgmInvoker(dllPath);

            var set = new MagneticModelSet
            {
                Type = knownModels.HDGM,
                Name = Path.GetFileNameWithoutExtension(dllPath).ToUpperInvariant(),
                MinDate = 1900.0,    // wide-permissive — sentinel is authoritative
                MaxDate = 9999.0,
                NativeInvoker = invoker
            };
            set.FileNames.Add(Path.GetFileName(dllPath));
            return set;
        }
    }
}
