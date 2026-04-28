/****************************************************************************
 * File:            ModelDiscovery.cs
 * Description:     Public discovery API: DiscoverModels, DescribeFile
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GeoMagSharp.Discovery;
using GeoMagSharp.HDGM;

namespace GeoMagSharp
{
    /// <summary>
    /// Library-level discovery API for enumerating loadable model files in a folder.
    /// Discovery is identification-only; consumers call <see cref="GeoMag.LoadModel(string)"/>
    /// when they actually want to use a model.
    /// </summary>
    public static class ModelDiscovery
    {
        /// <summary>Convenience overload: <see cref="ScanMode.Full"/>, no recursion, no cache.</summary>
        public static IEnumerable<ModelDescriptor> DiscoverModels(string folderPath)
        {
            return DiscoverModels(folderPath, new ModelDiscoveryOptions());
        }

        /// <summary>
        /// Enumerates loadable model files in folderPath. Returns empty if the folder does
        /// not exist. Per-file failures invoke options.OnError but do not stop enumeration.
        /// </summary>
        public static IEnumerable<ModelDescriptor> DiscoverModels(string folderPath, ModelDiscoveryOptions options)
        {
            if (folderPath == null) throw new ArgumentNullException(nameof(folderPath));
            if (options == null) throw new ArgumentNullException(nameof(options));

            // Implementation lands in Task 11.
            return new List<ModelDescriptor>();
        }

        /// <summary>
        /// Performs a Full-mode inspection on a single file and returns its descriptor.
        /// Returns null if the file's extension is not a recognized model format.
        /// </summary>
        /// <exception cref="ArgumentNullException">filePath is null.</exception>
        /// <exception cref="GeoMagExceptionFileNotFound">File does not exist.</exception>
        public static ModelDescriptor DescribeFile(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new GeoMagExceptionFileNotFound("Error: The file '" + filePath + "' was not found");

            string ext = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext)) return null;
            string extUpper = ext.ToUpperInvariant();

            if (extUpper == ".COF" || extUpper == ".DAT")
            {
                return ModelHeaderInspector.Inspect(filePath);
            }

            if (extUpper == ".DLL" && ModelPathDetector.IsHdgmPath(filePath))
            {
                var result = HdgmDateProbe.Probe(
                    path => CreateRealInvokerOrNull(path), filePath);
                return new ModelDescriptor(filePath, knownModels.HDGM,
                    BuildHdgmDisplayName(filePath), result.minDate, result.maxDate);
            }

            return null;
        }

        // ----- private helpers -----

        private static INativeHdgmInvoker CreateRealInvokerOrNull(string dllPath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
            try { return new LoadLibraryHdgmInvoker(dllPath); }
            catch { return null; }
        }

        private static string BuildHdgmDisplayName(string dllPath)
        {
            int? year = HdgmDateProbe.ExtractYearFromFilename(dllPath);
            return year.HasValue ? "HDGM" + year.Value : "HDGM";
        }
    }
}
