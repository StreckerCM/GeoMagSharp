/****************************************************************************
 * File:            ModelPathDetector.cs
 * Description:     Detection rules for routing model file paths
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Internal helper for classifying user-supplied model file paths.
    /// The HDGM detection rule is shared between <see cref="GeoMag"/> and
    /// the GeoMagSharp.GUI folder scanner via <see cref="System.Runtime.CompilerServices.InternalsVisibleToAttribute"/>.
    /// </summary>
    internal static class ModelPathDetector
    {
        /// <summary>
        /// Returns true if the path matches the HDGM filename rule:
        /// extension is ".dll" (case-insensitive) AND filename (without extension)
        /// contains "hdgm" (case-insensitive).
        /// </summary>
        /// <param name="path">A file path. Null or whitespace returns false.</param>
        public static bool IsHdgmPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            string ext;
            string fileNoExt;
            try
            {
                ext = Path.GetExtension(path);
                fileNoExt = Path.GetFileNameWithoutExtension(path);
            }
            catch (ArgumentException)
            {
                // Path contains invalid characters
                return false;
            }

            if (string.IsNullOrEmpty(ext)) return false;
            if (!ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)) return false;

            if (string.IsNullOrEmpty(fileNoExt)) return false;
            return fileNoExt.IndexOf("hdgm", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
