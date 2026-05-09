/****************************************************************************
 * File:            HDGMModelLoader.cs
 * Description:     Loads a NOAA HDGM DLL into a MagneticModelSet
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Runtime.InteropServices;
using GeoMagSharp.Discovery;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Loads a NOAA HDGM DLL from a user-supplied path into a <see cref="MagneticModelSet"/>
    /// configured with <see cref="knownModels.HDGM"/>, a probed date range, and a
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

            // Probe the DLL for its actual date range (#30). The probe makes ~8 hdgmcalc
            // calls and finds the first sentinel result — authoritative for HDGM, which
            // strips VERSIONINFO and exports no metadata. Falls back to wide-permissive
            // bounds on probe failure (corrupt DLL, LoadLibrary error); the runtime
            // sentinel inside HDGMCalculationAdapter remains the last-line guard.
            var probed = HdgmDateProbe.Probe(
                path => new LoadLibraryHdgmInvoker(path), dllPath);

            var invoker = new LoadLibraryHdgmInvoker(dllPath);

            var set = new MagneticModelSet
            {
                Type = knownModels.HDGM,
                Name = Path.GetFileNameWithoutExtension(dllPath).ToUpperInvariant(),
                MinDate = probed.minDate ?? 1900.0,
                MaxDate = probed.maxDate ?? 9999.0,
                NativeInvoker = invoker
            };
            set.FileNames.Add(Path.GetFileName(dllPath));
            return set;
        }
    }
}
