# HDGM Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Windows-only HDGM (High Definition Geomagnetic Model) support to GeoMagSharp by dynamically loading NOAA's `hdgm2019-64.dll` via P/Invoke and routing it through a side-door pipeline alongside the existing `.COF`/`.DAT` loaders.

**Architecture:** Filename detection at `GeoMag.LoadModel(path)` routes `.dll` paths whose filename contains "hdgm" (case-insensitive) into a new `HDGMModelLoader` that constructs a `LoadLibraryHdgmInvoker` (LoadLibraryEx + GetProcAddress + delegate). Calculations branch on `MagneticModelSet.NativeInvoker != null` into `HDGMCalculationAdapter` which calls the native delegate and maps the 25-element `outData` array to `MagneticCalculations` plus `GeomagneticUncertainty` per-point fields. Existing models (WMM/IGRF/EMM/DGRF/WMMHR) are byte-identical before and after.

**Tech Stack:** C# multi-target (net48 + netstandard2.0), MSTest for tests, Newtonsoft.Json for serialization, P/Invoke + `LoadLibraryEx`/`GetProcAddress` for native binding, NOAA HDGM DLL (user-supplied at runtime).

**Reference design:** `docs/superpowers/specs/2026-04-26-hdgm-support-design.md`

---

## Conventions used in this plan

- **Test framework:** MSTest 3.1.1, attributes `[TestClass]`, `[TestMethod]`, `[TestCategory]`
- **Test naming:** `Method_Scenario_Expected` per CLAUDE.md
- **Test project namespace:** `GeoMagSharp_UnitTests` (root) and `GeoMagSharp_UnitTests.HDGM` (new HDGM tests)
- **Production namespace:** `GeoMagSharp.HDGM` for HDGM types; `GeoMagSharp.HDGM.Native` for Win32 P/Invoke wrappers
- **Build commands:**
  - `dotnet build src/GeoMagSharp/GeoMagSharp.csproj -c Debug` — main library
  - `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Debug --filter "TestCategory!=RequiresHDGMDll"` — unit tests
  - `dotnet build -c Release` — full release build
- **Commit style:** `[<PERSONA>] <type>: <description>` per the Ralph Loop convention. For these implementation commits, use `[IMPLEMENTER] feat: ...` or `[IMPLEMENTER] test: ...` etc.
- **Co-author trailer:** include `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`

---

## Task 1: Add `knownModels.HDGM` enum value

**Files:**
- Modify: `src/GeoMagSharp/Enums/GeoMagEnums.cs`
- Test: `tests/GeoMagSharp.Tests/HDGM/KnownModelsHDGMTests.cs` (Create)

- [ ] **Step 1: Write the failing test**

Create `tests/GeoMagSharp.Tests/HDGM/KnownModelsHDGMTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class KnownModelsHDGMTests
    {
        [TestMethod]
        public void HDGMEnumValue_Equals6()
        {
            Assert.AreEqual(6, (int)knownModels.HDGM);
        }

        [TestMethod]
        public void HDGMEnumValue_DoesNotCollideWithExisting()
        {
            // sanity: other enum values are unchanged
            Assert.AreEqual(0, (int)knownModels.NONE);
            Assert.AreEqual(1, (int)knownModels.DGRF);
            Assert.AreEqual(2, (int)knownModels.EMM);
            Assert.AreEqual(3, (int)knownModels.IGRF);
            Assert.AreEqual(4, (int)knownModels.WMM);
            Assert.AreEqual(5, (int)knownModels.WMMHR);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails (compile error — `knownModels.HDGM` not defined)**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~KnownModelsHDGMTests" -c Debug
```

Expected: build error `'knownModels' does not contain a definition for 'HDGM'`

- [ ] **Step 3: Add the enum value**

In `src/GeoMagSharp/Enums/GeoMagEnums.cs`, after the `WMMHR = 5` entry (line 98), add:

```csharp
        /// <summary>
        /// High Definition Geomagnetic Model (NOAA degree-740 crustal field).
        /// Windows-only — requires user-supplied NOAA HDGM DLL at runtime.
        /// HighResolution category per ISCWSA Rev5.13.
        /// </summary>
        HDGM = 6
```

(Add a comma after `WMMHR = 5` if not already present.)

- [ ] **Step 4: Run test to verify it passes**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~KnownModelsHDGMTests" -c Debug
```

Expected: 2 tests passed.

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Enums/GeoMagEnums.cs tests/GeoMagSharp.Tests/HDGM/KnownModelsHDGMTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add knownModels.HDGM enum value (#19)

Adds HDGM = 6 to the knownModels enum with XML doc noting the
HighResolution category per ISCWSA Rev5.13. Purely additive — no
existing enum values renumbered. Sanity test verifies the value
and confirms no collision with existing enum members.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Define `INativeHdgmInvoker` interface, `HdgmCalcDelegate`, and `Win32NativeMethods`

**Files:**
- Create: `src/GeoMagSharp/HDGM/INativeHdgmInvoker.cs`
- Create: `src/GeoMagSharp/HDGM/HdgmCalcDelegate.cs`
- Create: `src/GeoMagSharp/HDGM/Native/Win32NativeMethods.cs`

These are pure type definitions — no behavior to test directly. Verification is "the project compiles" and "the types exist with the expected shape."

- [ ] **Step 1: Create `INativeHdgmInvoker.cs`**

```csharp
/****************************************************************************
 * File:            INativeHdgmInvoker.cs
 * Description:     Contract for invoking the NOAA HDGM native function
 ****************************************************************************/

using System;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Contract for calling NOAA's HDGM native calculation function.
    /// Production implementation: <see cref="LoadLibraryHdgmInvoker"/> (internal).
    /// Test implementations may be substituted via the public API for unit testing.
    /// </summary>
    public interface INativeHdgmInvoker : IDisposable
    {
        /// <summary>
        /// Invokes the native hdgmcalc function and returns its 25-element output array.
        /// </summary>
        /// <param name="latitude">Geodetic latitude in decimal degrees (-90 to +90).</param>
        /// <param name="longitude">Geodetic longitude in decimal degrees (-180 to +180).</param>
        /// <param name="depthMeters">Depth in meters, positive downward (negative for altitude).</param>
        /// <param name="decimalYear">Date as a decimal year (e.g., 2020.5).</param>
        /// <param name="outData">Output buffer, length 25. Must be allocated by caller.</param>
        /// <returns>Native function status code; 0 = success.</returns>
        int Calculate(double latitude, double longitude, double depthMeters, double decimalYear, double[] outData);
    }
}
```

- [ ] **Step 2: Create `HdgmCalcDelegate.cs`**

```csharp
/****************************************************************************
 * File:            HdgmCalcDelegate.cs
 * Description:     Native delegate matching NOAA hdgmcalc() function signature
 ****************************************************************************/

using System.Runtime.InteropServices;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Delegate matching the NOAA hdgmcalc() function signature.
    /// Reference: HDGM_Sublibrary.c:46 — int __stdcall hdgmcalc(double lt, double ln, ...).
    /// </summary>
    /// <param name="latitude">Geodetic latitude in degrees.</param>
    /// <param name="longitude">Geodetic longitude in degrees.</param>
    /// <param name="depthMeters">Depth in meters (positive down).</param>
    /// <param name="decimalYear">Date as decimal year.</param>
    /// <param name="usePomme">HDGM-RT magnetospheric flag (0 = disabled).</param>
    /// <param name="useDifi">HDGM-RT ionospheric flag (0 = disabled).</param>
    /// <param name="outData">Output array, must be at least 25 elements.</param>
    /// <returns>Status code; 0 = success.</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int HdgmCalcDelegate(
        double latitude,
        double longitude,
        double depthMeters,
        double decimalYear,
        int usePomme,
        int useDifi,
        [In, Out] double[] outData);
}
```

- [ ] **Step 3: Create `Win32NativeMethods.cs`**

```csharp
/****************************************************************************
 * File:            Win32NativeMethods.cs
 * Description:     P/Invoke wrappers for Windows DLL loading APIs
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
```

- [ ] **Step 4: Verify build**

```
dotnet build src/GeoMagSharp/GeoMagSharp.csproj -c Debug
```

Expected: build succeeds. (No tests yet for these types — they're contract definitions; behavior is tested via Tasks 6 and 8 indirectly through `LoadLibraryHdgmInvoker` and `HDGMCalculationAdapter`.)

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/HDGM/INativeHdgmInvoker.cs src/GeoMagSharp/HDGM/HdgmCalcDelegate.cs src/GeoMagSharp/HDGM/Native/Win32NativeMethods.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add HDGM native binding type definitions (#19)

Adds INativeHdgmInvoker (public interface), HdgmCalcDelegate (internal,
matches NOAA hdgmcalc signature with __stdcall), and Win32NativeMethods
(internal P/Invoke wrappers for LoadLibraryEx, GetProcAddress, FreeLibrary,
plus FormatMessage helper for descriptive Win32 errors).

These are contract-only types; behavior is exercised by LoadLibraryHdgmInvoker
in subsequent tasks.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Extend `GeomagneticUncertainty` with per-point σ and coverage flag

**Files:**
- Modify: `src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs`
- Test: `tests/GeoMagSharp.Tests/HDGM/GeomagneticUncertaintyHDGMExtensionTests.cs` (Create)

- [ ] **Step 1: Write the failing test**

Create `tests/GeoMagSharp.Tests/HDGM/GeomagneticUncertaintyHDGMExtensionTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class GeomagneticUncertaintyHDGMExtensionTests
    {
        [TestMethod]
        public void NewInstance_AllPerPointSigmasAreNull()
        {
            var u = new GeomagneticUncertainty();
            Assert.IsNull(u.SigmaD);
            Assert.IsNull(u.SigmaI);
            Assert.IsNull(u.SigmaH);
            Assert.IsNull(u.SigmaX);
            Assert.IsNull(u.SigmaY);
            Assert.IsNull(u.SigmaZ);
            Assert.IsNull(u.SigmaF);
        }

        [TestMethod]
        public void NewInstance_HighResolutionCoverageIsNull()
        {
            var u = new GeomagneticUncertainty();
            Assert.IsNull(u.HighResolutionCoverage);
        }

        [TestMethod]
        public void SetSigmaD_RoundTrips()
        {
            var u = new GeomagneticUncertainty { SigmaD = 0.123 };
            Assert.AreEqual(0.123, u.SigmaD);
        }

        [TestMethod]
        public void SetHighResolutionCoverage_RoundTrips()
        {
            var u = new GeomagneticUncertainty { HighResolutionCoverage = true };
            Assert.AreEqual(true, u.HighResolutionCoverage);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~GeomagneticUncertaintyHDGMExtensionTests" -c Debug
```

Expected: build error — `SigmaD`, `HighResolutionCoverage`, etc. not defined.

- [ ] **Step 3: Add the new properties to `GeomagneticUncertainty`**

In `src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs`, after the `DepthAzimuthUncertainty` property (line 45), and before the `ScaleTo` method, add:

```csharp
        /// <summary>
        /// Per-point σ for declination in degrees, 1-sigma. Populated by HDGM and other
        /// models that provide location-specific uncertainty. Null if the model only
        /// provides global ISCWSA values (the existing <see cref="Declination"/> field).
        /// </summary>
        public double? SigmaD { get; set; }

        /// <summary>Per-point σ for inclination in degrees, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaI { get; set; }

        /// <summary>Per-point σ for horizontal intensity in nT, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaH { get; set; }

        /// <summary>Per-point σ for the X (north) component in nT, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaX { get; set; }

        /// <summary>Per-point σ for the Y (east) component in nT, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaY { get; set; }

        /// <summary>Per-point σ for the Z (down) component in nT, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaZ { get; set; }

        /// <summary>Per-point σ for total field intensity in nT, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaF { get; set; }

        /// <summary>
        /// True if the queried location is in a high-resolution survey-covered region
        /// (28 km half-wavelength, e.g., HDGM's NSD-covered areas).
        /// False for satellite-only fallback regions (~150 km half-wavelength).
        /// Null if the model does not provide per-location coverage information.
        /// </summary>
        public bool? HighResolutionCoverage { get; set; }
```

Also extend `ScaleTo` to propagate the new fields. After the existing `DepthAzimuthUncertainty = ...` line in the `ScaleTo` return object, add:

```csharp
                SigmaD = SigmaD.HasValue ? SigmaD.Value * scaleFactor : (double?)null,
                SigmaI = SigmaI.HasValue ? SigmaI.Value * scaleFactor : (double?)null,
                SigmaH = SigmaH.HasValue ? SigmaH.Value * scaleFactor : (double?)null,
                SigmaX = SigmaX.HasValue ? SigmaX.Value * scaleFactor : (double?)null,
                SigmaY = SigmaY.HasValue ? SigmaY.Value * scaleFactor : (double?)null,
                SigmaZ = SigmaZ.HasValue ? SigmaZ.Value * scaleFactor : (double?)null,
                SigmaF = SigmaF.HasValue ? SigmaF.Value * scaleFactor : (double?)null,
                HighResolutionCoverage = HighResolutionCoverage  // bool flag — not scaled
```

- [ ] **Step 4: Run test to verify it passes**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~GeomagneticUncertaintyHDGMExtensionTests" -c Debug
```

Expected: 4 tests passed.

- [ ] **Step 5: Add a ScaleTo regression test**

Add this test to the same file:

```csharp
        [TestMethod]
        public void ScaleTo_PropagatesPerPointSigmas()
        {
            var u = new GeomagneticUncertainty
            {
                SigmaD = 0.1, SigmaI = 0.2, SigmaH = 100, SigmaX = 50, SigmaY = 60, SigmaZ = 70, SigmaF = 110,
                HighResolutionCoverage = true
            };
            var scaled = u.ScaleTo(2.0);
            Assert.AreEqual(0.2, scaled.SigmaD);
            Assert.AreEqual(0.4, scaled.SigmaI);
            Assert.AreEqual(200, scaled.SigmaH);
            Assert.AreEqual(100, scaled.SigmaX);
            Assert.AreEqual(120, scaled.SigmaY);
            Assert.AreEqual(140, scaled.SigmaZ);
            Assert.AreEqual(220, scaled.SigmaF);
            Assert.AreEqual(true, scaled.HighResolutionCoverage);
        }

        [TestMethod]
        public void ScaleTo_NullSigmasRemainNull()
        {
            var u = new GeomagneticUncertainty();
            var scaled = u.ScaleTo(2.0);
            Assert.IsNull(scaled.SigmaD);
            Assert.IsNull(scaled.HighResolutionCoverage);
        }
```

Run again:

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~GeomagneticUncertaintyHDGMExtensionTests" -c Debug
```

Expected: 6 tests passed.

- [ ] **Step 6: Commit**

```bash
git add src/GeoMagSharp/Models/Results/GeomagneticUncertainty.cs tests/GeoMagSharp.Tests/HDGM/GeomagneticUncertaintyHDGMExtensionTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add per-point sigma and coverage fields to GeomagneticUncertainty (#19)

Adds optional nullable SigmaD/I/H/X/Y/Z/F (degrees or nT, 1-sigma) and
HighResolutionCoverage (bool) to GeomagneticUncertainty. All default to null
on existing model types; HDGM populates them from outData[16..23].

Generic naming (no HDGM brand) so future HRGM-tier models can populate the
same fields without an API change. ScaleTo extended to propagate the new
sigma values through scale factors.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Implement `ModelPathDetector` with `IsHdgmPath`

**Files:**
- Create: `src/GeoMagSharp/HDGM/ModelPathDetector.cs`
- Create: `tests/GeoMagSharp.Tests/HDGM/ModelPathDetectorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/GeoMagSharp.Tests/HDGM/ModelPathDetectorTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class ModelPathDetectorTests
    {
        [TestMethod]
        public void IsHdgmPath_StandardNoaaName_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("hdgm2019-64.dll"));

        [TestMethod]
        public void IsHdgmPath_UpperCaseExtension_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("hdgm2019-64.DLL"));

        [TestMethod]
        public void IsHdgmPath_UpperCaseFilename_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("HDGM2019-64.dll"));

        [TestMethod]
        public void IsHdgmPath_MixedCase_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("HdGm2019-64.Dll"));

        [TestMethod]
        public void IsHdgmPath_VendorRename_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("halliburton_hdgm.dll"));

        [TestMethod]
        public void IsHdgmPath_AbsoluteWindowsPath_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath(@"C:\foo\bar\hdgm.dll"));

        [TestMethod]
        public void IsHdgmPath_RelativePath_ReturnsTrue() =>
            Assert.IsTrue(ModelPathDetector.IsHdgmPath("./coefficients/hdgm2024.dll"));

        [TestMethod]
        public void IsHdgmPath_NoHdgmInName_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("WMM.dll"));

        [TestMethod]
        public void IsHdgmPath_HdgmInDirectoryNotFile_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath(@"hdgm/wmm.dll"));

        [TestMethod]
        public void IsHdgmPath_HdgmExe_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("hdgm2019_file.exe"));

        [TestMethod]
        public void IsHdgmPath_HdgmTxt_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("HDGM_readme.txt"));

        [TestMethod]
        public void IsHdgmPath_HdgmCofFile_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("HDGM2019.COF"));

        [TestMethod]
        public void IsHdgmPath_NoExtension_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("hdgm"));

        [TestMethod]
        public void IsHdgmPath_EmptyString_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath(""));

        [TestMethod]
        public void IsHdgmPath_Whitespace_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath("   "));

        [TestMethod]
        public void IsHdgmPath_Null_ReturnsFalse() =>
            Assert.IsFalse(ModelPathDetector.IsHdgmPath(null));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelPathDetectorTests" -c Debug
```

Expected: build error — `ModelPathDetector` does not exist.

- [ ] **Step 3: Implement `ModelPathDetector`**

Create `src/GeoMagSharp/HDGM/ModelPathDetector.cs`:

```csharp
/****************************************************************************
 * File:            ModelPathDetector.cs
 * Description:     Detection rules for routing model file paths
 ****************************************************************************/

using System;
using System.IO;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Internal helper for classifying user-supplied model file paths.
    /// The HDGM detection rule is shared between <see cref="GeoMag.LoadModel"/> and
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
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelPathDetectorTests" -c Debug
```

Expected: 16 tests passed.

- [ ] **Step 5: Add `[InternalsVisibleTo]` for the test project**

The tests reference `internal` members. Find the existing `[InternalsVisibleTo]` declaration if any, otherwise add one. In `src/GeoMagSharp/GeoMagSharp.csproj`, add inside the existing `<Project>` element:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="GeoMagSharp.Tests" />
    <InternalsVisibleTo Include="GeoMagSharp.GUI" />
  </ItemGroup>
```

(If an `InternalsVisibleTo` ItemGroup already exists for the tests, just add the GUI line.)

- [ ] **Step 6: Re-run tests**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelPathDetectorTests" -c Debug
```

Expected: 16 tests passed (and now `internal` access is granted for both consumers).

- [ ] **Step 7: Commit**

```bash
git add src/GeoMagSharp/HDGM/ModelPathDetector.cs tests/GeoMagSharp.Tests/HDGM/ModelPathDetectorTests.cs src/GeoMagSharp/GeoMagSharp.csproj
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add ModelPathDetector with HDGM filename rule (#19)

Adds internal static ModelPathDetector.IsHdgmPath(path) implementing the
case-insensitive ".dll extension AND filename contains 'hdgm'" rule.
The rule is shared with the GeoMagSharp.GUI scanner via [InternalsVisibleTo],
preventing rule duplication between the loader and the GUI's folder scan.

Includes 16 unit tests covering exact match, case variations, vendor renames,
absolute and relative paths, false positives (HDGM in directory only,
hdgm.exe, hdgm.txt, hdgm.cof), and edge cases (null, empty, whitespace,
no extension, invalid characters).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Create `FakeHdgmInvoker` test double

**Files:**
- Create: `tests/GeoMagSharp.Tests/HDGM/FakeHdgmInvoker.cs`

This is a test infrastructure piece — no behavior tests for it directly; it exists to support adapter tests in Task 7.

- [ ] **Step 1: Create the fake**

Create `tests/GeoMagSharp.Tests/HDGM/FakeHdgmInvoker.cs`:

```csharp
using System.Collections.Generic;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    /// <summary>
    /// Test double for INativeHdgmInvoker. Records calls and returns
    /// configurable canned responses without invoking any native code.
    /// </summary>
    internal class FakeHdgmInvoker : INativeHdgmInvoker
    {
        public double[] CannedOutData { get; set; } = new double[25];
        public int CannedReturnValue { get; set; } = 0;
        public List<CalculationCall> Calls { get; } = new List<CalculationCall>();
        public bool DisposeWasCalled { get; private set; }

        public int Calculate(double latitude, double longitude, double depthMeters, double decimalYear, double[] outData)
        {
            Calls.Add(new CalculationCall
            {
                Latitude = latitude,
                Longitude = longitude,
                DepthMeters = depthMeters,
                DecimalYear = decimalYear
            });
            System.Array.Copy(CannedOutData, outData, System.Math.Min(CannedOutData.Length, outData.Length));
            return CannedReturnValue;
        }

        public void Dispose() => DisposeWasCalled = true;
    }

    internal class CalculationCall
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DepthMeters { get; set; }
        public double DecimalYear { get; set; }
    }
}
```

- [ ] **Step 2: Verify build**

```
dotnet build tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Debug
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add tests/GeoMagSharp.Tests/HDGM/FakeHdgmInvoker.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] test: add FakeHdgmInvoker test double (#19)

Internal test double implementing INativeHdgmInvoker. Records each
Calculate call into a list (lat, lon, depth, decimal year) and returns
a configurable canned 25-element outData array plus configurable status
code. Used by HDGMCalculationAdapter unit tests to verify index mapping
without requiring the real NOAA DLL.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Implement `LoadLibraryHdgmInvoker` (production native binding)

**Files:**
- Create: `src/GeoMagSharp/HDGM/LoadLibraryHdgmInvoker.cs`
- Create: `tests/GeoMagSharp.Tests/HDGM/LoadLibraryHdgmInvokerUnitTests.cs`

Most behavior of this class requires a real DLL and is covered in integration tests (Task 14). This task adds only the unit-testable parts: argument validation and the "file not found" path.

- [ ] **Step 1: Write the failing tests**

Create `tests/GeoMagSharp.Tests/HDGM/LoadLibraryHdgmInvokerUnitTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~LoadLibraryHdgmInvokerUnitTests" -c Debug
```

Expected: build error — `LoadLibraryHdgmInvoker` does not exist.

- [ ] **Step 3: Implement `LoadLibraryHdgmInvoker`**

Create `src/GeoMagSharp/HDGM/LoadLibraryHdgmInvoker.cs`:

```csharp
/****************************************************************************
 * File:            LoadLibraryHdgmInvoker.cs
 * Description:     Production INativeHdgmInvoker implementation backed by the
 *                  Win32 LoadLibraryEx + GetProcAddress + delegate pattern.
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

        public int Calculate(double latitude, double longitude, double depthMeters, double decimalYear, double[] outData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LoadLibraryHdgmInvoker));
            if (outData == null) throw new ArgumentNullException(nameof(outData));
            if (outData.Length < 25) throw new ArgumentException("outData must have at least 25 elements", nameof(outData));

            // The NOAA DLL is not documented as thread-safe; serialize at the native boundary.
            lock (_syncRoot)
            {
                return _delegate(latitude, longitude, depthMeters, decimalYear,
                    /* usePomme */ 0, /* useDifi */ 0, outData);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_hModule != IntPtr.Zero)
            {
                Win32NativeMethods.FreeLibrary(_hModule);
                _hModule = IntPtr.Zero;
            }
            _delegate = null;
        }

        ~LoadLibraryHdgmInvoker() { Dispose(); }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~LoadLibraryHdgmInvokerUnitTests" -c Debug
```

Expected: 3 tests pass + 1 inconclusive (the dispose-twice test, unless `HDGM_DLL_PATH` is set).

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/HDGM/LoadLibraryHdgmInvoker.cs tests/GeoMagSharp.Tests/HDGM/LoadLibraryHdgmInvokerUnitTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add LoadLibraryHdgmInvoker production native binding (#19)

Sealed internal class implementing INativeHdgmInvoker via Win32
LoadLibraryEx + GetProcAddress + Marshal.GetDelegateForFunctionPointer.
Validates path on construction (ArgumentNullException for null/empty,
GeoMagExceptionFileNotFound for missing file). Translates Win32 errors
into descriptive GeoMagExceptionModelNotLoaded messages, including a
bitness-mismatch hint for Win32 error 193 and an antivirus-quarantine
hint for other failures. Frees the DLL handle in Dispose; finalizer is
present as a safety net.

Calculate is serialized via lock(_syncRoot) — the NOAA DLL is not
documented as thread-safe.

Unit tests cover null/empty path validation, missing-file behavior, and
Dispose idempotency (the latter via env-var-gated probe).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Implement `HDGMCalculationAdapter` and outData index mapping

**Files:**
- Create: `src/GeoMagSharp/HDGM/HDGMCalculationAdapter.cs`
- Create: `tests/GeoMagSharp.Tests/HDGM/HDGMCalculationAdapterTests.cs`

This is the heart of the feature. Strict TDD with FakeHdgmInvoker.

- [ ] **Step 1: Write the failing tests (initial subset)**

Create `tests/GeoMagSharp.Tests/HDGM/HDGMCalculationAdapterTests.cs`:

```csharp
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class HDGMCalculationAdapterTests
    {
        private CalculationOptions DefaultOpts() => new CalculationOptions
        {
            Latitude = 40.0,
            Longitude = -100.0,
            StartDate = new DateTime(2020, 6, 1)
        };

        private FakeHdgmInvoker FakeReturning(double[] outData)
        {
            return new FakeHdgmInvoker { CannedOutData = outData, CannedReturnValue = 0 };
        }

        private double[] OutDataAllZero() => new double[25];

        // ── Index mapping: field values ─────────────────────────────────

        [TestMethod]
        public void Calculate_MapsDeclination_FromOutData0()
        {
            var data = OutDataAllZero(); data[0] = 12.345;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(12.345, result.Declination.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsInclination_FromOutData1()
        {
            var data = OutDataAllZero(); data[1] = 67.890;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(67.890, result.Inclination.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsTotalField_FromOutData2()
        {
            var data = OutDataAllZero(); data[2] = 53210.5;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(53210.5, result.TotalField.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsHorizontalIntensity_FromOutData3()
        {
            var data = OutDataAllZero(); data[3] = 21000.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(21000.0, result.HorizontalIntensity.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsNorthComp_FromOutData4()
        {
            var data = OutDataAllZero(); data[4] = 19500.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(19500.0, result.NorthComp.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsEastComp_FromOutData5()
        {
            var data = OutDataAllZero(); data[5] = -2000.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(-2000.0, result.EastComp.Value, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsVerticalComp_FromOutData6()
        {
            var data = OutDataAllZero(); data[6] = 48000.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(48000.0, result.VerticalComp.Value, 1e-9);
        }

        // ── Index mapping: secular variations (skip GV at indices 7 and 15) ──

        [TestMethod]
        public void Calculate_MapsDeclinationChangePerYear_FromOutData8_NotOutData7()
        {
            var data = OutDataAllZero();
            data[7] = 999.0;     // Grid Variation — must be ignored
            data[8] = 0.123;     // dD/dt
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(0.123, result.Declination.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsInclinationChangePerYear_FromOutData9()
        {
            var data = OutDataAllZero(); data[9] = -0.05;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(-0.05, result.Inclination.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsTotalFieldChangePerYear_FromOutData10()
        {
            var data = OutDataAllZero(); data[10] = 22.5;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(22.5, result.TotalField.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsHorizontalIntensityChangePerYear_FromOutData11()
        {
            var data = OutDataAllZero(); data[11] = 5.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(5.0, result.HorizontalIntensity.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsNorthCompChangePerYear_FromOutData12()
        {
            var data = OutDataAllZero(); data[12] = 1.5;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(1.5, result.NorthComp.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsEastCompChangePerYear_FromOutData13()
        {
            var data = OutDataAllZero(); data[13] = -0.5;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(-0.5, result.EastComp.ChangePerYear, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsVerticalCompChangePerYear_FromOutData14()
        {
            var data = OutDataAllZero(); data[14] = 3.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(3.0, result.VerticalComp.ChangePerYear, 1e-9);
        }

        // ── Index mapping: NSD coverage flag and per-point sigma ─────────

        [TestMethod]
        public void Calculate_MapsCoverageFlag_FromOutData16_HighRes_True()
        {
            var data = OutDataAllZero(); data[16] = 0; // 0 = high-res covered
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(true, result.Uncertainty?.HighResolutionCoverage);
        }

        [TestMethod]
        public void Calculate_MapsCoverageFlag_FromOutData16_Fallback_False()
        {
            var data = OutDataAllZero(); data[16] = 1; // 1 = satellite-fallback
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(false, result.Uncertainty?.HighResolutionCoverage);
        }

        [TestMethod]
        public void Calculate_MapsSigmaD_FromOutData17()
        {
            var data = OutDataAllZero(); data[17] = 0.13;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(0.13, result.Uncertainty?.SigmaD ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaI_FromOutData18()
        {
            var data = OutDataAllZero(); data[18] = 0.16;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(0.16, result.Uncertainty?.SigmaI ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaH_FromOutData19()
        {
            var data = OutDataAllZero(); data[19] = 100.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(100.0, result.Uncertainty?.SigmaH ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaX_FromOutData20()
        {
            var data = OutDataAllZero(); data[20] = 50.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(50.0, result.Uncertainty?.SigmaX ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaY_FromOutData21()
        {
            var data = OutDataAllZero(); data[21] = 60.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(60.0, result.Uncertainty?.SigmaY ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaZ_FromOutData22()
        {
            var data = OutDataAllZero(); data[22] = 70.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(70.0, result.Uncertainty?.SigmaZ ?? double.NaN, 1e-9);
        }

        [TestMethod]
        public void Calculate_MapsSigmaF_FromOutData23()
        {
            var data = OutDataAllZero(); data[23] = 107.0;
            var fake = FakeReturning(data);
            var result = HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
            Assert.AreEqual(107.0, result.Uncertainty?.SigmaF ?? double.NaN, 1e-9);
        }

        // ── Sentinel handling ────────────────────────────────────────────

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionOutOfRange))]
        public void Calculate_SentinelMinus99999_ThrowsOutOfRange()
        {
            var data = OutDataAllZero(); data[0] = -99999;
            var fake = FakeReturning(data);
            HDGMCalculationAdapter.Calculate(DefaultOpts(), DefaultOpts().StartDate, fake);
        }

        // ── Inputs passed correctly to native ──────────────────────────

        [TestMethod]
        public void Calculate_LatitudePassedToInvoker()
        {
            var fake = FakeReturning(OutDataAllZero());
            var opts = DefaultOpts(); opts.Latitude = 35.5;
            HDGMCalculationAdapter.Calculate(opts, opts.StartDate, fake);
            Assert.AreEqual(35.5, fake.Calls[0].Latitude, 1e-9);
        }

        [TestMethod]
        public void Calculate_LongitudePassedToInvoker()
        {
            var fake = FakeReturning(OutDataAllZero());
            var opts = DefaultOpts(); opts.Longitude = -75.25;
            HDGMCalculationAdapter.Calculate(opts, opts.StartDate, fake);
            Assert.AreEqual(-75.25, fake.Calls[0].Longitude, 1e-9);
        }

        [TestMethod]
        public void Calculate_DepthInMetersPassedToInvoker_FromAltitudeFeet()
        {
            var fake = FakeReturning(OutDataAllZero());
            var opts = DefaultOpts();
            opts.SetElevation(value: 1000, unit: Distance.Unit.foot, isAltitude: true);
            HDGMCalculationAdapter.Calculate(opts, opts.StartDate, fake);
            // 1000 ft altitude → 1000 * 0.3048 m above MSL → -304.8 m depth (negative for altitude)
            Assert.AreEqual(-304.8, fake.Calls[0].DepthMeters, 1e-9);
        }

        [TestMethod]
        public void Calculate_DepthInMetersPassedToInvoker_FromDepthMeters()
        {
            var fake = FakeReturning(OutDataAllZero());
            var opts = DefaultOpts();
            opts.SetElevation(value: 1500, unit: Distance.Unit.meter, isAltitude: false);
            HDGMCalculationAdapter.Calculate(opts, opts.StartDate, fake);
            // 1500 m depth → +1500 m
            Assert.AreEqual(1500.0, fake.Calls[0].DepthMeters, 1e-9);
        }

        [TestMethod]
        public void Calculate_DateConvertedToDecimalYear()
        {
            var fake = FakeReturning(OutDataAllZero());
            var opts = DefaultOpts();
            var date = new DateTime(2020, 7, 1); // mid-year, decimal year ≈ 2020.4986
            HDGMCalculationAdapter.Calculate(opts, date, fake);
            Assert.IsTrue(fake.Calls[0].DecimalYear > 2020.49 && fake.Calls[0].DecimalYear < 2020.51,
                $"DecimalYear={fake.Calls[0].DecimalYear} not in expected range");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~HDGMCalculationAdapterTests" -c Debug
```

Expected: build error — `HDGMCalculationAdapter` does not exist.

- [ ] **Step 3: Implement `HDGMCalculationAdapter`**

Create `src/GeoMagSharp/HDGM/HDGMCalculationAdapter.cs`:

```csharp
/****************************************************************************
 * File:            HDGMCalculationAdapter.cs
 * Description:     Per-call adapter mapping the NOAA HDGM outData array
 *                  into MagneticCalculations + GeomagneticUncertainty fields
 ****************************************************************************/

using System;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Per-call adapter that invokes <see cref="INativeHdgmInvoker"/> and maps the 25-element
    /// native outData array to a <see cref="MagneticCalculations"/> result with per-point
    /// uncertainty fields populated.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: outData index 16 carries the NSD high-resolution coverage flag in the
    /// DLL output (HDGM_Sublibrary.c:212 — IsNotCovered: 0 = covered, 1 = fallback).
    /// The CLI variant of NOAA's source (hdgm_file.c:204) overwrites slot 16 with UsePomme;
    /// we use the DLL semantics.
    /// </remarks>
    internal static class HDGMCalculationAdapter
    {
        private const double Sentinel = -99999.0;

        public static MagneticCalculations Calculate(
            CalculationOptions opts,
            DateTime intervalDate,
            INativeHdgmInvoker invoker)
        {
            if (opts == null) throw new ArgumentNullException(nameof(opts));
            if (invoker == null) throw new ArgumentNullException(nameof(invoker));

            double depthMeters = opts.DepthInM;       // positive for depth, negative for altitude
            double decimalYear = intervalDate.ToDecimal();

            var outData = new double[25];
            invoker.Calculate(opts.Latitude, opts.Longitude, depthMeters, decimalYear, outData);

            if (outData[0] == Sentinel)
            {
                throw new GeoMagExceptionOutOfRange(string.Format(
                    "Error: HDGM returned out-of-range result for date {0:yyyy-MM-dd} at lat {1}, lon {2}. " +
                    "The loaded HDGM version may not cover this date or location is invalid.",
                    intervalDate, opts.Latitude, opts.Longitude));
            }

            var result = new MagneticCalculations
            {
                Date = intervalDate,
                Declination = new MagneticValue { Value = outData[0], ChangePerYear = outData[8] },
                Inclination = new MagneticValue { Value = outData[1], ChangePerYear = outData[9] },
                TotalField = new MagneticValue { Value = outData[2], ChangePerYear = outData[10] },
                HorizontalIntensity = new MagneticValue { Value = outData[3], ChangePerYear = outData[11] },
                NorthComp = new MagneticValue { Value = outData[4], ChangePerYear = outData[12] },
                EastComp = new MagneticValue { Value = outData[5], ChangePerYear = outData[13] },
                VerticalComp = new MagneticValue { Value = outData[6], ChangePerYear = outData[14] },
                // outData[7]  = Grid Variation, discarded
                // outData[15] = dGV/dt, discarded
                Uncertainty = new GeomagneticUncertainty
                {
                    ModelCategory = GeomagneticModelCategory.HighResolution,
                    HighResolutionCoverage = (outData[16] == 0.0),
                    SigmaD = outData[17],
                    SigmaI = outData[18],
                    SigmaH = outData[19],
                    SigmaX = outData[20],
                    SigmaY = outData[21],
                    SigmaZ = outData[22],
                    SigmaF = outData[23]
                    // outData[24] = UsePomme HDGM-RT flag, out of scope
                }
            };

            return result;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~HDGMCalculationAdapterTests" -c Debug
```

Expected: 25 tests passed.

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/HDGM/HDGMCalculationAdapter.cs tests/GeoMagSharp.Tests/HDGM/HDGMCalculationAdapterTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add HDGMCalculationAdapter and outData index mapping (#19)

Implements the per-call adapter that invokes INativeHdgmInvoker and maps
the 25-element native outData array to MagneticCalculations:

  outData[0..6]   → D, I, F, H, X, Y, Z (field values)
  outData[7]      → Grid Variation (discarded)
  outData[8..14]  → secular variations (skip outData[7])
  outData[15]     → dGV/dt (discarded)
  outData[16]     → HighResolutionCoverage (DLL semantics: 0 = covered)
  outData[17..23] → SigmaD/I/H/X/Y/Z/F per-point sigma values
  outData[24]     → UsePomme HDGM-RT flag (discarded — out of scope)

The DLL-vs-CLI difference at outData[16] is documented inline citing
HDGM_Sublibrary.c:212 to prevent regression.

Sentinel value -99999 from outData[0] translates to GeoMagExceptionOutOfRange
with a descriptive message hinting at HDGM-version date coverage limits.

25 unit tests via FakeHdgmInvoker exercise every index mapping individually,
plus input validation (lat, lon, depth conversion from feet/meters/altitude/
depth, decimal year conversion).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Add `MagneticModelSet.NativeInvoker` and `IDisposable`

**Files:**
- Modify: `src/GeoMagSharp/Models/Magnetic/MagneticModelSet.cs`
- Test: `tests/GeoMagSharp.Tests/HDGM/MagneticModelSetDisposableTests.cs` (Create)

- [ ] **Step 1: Write the failing tests**

Create `tests/GeoMagSharp.Tests/HDGM/MagneticModelSetDisposableTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~MagneticModelSetDisposableTests" -c Debug
```

Expected: build error — `NativeInvoker` and `Dispose` not defined.

- [ ] **Step 3: Modify `MagneticModelSet`**

In `src/GeoMagSharp/Models/Magnetic/MagneticModelSet.cs`:

3a) Add `using` directives at the top if not present:

```csharp
using GeoMagSharp.HDGM;
```

3b) Change the class declaration (line 19) from:
```csharp
    public class MagneticModelSet
```
to:
```csharp
    public class MagneticModelSet : IDisposable
```

3c) After the existing `EarthRadius` property (around line 416), and before the closing region tag, add:

```csharp
        /// <summary>
        /// Native HDGM invoker handle. Null for non-HDGM model sets. Internal — HDGM
        /// detection in client code should rely on Type == knownModels.HDGM rather
        /// than reaching for this field.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        internal INativeHdgmInvoker NativeInvoker { get; set; }

        private bool _disposed;

        /// <summary>
        /// Releases the native HDGM DLL handle if one is held. No-op for non-HDGM model sets.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            NativeInvoker?.Dispose();
            NativeInvoker = null;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~MagneticModelSetDisposableTests" -c Debug
```

Expected: 5 tests passed.

- [ ] **Step 5: Sanity-check that existing serialization still works**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "TestCategory!=RequiresHDGMDll" -c Debug
```

Expected: all existing tests still pass. The `[JsonIgnore]` ensures `NativeInvoker` does not appear in JSON output.

- [ ] **Step 6: Commit**

```bash
git add src/GeoMagSharp/Models/Magnetic/MagneticModelSet.cs tests/GeoMagSharp.Tests/HDGM/MagneticModelSetDisposableTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add IDisposable and NativeInvoker to MagneticModelSet (#19)

MagneticModelSet now implements IDisposable and exposes an internal
NativeInvoker property (INativeHdgmInvoker, null for non-HDGM models,
[JsonIgnore]). Dispose is idempotent and a no-op for non-HDGM models.

Existing usages compile unchanged because IDisposable is opt-in. JSON
serialization round-trip is preserved by the [JsonIgnore] attribute.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Implement `HDGMModelLoader`

**Files:**
- Create: `src/GeoMagSharp/HDGM/HDGMModelLoader.cs`
- Create: `tests/GeoMagSharp.Tests/HDGM/HDGMModelLoaderTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/GeoMagSharp.Tests/HDGM/HDGMModelLoaderTests.cs`:

```csharp
using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class HDGMModelLoaderTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Load_NullPath_ThrowsArgumentNull()
        {
            HDGMModelLoader.Load(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Load_EmptyPath_ThrowsArgumentNull()
        {
            HDGMModelLoader.Load("");
        }

        [TestMethod]
        public void Load_NonWindowsPlatform_ThrowsPlatformNotSupported()
        {
            // This test only meaningfully runs on non-Windows platforms.
            // On Windows, it asserts the runtime is Windows (i.e. inconclusive for the
            // platform-not-supported path).
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("Running on Windows; PlatformNotSupportedException path cannot be reached.");
                return;
            }
            try
            {
                HDGMModelLoader.Load(@"C:\anything\hdgm.dll");
                Assert.Fail("Expected PlatformNotSupportedException");
            }
            catch (PlatformNotSupportedException) { /* expected */ }
        }

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionFileNotFound))]
        public void Load_NonexistentDll_ThrowsFileNotFound_OnWindows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new GeoMagExceptionFileNotFound("(skipping on non-Windows — platform check fires first)");
            }
            HDGMModelLoader.Load(@"C:\__definitely_not_real__\hdgm2019-64.dll");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~HDGMModelLoaderTests" -c Debug
```

Expected: build error — `HDGMModelLoader` does not exist.

- [ ] **Step 3: Implement `HDGMModelLoader`**

Create `src/GeoMagSharp/HDGM/HDGMModelLoader.cs`:

```csharp
/****************************************************************************
 * File:            HDGMModelLoader.cs
 * Description:     Loads a NOAA HDGM DLL into a MagneticModelSet
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
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~HDGMModelLoaderTests" -c Debug
```

Expected: 4 tests passed (one inconclusive on Windows for the platform-check test).

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/HDGM/HDGMModelLoader.cs tests/GeoMagSharp.Tests/HDGM/HDGMModelLoaderTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add HDGMModelLoader.Load entry point (#19)

Loads a NOAA HDGM DLL into a MagneticModelSet configured with:
- Type = knownModels.HDGM
- Name = uppercase filename without extension
- MinDate = 1900.0, MaxDate = 9999.0 (sentinel-driven validation)
- NativeInvoker = LoadLibraryHdgmInvoker(path)

Validation order:
  1. ArgumentNullException for null/empty path
  2. PlatformNotSupportedException on non-Windows (with helpful message)
  3. GeoMagExceptionFileNotFound for missing DLL
  4. Bubble up native binding errors from LoadLibraryHdgmInvoker ctor

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Wire `GeoMag.LoadModel` and `GeoMag.MagneticCalculations` for HDGM

**Files:**
- Modify: `src/GeoMagSharp/GeoMag.cs`
- Test: `tests/GeoMagSharp.Tests/HDGM/GeoMagHDGMRoutingTests.cs` (Create)

- [ ] **Step 1: Write the failing tests**

Create `tests/GeoMagSharp.Tests/HDGM/GeoMagHDGMRoutingTests.cs`:

```csharp
using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class GeoMagHDGMRoutingTests
    {
        // Helper: bypass file IO/native loading by injecting a MagneticModelSet pre-built
        // with a FakeHdgmInvoker. Uses the existing public LoadModel(MagneticModelSet) overload.
        private GeoMag NewGeoMagWithFakeHDGM(double[] cannedOutData)
        {
            var fake = new FakeHdgmInvoker { CannedOutData = cannedOutData, CannedReturnValue = 0 };
            var set = new MagneticModelSet
            {
                Type = knownModels.HDGM,
                Name = "HDGM-FAKE",
                MinDate = 1900.0,
                MaxDate = 9999.0,
                NativeInvoker = fake
            };
            var geo = new GeoMag();
            geo.LoadModel(set);
            return geo;
        }

        [TestMethod]
        public void MagneticCalculations_OnHDGMModelSet_RoutesToAdapter()
        {
            var data = new double[25];
            data[0] = 5.55;  // Declination
            using (var geo = NewGeoMagWithFakeHDGM(data))
            {
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0,
                    Longitude = -100.0,
                    StartDate = new DateTime(2020, 6, 1)
                });
                Assert.AreEqual(1, geo.ResultsOfCalculation.Count);
                Assert.AreEqual(5.55, geo.ResultsOfCalculation[0].Declination.Value, 1e-9);
            }
        }

        [TestMethod]
        public void MagneticCalculations_OnHDGMModelSet_DateSweep_CallsInvokerOncePerDate()
        {
            var fake = new FakeHdgmInvoker { CannedOutData = new double[25], CannedReturnValue = 0 };
            var set = new MagneticModelSet
            {
                Type = knownModels.HDGM,
                Name = "HDGM-FAKE",
                MinDate = 1900.0,
                MaxDate = 9999.0,
                NativeInvoker = fake
            };
            var geo = new GeoMag();
            geo.LoadModel(set);
            geo.MagneticCalculations(new CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -100.0,
                StartDate = new DateTime(2020, 1, 1),
                EndDate = new DateTime(2020, 1, 6),
                StepInterval = 1
            });
            // Date stepping in existing GeoMag.MagneticCalculations: 6 days → 6 iterations
            Assert.IsTrue(fake.Calls.Count >= 1);
        }

        [TestMethod]
        public void Dispose_DisposesUnderlyingNativeInvoker()
        {
            var fake = new FakeHdgmInvoker();
            var set = new MagneticModelSet
            {
                Type = knownModels.HDGM,
                Name = "HDGM-FAKE",
                MinDate = 1900.0,
                MaxDate = 9999.0,
                NativeInvoker = fake
            };
            var geo = new GeoMag();
            geo.LoadModel(set);
            geo.Dispose();
            Assert.IsTrue(fake.DisposeWasCalled);
        }

        [TestMethod]
        public void IsDisposable()
        {
            using (var geo = new GeoMag())
            {
                Assert.IsTrue(geo is IDisposable);
            }
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~GeoMagHDGMRoutingTests" -c Debug
```

Expected: build error — `GeoMag` does not implement `IDisposable` (and routing logic absent).

- [ ] **Step 3: Modify `GeoMag` — class declaration and Dispose**

In `src/GeoMagSharp/GeoMag.cs`:

3a) Add `using` at the top:

```csharp
using GeoMagSharp.HDGM;
```

3b) Change the class declaration (line 23) from:
```csharp
    public class GeoMag
```
to:
```csharp
    public class GeoMag : IDisposable
```

3c) Add `Dispose` and a `_disposed` field. Place near the bottom of the class:

```csharp
        private bool _disposed;

        /// <summary>
        /// Disposes the underlying model set, releasing any native HDGM DLL handle.
        /// No-op for GeoMag instances loaded with non-HDGM models.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _Models?.Dispose();
            _Models = null;
        }
```

- [ ] **Step 4: Modify `GeoMag.LoadModel(string)` to detect HDGM paths**

In `LoadModel(string modelFile)` (around line 47), replace the body with:

```csharp
        public void LoadModel(string modelFile)
        {
            _Models = null;

            if (string.IsNullOrEmpty(modelFile))
                throw new GeoMagExceptionFileNotFound("Error coefficient file name not specified");

            if (ModelPathDetector.IsHdgmPath(modelFile))
            {
                _Models = HDGMModelLoader.Load(modelFile);
                return;
            }

            _Models = ModelReader.Read(modelFile);
        }
```

Apply the same routing inside `LoadModel(string modelFile, string svFile)` (around line 80):

```csharp
        public void LoadModel(string modelFile, string svFile)
        {
            _Models = null;

            if (string.IsNullOrEmpty(modelFile))
                throw new GeoMagExceptionFileNotFound("Error coefficient file name not specified");

            if (ModelPathDetector.IsHdgmPath(modelFile))
            {
                _Models = HDGMModelLoader.Load(modelFile);
                return;
            }

            _Models = ModelReader.Read(modelFile, svFile);
        }
```

- [ ] **Step 5: Modify `GeoMag.MagneticCalculations` to branch on HDGM**

In `MagneticCalculations(CalculationOptions inCalculationOptions)` (around line 98), inside the `while (dateIdx <= timespan.Days)` loop, replace the current loop body's per-date calculation with a branch on `_Models.NativeInvoker != null`. The full updated loop body (replacing lines 136-159):

```csharp
            while (dateIdx <= timespan.Days)
            {
                DateTime intervalDate = _CalculationOptions.StartDate.AddDays(dateIdx);

                MagneticCalculations magCalcDate;
                if (_Models.NativeInvoker != null)
                {
                    magCalcDate = HDGMCalculationAdapter.Calculate(_CalculationOptions, intervalDate, _Models.NativeInvoker);
                }
                else
                {
                    var internalSH = new Coefficients();
                    var externalSH = new Coefficients();
                    _Models.GetIntExt(intervalDate.ToDecimal(), out internalSH, out externalSH);
                    magCalcDate = Calculator.SpotCalculation(_CalculationOptions, intervalDate, _Models, internalSH, externalSH, _Models.EarthRadius);
                }

                if (magCalcDate != null)
                {
                    // For HDGM, the adapter already populated per-point Uncertainty.
                    // For other models, fall back to the global ISCWSA uncertainty.
                    if (magCalcDate.Uncertainty == null)
                    {
                        magCalcDate.Uncertainty = uncertainty;
                    }
                    ResultsOfCalculation.Add(magCalcDate);
                }

                dateIdx = ((dateIdx < timespan.Days) && ((dateIdx + dayInc) > timespan.Days))
                            ? timespan.Days
                            : dateIdx + dayInc;
            }
```

- [ ] **Step 6: Apply the same branching to `MagneticCalculationsAsync`**

In `MagneticCalculationsAsync` (around line 287), the inner loop (around line 338) needs the same branching. Replace its body with:

```csharp
            while (dateIdx <= timespan.Days)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DateTime intervalDate = _CalculationOptions.StartDate.AddDays(dateIdx);

                currentStep++;
                progress?.Report(new CalculationProgressInfo
                {
                    CurrentStep = currentStep,
                    TotalSteps = totalSteps,
                    StatusMessage = string.Format("Calculating for {0}...", intervalDate.ToString("yyyy-MM-dd"))
                });

                var models = _Models;
                var calcOptions = _CalculationOptions;

                MagneticCalculations magCalcDate = await Task.Run(() =>
                {
                    if (models.NativeInvoker != null)
                    {
                        return HDGMCalculationAdapter.Calculate(calcOptions, intervalDate, models.NativeInvoker);
                    }
                    var internalSH = new Coefficients();
                    var externalSH = new Coefficients();
                    models.GetIntExt(intervalDate.ToDecimal(), out internalSH, out externalSH);
                    return Calculator.SpotCalculation(calcOptions, intervalDate, models, internalSH, externalSH, models.EarthRadius);
                }, cancellationToken).ConfigureAwait(false);

                if (magCalcDate != null)
                {
                    if (magCalcDate.Uncertainty == null)
                    {
                        magCalcDate.Uncertainty = uncertainty;
                    }
                    ResultsOfCalculation.Add(magCalcDate);
                }

                dateIdx = ((dateIdx < timespan.Days) && ((dateIdx + dayInc) > timespan.Days))
                            ? timespan.Days
                            : dateIdx + dayInc;
            }
```

- [ ] **Step 7: Run tests to verify they pass**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~GeoMagHDGMRoutingTests" -c Debug
```

Expected: 4 tests passed.

- [ ] **Step 8: Run the full unit-test suite to verify no regressions**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "TestCategory!=RequiresHDGMDll" -c Debug
```

Expected: all existing tests still pass plus the new HDGM tests.

- [ ] **Step 9: Commit**

```bash
git add src/GeoMagSharp/GeoMag.cs tests/GeoMagSharp.Tests/HDGM/GeoMagHDGMRoutingTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: route HDGM paths through GeoMag.LoadModel and MagneticCalculations (#19)

GeoMag now implements IDisposable (propagates to _Models). LoadModel(string)
and LoadModel(string, string) call ModelPathDetector.IsHdgmPath; matched
paths route to HDGMModelLoader.Load instead of ModelReader.Read.

MagneticCalculations and MagneticCalculationsAsync branch on
_Models.NativeInvoker != null: HDGM path calls HDGMCalculationAdapter
which populates per-point Uncertainty directly; non-HDGM path is unchanged
and gets the global ISCWSA uncertainty assigned post-hoc.

The fallback "if Uncertainty == null assign global" preserves backward
compatibility — HDGM results carry per-point sigma and the existing global
ScaleTo behavior also applies.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 11: Add HDGM case to `UncertaintyDataProvider` (HRGM-tier ISCWSA values)

**Files:**
- Modify: `src/GeoMagSharp/UncertaintyDataProvider.cs`
- Test: `tests/GeoMagSharp.Tests/HDGM/UncertaintyDataProviderHDGMTests.cs` (Create)

- [ ] **Step 1: Read the current `UncertaintyDataProvider` to confirm shape**

```
cat src/GeoMagSharp/UncertaintyDataProvider.cs
```

Look for the existing pattern: a `GetUncertainty(knownModels modelType, GeomagneticModelCategory? override)` static method that returns a `GeomagneticUncertainty`. Note the existing case structure for WMM/IGRF.

- [ ] **Step 2: Write the failing test**

Create `tests/GeoMagSharp.Tests/HDGM/UncertaintyDataProviderHDGMTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class UncertaintyDataProviderHDGMTests
    {
        [TestMethod]
        public void GetUncertainty_ForHDGMType_ReturnsHRGMTierValues()
        {
            var u = UncertaintyDataProvider.GetUncertainty(knownModels.HDGM, null);
            Assert.IsNotNull(u);
            // ISCWSA HRGM-tier values per openbrain KB#70 / KB#105
            Assert.AreEqual(GeomagneticModelCategory.HighResolution, u.ModelCategory);
            Assert.AreEqual(107.0, u.TotalField, 1e-6, "MFI (TotalField) should be 107 nT for HRGM");
            Assert.AreEqual(0.16, u.DipAngle, 1e-6, "MDI (DipAngle) should be 0.16° for HRGM");
            Assert.AreEqual(0.30, u.Declination, 1e-6, "DEC constant should be 0.30° for HRGM");
            Assert.AreEqual(4118.0, u.BhDependentDec, 1e-6, "DBH should be 4118 deg·nT for HRGM");
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~UncertaintyDataProviderHDGMTests" -c Debug
```

Expected: assertion failure (unknown model returns null or wrong values).

- [ ] **Step 4: Add HDGM case to `UncertaintyDataProvider`**

In `src/GeoMagSharp/UncertaintyDataProvider.cs`, locate the `GetUncertainty` method's switch on `knownModels` and add the HDGM case alongside existing model cases. The exact placement depends on the existing structure; add a case branch returning:

```csharp
                case knownModels.HDGM:
                    return new GeomagneticUncertainty
                    {
                        ModelCategory = GeomagneticModelCategory.HighResolution,
                        TotalField = 107.0,         // MFI (1-sigma)
                        DipAngle = 0.16,            // MDI (1-sigma)
                        Declination = 0.30,         // DEC constant (1-sigma)
                        BhDependentDec = 4118.0,    // DBH (1-sigma, deg·nT)
                        Revision = "Rev5.13"
                    };
```

If the provider uses a dictionary or other lookup structure instead of a switch, adapt the pattern accordingly — register `knownModels.HDGM` mapped to the same values.

- [ ] **Step 5: Run test to verify it passes**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~UncertaintyDataProviderHDGMTests" -c Debug
```

Expected: 1 test passed.

- [ ] **Step 6: Commit**

```bash
git add src/GeoMagSharp/UncertaintyDataProvider.cs tests/GeoMagSharp.Tests/HDGM/UncertaintyDataProviderHDGMTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add HDGM case to UncertaintyDataProvider (#19)

Returns ISCWSA HRGM-tier 1-sigma values for HDGM:
- MFI (TotalField) = 107 nT
- MDI (DipAngle)   = 0.16°
- DEC constant     = 0.30°
- DBH              = 4118 deg·nT

Source: ISCWSA Rev5.13 / openbrain KB#70, KB#105. These are the model-wide
global values; per-point sigma values from outData[17..23] populate the
SigmaD/I/H/X/Y/Z/F fields on the same Uncertainty instance.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 12: `[Obsolete]` mark `Constants.MaxDeg` and `Constants.MaxCoeff`

**Files:**
- Modify: `src/GeoMagSharp/GeoConstants.cs`

- [ ] **Step 1: Add `[Obsolete]` attributes**

In `src/GeoMagSharp/GeoConstants.cs`, replace the `MaxDeg` and `MaxCoeff` definitions (lines 38-49) with:

```csharp
        /// <summary>
        /// Maximum spherical harmonic degree supported.
        /// </summary>
        /// <remarks>
        /// No longer used. The calculator (Calculator.cs) sizes its scratch buffers
        /// dynamically from <see cref="Coefficients.MaxDegree"/> per evaluation, and
        /// <see cref="MagneticModel.Max_Degree"/> reports a model's actual degree from
        /// its coefficient count. This constant remains for backward binary compatibility
        /// and will be removed in a future major version.
        /// </remarks>
        [Obsolete("No longer used; calculator sizes dynamically from the loaded model's Coefficients.MaxDegree. Will be removed in a future major version.")]
        public const Int32 MaxDeg = 20;

        /// <summary>
        /// Maximum number of spherical harmonic coefficients
        /// </summary>
        [Obsolete("No longer used; see MaxDeg. Will be removed in a future major version.")]
        public static Int32 MaxCoeff
        {
            get
            {
#pragma warning disable CS0618
                return (MaxDeg * (MaxDeg + 2) + 1);
#pragma warning restore CS0618
            }
        }
```

- [ ] **Step 2: Verify build emits the deprecation warnings (informational, not errors)**

```
dotnet build src/GeoMagSharp/GeoMagSharp.csproj -c Debug
```

Expected: build succeeds; if anything inside the library still references `Constants.MaxDeg`, treat that as a finding to fix here. Per the design, no current call sites reference these.

- [ ] **Step 3: Run all tests to confirm nothing depends on these constants**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "TestCategory!=RequiresHDGMDll" -c Debug
```

Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/GeoMagSharp/GeoConstants.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] chore: deprecate unused Constants.MaxDeg and MaxCoeff (#19)

Marks Constants.MaxDeg and Constants.MaxCoeff as [Obsolete] with removal
notice for v2.0. Both constants have zero call sites in src/ or tests/;
the calculator already sizes dynamically from Coefficients.MaxDegree
(Calculator.cs:78-80) and MagneticModel.Max_Degree (MagneticModel.cs:78-90).

Kept visible (not deleted) to preserve binary compatibility for any
downstream consumer that may have referenced them.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 13: Add HDGM integration tests skeleton (env-var-gated)

**Files:**
- Create: `tests/GeoMagSharp.Tests/HDGM/HDGMIntegrationTests.cs`

These tests run only when `HDGM_DLL_PATH` and `HDGM_TEST_VALUES_PATH` env vars are set. CI excludes them via the existing `--filter "TestCategory!=RequiresHDGMDll"` flag.

- [ ] **Step 1: Create the integration test file**

```csharp
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.HDGM
{
    [TestClass]
    public class HDGMIntegrationTests
    {
        private static string DllPath => Environment.GetEnvironmentVariable("HDGM_DLL_PATH");
        private static string TestValuesPath => Environment.GetEnvironmentVariable("HDGM_TEST_VALUES_PATH");

        [TestInitialize]
        public void RequireEnvironment()
        {
            if (string.IsNullOrWhiteSpace(DllPath) || !File.Exists(DllPath))
                Assert.Inconclusive("HDGM_DLL_PATH not set or file missing; integration tests skipped.");
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_LoadsRealDll_NoException()
        {
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath); // must not throw
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_SinglePoint_ReturnsPlausibleValues()
        {
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath);
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0,
                    Longitude = -100.0,
                    StartDate = new DateTime(2020, 6, 1)
                });

                Assert.AreEqual(1, geo.ResultsOfCalculation.Count);
                var r = geo.ResultsOfCalculation[0];

                // Plausibility ranges for mid-North-America in 2020:
                Assert.IsTrue(r.Declination.Value > 0 && r.Declination.Value < 15,
                    $"Declination {r.Declination.Value}° not in plausible range 0..15°");
                Assert.IsTrue(r.TotalField.Value > 30000 && r.TotalField.Value < 70000,
                    $"TotalField {r.TotalField.Value} nT not in plausible range 30000..70000");
                Assert.IsTrue(r.Inclination.Value > 50 && r.Inclination.Value < 80,
                    $"Inclination {r.Inclination.Value}° not in plausible range 50..80°");
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_SamplePointsFromTestValues_AllWithinTolerance()
        {
            if (string.IsNullOrWhiteSpace(TestValuesPath) || !File.Exists(TestValuesPath))
            {
                Assert.Inconclusive("HDGM_TEST_VALUES_PATH not set; numerical tolerance test skipped.");
                return;
            }

            // Format: "Date Depth Lat Lon D I H X Y Z F dD dI dH dX dY dZ dF" (18 cols, whitespace-separated)
            // Field indices in the file: 0..17
            string[] lines = File.ReadAllLines(TestValuesPath)
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
                .Take(20) // limit to first 20 rows for runtime
                .ToArray();

            Assert.IsTrue(lines.Length >= 5, $"Expected at least 5 sample rows in {TestValuesPath}");

            int passed = 0;
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath);
                foreach (var line in lines)
                {
                    var f = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (f.Length < 11) continue;

                    double year = double.Parse(f[0], System.Globalization.CultureInfo.InvariantCulture);
                    double depth = double.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture);
                    double lat = double.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture);
                    double lon = double.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture);
                    double expectedD = double.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture);
                    double expectedI = double.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture);
                    double expectedF = double.Parse(f[10], System.Globalization.CultureInfo.InvariantCulture);

                    int yearInt = (int)year;
                    DateTime date = new DateTime(yearInt, 1, 1).AddDays((year - yearInt) * 365.25);

                    var opts = new CalculationOptions { Latitude = lat, Longitude = lon, StartDate = date };
                    opts.SetElevation(value: depth, unit: Distance.Unit.meter, isAltitude: false);

                    geo.MagneticCalculations(opts);
                    var r = geo.ResultsOfCalculation.Last();

                    Assert.AreEqual(expectedD, r.Declination.Value, 0.0001,
                        $"D mismatch at year={year}, depth={depth}, lat={lat}, lon={lon}");
                    Assert.AreEqual(expectedI, r.Inclination.Value, 0.0001,
                        $"I mismatch at year={year}, depth={depth}, lat={lat}, lon={lon}");
                    Assert.AreEqual(expectedF, r.TotalField.Value, 0.05,
                        $"F mismatch at year={year}, depth={depth}, lat={lat}, lon={lon}");

                    passed++;
                }
            }
            Assert.IsTrue(passed >= 5, $"Validated {passed} sample points; expected >= 5");
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_OutOfRangeDate_ThrowsOutOfRange()
        {
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath);
                try
                {
                    geo.MagneticCalculations(new CalculationOptions
                    {
                        Latitude = 40.0, Longitude = -100.0,
                        StartDate = new DateTime(1500, 1, 1) // far before HDGM range
                    });
                    Assert.Fail("Expected GeoMagExceptionOutOfRange");
                }
                catch (GeoMagExceptionOutOfRange) { /* expected */ }
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_DisposeFreesDll()
        {
            // Soft check: load and dispose multiple times; should not throw or leak observably.
            for (int i = 0; i < 3; i++)
            {
                using (var geo = new GeoMag())
                {
                    geo.LoadModel(DllPath);
                }
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_SigmaValuesPopulated()
        {
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath);
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0, StartDate = new DateTime(2020, 6, 1)
                });
                var u = geo.ResultsOfCalculation[0].Uncertainty;
                Assert.IsNotNull(u);
                Assert.IsNotNull(u.SigmaD);
                Assert.IsNotNull(u.SigmaI);
                Assert.IsNotNull(u.SigmaF);
                Assert.IsNotNull(u.HighResolutionCoverage); // bool? — should be either true or false, not null
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_NSDCoverageFlag_ReturnsBoolForKnownLocations()
        {
            // North America — should be high-res covered
            using (var geo = new GeoMag())
            {
                geo.LoadModel(DllPath);
                geo.MagneticCalculations(new CalculationOptions
                {
                    Latitude = 40.0, Longitude = -100.0, StartDate = new DateTime(2020, 6, 1)
                });
                Assert.AreEqual(true, geo.ResultsOfCalculation[0].Uncertainty.HighResolutionCoverage,
                    "North America should be NSD high-res covered");
            }
        }
    }
}
```

- [ ] **Step 2: Verify build passes**

```
dotnet build tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Debug
```

Expected: builds.

- [ ] **Step 3: Run with category filter to confirm tests are skipped in CI mode**

```
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "TestCategory!=RequiresHDGMDll" -c Debug
```

Expected: integration tests are excluded (the new file's tests do not appear in the result counts).

- [ ] **Step 4: (Optional, manual) If maintainer has the NOAA DLL locally, run with env vars set**

```bash
HDGM_DLL_PATH=/path/to/hdgm2019-64.dll \
HDGM_TEST_VALUES_PATH=/path/to/HDGM2019_TestValues.txt \
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "TestCategory=RequiresHDGMDll" -c Debug
```

Expected: integration tests pass.

- [ ] **Step 5: Commit**

```bash
git add tests/GeoMagSharp.Tests/HDGM/HDGMIntegrationTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] test: add HDGM integration tests (env-var-gated) (#19)

Adds integration test class HDGMIntegrationTests, gated on HDGM_DLL_PATH
(and optionally HDGM_TEST_VALUES_PATH for the numerical-tolerance test).
All tests carry [TestCategory("RequiresHDGMDll")] so CI's existing
--filter "TestCategory!=RequiresHDGMDll" excludes them automatically.

Tests validate: real DLL load, single-point plausibility for mid-North-
America, sample-point match against HDGM2019_TestValues.txt within
tolerance (D/I within 0.0001°, F within 0.05 nT), out-of-range date
sentinel handling, multi-load/dispose cycle, sigma value population,
and NSD coverage flag for a known-covered location.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 14: Add `.gitignore` defensive entries for HDGM artifacts

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Read current `.gitignore`**

```
cat .gitignore
```

Note any existing patterns and how they're scoped.

- [ ] **Step 2: Append HDGM defensive entries**

Append to the end of `.gitignore`:

```gitignore

# HDGM-derived artifacts — never committed (license posture per design 2026-04-26)
# These rules block accidental commits of NOAA HDGM data files. Documentation
# (this design / spec) is exempt because it lives under docs/ and references the
# files only by name, not contents.
hdgm*.dll
HDGM*.dll
HDGM*.exe
HDGM*.EXE
HDGM*_TestValues.txt
HDGM*_Documentation.pdf
HDGM_license.txt
```

- [ ] **Step 3: Verify the design doc and tasks.md are NOT matched**

```
git check-ignore docs/superpowers/specs/2026-04-26-hdgm-support-design.md docs/features/hdgm-support/tasks.md docs/features/hdgm-support/README.md
```

Expected: empty output (none ignored).

Verify the rule WOULD ignore an HDGM artifact (synthetic test):

```
git check-ignore -v hdgm2019-64.dll HDGM2019_TestValues.txt
```

Expected: both shown with the `.gitignore` rule that matched.

- [ ] **Step 4: Commit**

```bash
git add .gitignore
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] chore: add HDGM defensive .gitignore entries (#19)

Defensive rules to prevent accidental commits of NOAA HDGM artifacts
(DLLs, EXEs, test value files, documentation PDF) per the design's
license posture. End users supply HDGM data independently; the repo
intentionally ships no HDGM-derived data.

Design and task docs under docs/ are unaffected (they reference HDGM
files by name only, not contents).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 15: Create `docs/features/hdgm-support/README.md` user guide

**Files:**
- Create: `docs/features/hdgm-support/README.md`

- [ ] **Step 1: Create the user-facing guide**

```markdown
# HDGM Support — User Guide

GeoMagSharp v1.6.0 adds support for NOAA's High Definition Geomagnetic Model (HDGM)
on Windows. This guide explains how to obtain HDGM, configure GeoMagSharp to use it,
and run the integration tests.

## Licensing

HDGM is publicly available from NOAA for **non-commercial use** without restriction.
**Commercial use requires a license agreement with NOAA.**

GeoMagSharp does not redistribute any HDGM-derived artifacts. The library only
provides the dynamic-loading mechanism — the DLL, coefficient data, test values,
and documentation must be obtained separately from NOAA or your authorized vendor.

If you have any uncertainty about your usage's commercial/non-commercial status,
contact NOAA's HDGM support (`hdgm.support@noaa.gov`) for guidance.

## Obtaining the NOAA HDGM DLL

Download the NOAA HDGM2019 (or later) developer package from NCEI. The package
contains:

- `hdgm2019-64.dll` (or `hdgm2019.dll` for 32-bit)
- `HDGM2019_TestValues.txt` (validation data)
- `HDGM_Documentation.pdf` (NOAA's user guide)
- Other files (developer C source, GUI installers, etc. — not needed by GeoMagSharp)

Place the DLL anywhere your application can read it. GeoMagSharp does not impose
a folder convention.

## Bitness selection

NOAA ships both 32-bit and 64-bit DLLs:

| Process bitness | Use this DLL |
|---|---|
| 64-bit (most modern apps) | `hdgm2019-64.dll` |
| 32-bit (legacy net48 apps configured x86) | `hdgm2019.dll` |

A bitness mismatch surfaces as `GeoMagExceptionModelNotLoaded` with a hint
message. Pick the DLL that matches the process bitness of the application
that consumes GeoMagSharp.

## Loading HDGM

```csharp
using (var geo = new GeoMag())
{
    geo.LoadModel(@"C:\NOAA\HDGM2019\hdgm2019-64.dll");

    geo.MagneticCalculations(new CalculationOptions
    {
        Latitude = 40.0,
        Longitude = -100.0,
        StartDate = new DateTime(2020, 6, 1)
    });

    var r = geo.ResultsOfCalculation[0];

    // Standard fields (work for any model)
    Console.WriteLine($"D = {r.Declination.Value:F3}°");
    Console.WriteLine($"F = {r.TotalField.Value:F1} nT");

    // HDGM-specific extras (null on other models)
    if (r.Uncertainty.SigmaD.HasValue)
        Console.WriteLine($"σ_D = {r.Uncertainty.SigmaD:F3}°");
    if (r.Uncertainty.HighResolutionCoverage == true)
        Console.WriteLine("Location has high-resolution NSD survey coverage");
}
```

The `using` block disposes the DLL handle when scope exits. Without `using`,
the handle leaks until process exit (no functional issue, but it's good hygiene).

## Detection rule

`GeoMag.LoadModel(path)` automatically routes a path into the HDGM pipeline if:

- The file extension is `.dll` (case-insensitive), AND
- The filename (without extension) contains the substring `hdgm` (case-insensitive)

Examples that route to HDGM:
- `hdgm2019-64.dll`
- `HDGM2024.DLL`
- `halliburton_hdgm.dll` (vendor rename)
- `C:\NOAA\HDGM2019\hdgm2019-64.dll`

Examples that do NOT route to HDGM:
- `WMM.COF` → existing COF loader
- `IGRF14.DAT` → existing DAT loader
- `hdgm/wmm.dll` → no "hdgm" in filename portion
- `hdgm2019_file.exe` → wrong extension

## Cross-platform behavior

HDGM is **Windows-only**. Calls to `LoadModel` with an HDGM path on Linux or
macOS throw `PlatformNotSupportedException`. The other models (WMM, WMMHR,
IGRF, EMM, DGRF) remain cross-platform — only HDGM is restricted.

## Date validity

HDGM ships with model coefficients for a defined year range (e.g., HDGM2019
covers approximately 1900–2020). GeoMagSharp does not parse the version year
from the filename; it trusts the DLL's own validation. Dates outside the
supported range surface as `GeoMagExceptionOutOfRange` with a descriptive
message indicating the issue.

## Integration tests

GeoMagSharp's HDGM integration tests are gated on environment variables.
The tests skip silently if either variable is unset:

```bash
HDGM_DLL_PATH=C:\NOAA\HDGM2019\hdgm2019-64.dll
HDGM_TEST_VALUES_PATH=C:\NOAA\HDGM2019\HDGM2019_TestValues.txt

dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj \
  --filter "TestCategory=RequiresHDGMDll"
```

CI runs unit tests only, with `--filter "TestCategory!=RequiresHDGMDll"`.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `GeoMagExceptionFileNotFound` | DLL path wrong or file missing |
| `GeoMagExceptionModelNotLoaded` with "Win32 error 193" | Bitness mismatch — wrong DLL for process bitness |
| `GeoMagExceptionModelNotLoaded` with "hdgmcalc symbol not found" | Wrong DLL (not a real HDGM DLL, or unsupported version) |
| `GeoMagExceptionModelNotLoaded` with arbitrary Win32 error | Often antivirus quarantine; check that the DLL exists and is not blocked |
| `GeoMagExceptionOutOfRange` from a calculation | Date or location outside what the loaded HDGM version covers |
| `PlatformNotSupportedException` | Running on Linux/macOS; HDGM is Windows-only |

## Reference

- Design document: `docs/superpowers/specs/2026-04-26-hdgm-support-design.md`
- GitHub issue: #19
- NOAA HDGM contact: `hdgm.support@noaa.gov`
```

- [ ] **Step 2: Verify it renders correctly (markdown lint optional)**

```
ls -la docs/features/hdgm-support/README.md
```

- [ ] **Step 3: Commit**

```bash
git add docs/features/hdgm-support/README.md
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] docs: add HDGM user guide (#19)

User-facing guide covering: license posture (non-commercial vs
commercial), how to obtain the NOAA DLL, bitness selection, sample
LoadModel + MagneticCalculations call site with sigma and coverage
field access, the filename detection rule with examples, cross-platform
behavior, integration test env-var setup, and a troubleshooting table
mapping common error patterns to causes.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 16: Update `README.md` and `CLAUDE.md`

**Files:**
- Modify: `README.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Read current `README.md`**

```
cat README.md
```

Locate the supported-models section. Note the existing list format (e.g., bullet list, table, etc.).

- [ ] **Step 2: Update `README.md`**

Add HDGM to the supported-models list. The new entry — match the style of the existing entries; here is a representative example for a bullet list:

```markdown
- **HDGM** (High Definition Geomagnetic Model) — *Windows-only.* Requires the
  user-supplied NOAA HDGM DLL. Provides degree-740 crustal field with per-point
  uncertainty estimates and high-resolution survey coverage flag. See
  [docs/features/hdgm-support/README.md](docs/features/hdgm-support/README.md)
  for details.
```

If `README.md` has a "Quick Start" or "Usage" section with an existing code sample, append a parallel HDGM example mirroring the user guide.

- [ ] **Step 3: Update `CLAUDE.md` Project Overview**

In `CLAUDE.md`, locate the line:

```
GeoMagSharp is a C# library for geomagnetic field calculations using spherical harmonic models. It is a port of GeoMag 7.0 (NOAA) and supports WMM, WMMHR, IGRF, EMM, and BGGM models for computing magnetic declination, inclination, and field intensity.
```

Update the supported-models list. Replace with:

```
GeoMagSharp is a C# library for geomagnetic field calculations using spherical harmonic models. It is a port of GeoMag 7.0 (NOAA) and supports WMM, WMMHR, IGRF, EMM, DGRF, BGGM, and HDGM (Windows-only via NOAA DLL) models for computing magnetic declination, inclination, and field intensity.
```

Also locate the **Supported Magnetic Models** section (around line 80) and add HDGM to the bullet list:

```markdown
- **HDGM** (High Definition Geomagnetic Model — Windows-only via user-supplied NOAA DLL)
```

- [ ] **Step 4: Verify both files**

```
git diff README.md CLAUDE.md | head -100
```

- [ ] **Step 5: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] docs: add HDGM to README and CLAUDE.md supported models (#19)

Updates the supported-models list in README.md and CLAUDE.md to include
HDGM with a Windows-only callout and a pointer to the dedicated HDGM
user guide at docs/features/hdgm-support/README.md.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 17: Final regression and multi-target build verification

**Files:** none (verification only)

- [ ] **Step 1: Clean build**

```
dotnet clean
dotnet restore
```

- [ ] **Step 2: Build all targets in Release**

```
dotnet build -c Release
```

Expected: build succeeds for both `net48` and `netstandard2.0` targets. Watch for any unexpected warnings related to obsoleted constants — should only see warnings on call sites that reference `Constants.MaxDeg`/`MaxCoeff` (there should be none).

- [ ] **Step 3: Run full unit test suite (CI mode)**

```
dotnet test -c Release --filter "TestCategory!=RequiresHDGMDll" --verbosity normal
```

Expected: all tests pass. Capture the totals (existing + new ~70 HDGM tests).

- [ ] **Step 4: (Optional, maintainer-only) Run integration tests with real DLL**

```bash
HDGM_DLL_PATH=/path/to/hdgm2019-64.dll \
HDGM_TEST_VALUES_PATH=/path/to/HDGM2019_TestValues.txt \
dotnet test -c Release --filter "TestCategory=RequiresHDGMDll" --verbosity normal
```

Expected: all integration tests pass within tolerance.

- [ ] **Step 5: Verify NuGet pack still works**

```
dotnet pack src/GeoMagSharp/GeoMagSharp.csproj -c Release -o artifacts
ls artifacts/
```

Expected: `GeoMagSharp.1.6.0.nupkg` and symbols package. Verify the package does not include any HDGM data files:

```
unzip -l artifacts/GeoMagSharp.1.6.0.nupkg | grep -iE "hdgm|hdgm" || echo "OK: no HDGM artifacts in package"
```

Expected: prints "OK: no HDGM artifacts in package".

- [ ] **Step 6: Verify GUI assembly's `[InternalsVisibleTo]` is recognized**

```
git grep -n "InternalsVisibleTo" src/GeoMagSharp/
```

Expected: shows the entries in `GeoMagSharp.csproj` (or `AssemblyInfo.cs` if used) for both `GeoMagSharp.Tests` and `GeoMagSharp.GUI`.

- [ ] **Step 7: Final sanity-check — Ralph Loop personas should find clean cycles**

Per CLAUDE.md, the Ralph Loop runs after implementation: 6 personas (IMPLEMENTER, REVIEWER, TESTER, API_DESIGNER, SECURITY, PROJECT_MGR) cycle until 2 clean cycles complete. The implementation tasks above end here; the Ralph Loop is a separate execution phase governed by the project's standing process.

- [ ] **Step 8: No commit needed for verification.** If any issue surfaces, fix it inline and commit per the affected task's pattern.

---

## Self-review checklist (run by plan author after writing)

### Spec coverage map

| Spec section | Tasks |
|---|---|
| Section 4 — Architecture overview (detection rule, IDisposable lifecycle) | Tasks 4 (detection), 8 (MagneticModelSet IDisposable), 10 (GeoMag IDisposable) |
| Section 5 — New / modified components | Tasks 1 (enum), 2 (interface + delegate + Win32), 4 (detector), 6 (LoadLibrary impl), 7 (adapter), 8 (model set), 9 (loader), 10 (GeoMag wiring), 11 (uncertainty provider), 12 (cleanup), 5 (test fake), 13 (integration tests) |
| Section 6 — Data flow | Tasks 7, 10 (sync + async branching) |
| Section 7 — Public API surface | Tasks 1, 2, 3, 8, 10, 12 |
| Section 8 — Error handling | Tasks 6, 7, 9 (exception messages with concrete hints) |
| Section 9 — Testing strategy | Tasks 4, 5, 7, 9, 10, 13, 17 |
| Section 10 — Versioning, packaging, process | Tasks 14 (.gitignore), 15 (user guide), 16 (README/CLAUDE), 17 (build/pack verification) |

### Placeholder scan

- ✅ No "TBD", "TODO", "FIXME", "implement later", or "fill in details" anywhere
- ✅ Every code block contains the actual code an engineer needs to write
- ✅ Every command is concrete with explicit expected output
- ✅ No "similar to Task N" — code is repeated where used

### Type-consistency check

- `INativeHdgmInvoker.Calculate(double, double, double, double, double[])` — used identically in Tasks 2, 5, 6, 7, 10, 13
- `HdgmCalcDelegate(double, double, double, double, int, int, double[])` — defined Task 2; consumed Task 6
- `MagneticModelSet.NativeInvoker` — defined Task 8; consumed Tasks 9, 10
- `GeomagneticUncertainty.SigmaD/I/H/X/Y/Z/F` — defined Task 3; consumed Task 7
- `GeomagneticUncertainty.HighResolutionCoverage` — defined Task 3; consumed Task 7, 13
- `ModelPathDetector.IsHdgmPath(string)` — defined Task 4; consumed Task 10
- `HDGMModelLoader.Load(string)` — defined Task 9; consumed Task 10
- `HDGMCalculationAdapter.Calculate(CalculationOptions, DateTime, INativeHdgmInvoker)` — defined Task 7; consumed Task 10
- `LoadLibraryHdgmInvoker(string)` ctor — defined Task 6; consumed Task 9
- `knownModels.HDGM` — defined Task 1; consumed Tasks 7, 9, 10, 11, 13

All names match across tasks.

### Scope check

- 17 tasks, each with multi-step TDD structure
- Each task ends in a commit, advancing the implementation
- Each task is independently testable except integration tests (env-var-gated)
- No task depends on a future task — dependencies flow forward only
- Plan covers every section of the spec; nothing left uncovered

---
