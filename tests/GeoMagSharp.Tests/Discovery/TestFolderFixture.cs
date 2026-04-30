/****************************************************************************
 * File:            TestFolderFixture.cs
 * Description:     IDisposable temp-folder helper for discovery functional tests
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;

namespace GeoMagSharp_UnitTests.Discovery
{
    /// <summary>
    /// Creates a temp folder (under the user's TEMP) on construction, exposes its
    /// path, and recursively deletes it on Dispose. Use inside a using block per
    /// test to keep test isolation.
    /// </summary>
    internal sealed class TestFolderFixture : IDisposable
    {
        public string FolderPath { get; }

        public TestFolderFixture()
        {
            FolderPath = Path.Combine(Path.GetTempPath(),
                "GeoMagSharpTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(FolderPath);
        }

        /// <summary>Copies a fixture file from the test deploy directory into the temp folder.</summary>
        public string CopyFixture(string fixtureName, string targetName = null)
        {
            string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Discovery", "Fixtures", fixtureName);
            string destPath = Path.Combine(FolderPath, targetName ?? fixtureName);
            File.Copy(sourcePath, destPath, overwrite: true);
            return destPath;
        }

        /// <summary>Writes arbitrary text into a file inside the temp folder.</summary>
        public string WriteFile(string fileName, string content)
        {
            string path = Path.Combine(FolderPath, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>Creates a subdirectory inside the temp folder.</summary>
        public string CreateSubdir(string subdirName)
        {
            string path = Path.Combine(FolderPath, subdirName);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(FolderPath))
                    Directory.Delete(FolderPath, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; don't fail tests on deletion races.
            }
        }
    }
}
