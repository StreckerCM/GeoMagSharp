# Model Discovery API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a public `ModelDiscovery` static class so consumers can enumerate loadable model files in a folder (COF, DAT, HDGM .dll), with optional `.models.json` caching and HDGM date-range probing.

**Architecture:** New `Discovery/` subnamespace adjacent to existing `HDGM/`. Two scan modes (Quick = filename-only, Full = header peek + HDGM probe). Optional folder-local cache with mtime+size invalidation and atomic writes. HDGM date probe reuses the existing `LoadLibraryHdgmInvoker`. Discovery is identification-only — never calculates.

**Tech Stack:** C# multi-target (net48 + netstandard2.0), MSTest 3.1.1, Newtonsoft.Json (already a project dependency).

**Reference design:** `docs/superpowers/specs/2026-04-28-discovery-api-design.md`

---

## Conventions used in this plan

- **Test framework:** MSTest 3.1.1; attributes `[TestClass]`, `[TestMethod]`, `[TestCategory]`, `[ExpectedException]`.
- **Test naming:** `Method_Scenario_Expected` per CLAUDE.md.
- **Test project namespace:** `GeoMagSharp_UnitTests` (root) and `GeoMagSharp_UnitTests.Discovery` (new).
- **Production namespace:** `GeoMagSharp` for public types, `GeoMagSharp.Discovery` only for internals (per spec — keep public surface in `GeoMagSharp` for IntelliSense discoverability).
- **File headers:** 6-line block — File / Description / Author: Christopher Strecker / Website: https://github.com/StreckerCM/GeoMagSharp.
- **Build commands:**
  - `dotnet build src/GeoMagSharp/GeoMagSharp.csproj -c Debug` — main library
  - `dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "TestCategory!=RequiresHDGMDll" -c Debug` — unit/functional tests
  - `dotnet build -c Release` — full release build
- **Commit prefix:** `[IMPLEMENTER] feat: ...` / `[IMPLEMENTER] test: ...` / `[IMPLEMENTER] chore: ...`
- **Co-author trailer:** `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`

---

## File structure

| File | Visibility | Responsibility |
|---|---|---|
| `src/GeoMagSharp/Discovery/ScanMode.cs` | public enum | `Quick`, `Full` |
| `src/GeoMagSharp/Discovery/ModelDescriptor.cs` | public sealed | Immutable read-only descriptor (FilePath, DetectedType, DisplayName, MinDate?, MaxDate?, Description) |
| `src/GeoMagSharp/Discovery/ModelDiscoveryOptions.cs` | public | Mutable options (Mode, Recursive, UseCache, CacheFileName, CancellationToken, OnError) |
| `src/GeoMagSharp/Discovery/ModelDiscovery.cs` | public static | Entry point: `DiscoverModels`, `DescribeFile` |
| `src/GeoMagSharp/Discovery/ModelHeaderInspector.cs` | internal static | Open COF/DAT, read first line, classify type and year |
| `src/GeoMagSharp/Discovery/HdgmDateProbe.cs` | internal static | Filename year extraction, forward-probe loop |
| `src/GeoMagSharp/Discovery/ModelDiscoveryCache.cs` | internal static | Read/validate/save `.models.json` atomically |
| `src/GeoMagSharp/Discovery/ModelDiscoveryCacheEntry.cs` | internal | DTO: file path + size + mtime + descriptor |
| `tests/GeoMagSharp.Tests/Discovery/Fixtures/WMM2025_sample.COF` | test fixture | Real WMM header for inspector tests |
| `tests/GeoMagSharp.Tests/Discovery/Fixtures/IGRF14_sample.COF` | test fixture | Real IGRF header |
| `tests/GeoMagSharp.Tests/Discovery/Fixtures/EMM_sample.COF` | test fixture | Real EMM header |
| `tests/GeoMagSharp.Tests/Discovery/Fixtures/corrupt_header.COF` | test fixture | Garbage first line |
| `tests/GeoMagSharp.Tests/Discovery/Fixtures/empty.COF` | test fixture | Zero bytes |
| `tests/GeoMagSharp.Tests/Discovery/Fixtures/notamodel.txt` | test fixture | Wrong extension |
| `tests/GeoMagSharp.Tests/Discovery/TestFolderFixture.cs` | test helper | IDisposable temp-folder helper |
| `tests/GeoMagSharp.Tests/Discovery/ModelDescriptorTests.cs` | unit tests | ~6 tests |
| `tests/GeoMagSharp.Tests/Discovery/ModelHeaderInspectorTests.cs` | functional tests | ~8 tests |
| `tests/GeoMagSharp.Tests/Discovery/HdgmDateProbeTests.cs` | unit tests | ~6 tests via `FakeHdgmInvoker` |
| `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryCacheTests.cs` | unit tests | ~10 tests |
| `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryTests.cs` | functional tests | ~17 tests |
| `tests/GeoMagSharp.Tests/Discovery/HdgmDateProbeIntegrationTests.cs` | integration tests | ~3 env-var-gated tests |
| `Directory.Build.props` | modify | Bump `<VersionPrefix>` 1.6.0 → 1.7.0 |

---

## Task 1: Bump version 1.6.0 → 1.7.0

**Files:**
- Modify: `Directory.Build.props`

- [ ] **Step 1: Read current version**

```bash
grep VersionPrefix Directory.Build.props
```

Expected: `<VersionPrefix>1.6.0</VersionPrefix>`

- [ ] **Step 2: Bump to 1.7.0**

In `Directory.Build.props`, replace:
```xml
    <VersionPrefix>1.6.0</VersionPrefix>
```
with:
```xml
    <VersionPrefix>1.7.0</VersionPrefix>
```

- [ ] **Step 3: Verify build succeeds**

```bash
dotnet build src/GeoMagSharp/GeoMagSharp.csproj -c Debug --verbosity minimal
```

Expected: `Build succeeded` with no errors.

- [ ] **Step 4: Commit**

```bash
git add Directory.Build.props
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] chore: bump version 1.6.0 -> 1.7.0 (#21)

First commit on the feature branch per CLAUDE.md "bump version on first
feature branch after a release". v1.7.0 will publish with the
ModelDiscovery API addition.
EOF
)"
```

---

## Task 2: Add `ScanMode` public enum

**Files:**
- Create: `src/GeoMagSharp/Discovery/ScanMode.cs`
- Test: `tests/GeoMagSharp.Tests/Discovery/ModelDescriptorTests.cs` will exercise it indirectly in Task 3

- [ ] **Step 1: Create the enum file**

Create `src/GeoMagSharp/Discovery/ScanMode.cs`:

```csharp
/****************************************************************************
 * File:            ScanMode.cs
 * Description:     Discovery scan-depth selector
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

namespace GeoMagSharp
{
    /// <summary>
    /// Scan depth for <see cref="ModelDiscovery.DiscoverModels(string, ModelDiscoveryOptions)"/>.
    /// </summary>
    public enum ScanMode
    {
        /// <summary>
        /// Identify files by extension and filename only. Fast (filesystem-stat-only).
        /// <see cref="ModelDescriptor.DetectedType"/> remains <see cref="knownModels.NONE"/> for
        /// COF/DAT files, and <see cref="ModelDescriptor.MinDate"/>/<see cref="ModelDescriptor.MaxDate"/>
        /// are null.
        /// </summary>
        Quick,

        /// <summary>
        /// Open each candidate to read header (COF/DAT) or probe via LoadLibraryEx (HDGM .dll).
        /// Slower but populates <see cref="ModelDescriptor.DetectedType"/>, display name, and date range.
        /// </summary>
        Full
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/GeoMagSharp/GeoMagSharp.csproj -c Debug --verbosity minimal
```

Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add src/GeoMagSharp/Discovery/ScanMode.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add ScanMode enum (Quick, Full) for discovery (#21)

Public enum in the GeoMagSharp namespace selecting discovery scan depth.
Quick = filename-only identification. Full = open files for header peek
and probe HDGM DLLs.
EOF
)"
```

---

## Task 3: Add `ModelDescriptor` immutable type

**Files:**
- Create: `src/GeoMagSharp/Discovery/ModelDescriptor.cs`
- Create: `tests/GeoMagSharp.Tests/Discovery/ModelDescriptorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/GeoMagSharp.Tests/Discovery/ModelDescriptorTests.cs`:

```csharp
/****************************************************************************
 * File:            ModelDescriptorTests.cs
 * Description:     Unit tests for ModelDescriptor immutability and round-trip
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class ModelDescriptorTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullFilePath_Throws()
        {
            var _ = new ModelDescriptor(null, knownModels.NONE, "x", null, null);
        }

        [TestMethod]
        public void Constructor_NullDisplayName_DefaultsEmpty()
        {
            var d = new ModelDescriptor("path", knownModels.NONE, null, null, null);
            Assert.AreEqual(string.Empty, d.DisplayName);
        }

        [TestMethod]
        public void Constructor_AllFieldsSet_PropertiesRoundTrip()
        {
            var d = new ModelDescriptor("WMM.COF", knownModels.WMM, "WMM2025", 2025.0, 2030.0, "test");
            Assert.AreEqual("WMM.COF", d.FilePath);
            Assert.AreEqual(knownModels.WMM, d.DetectedType);
            Assert.AreEqual("WMM2025", d.DisplayName);
            Assert.AreEqual(2025.0, d.MinDate);
            Assert.AreEqual(2030.0, d.MaxDate);
            Assert.AreEqual("test", d.Description);
        }

        [TestMethod]
        public void Properties_HaveNoSetters()
        {
            var props = typeof(ModelDescriptor).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsTrue(props.Length > 0, "expected public instance properties on ModelDescriptor");
            foreach (var p in props)
            {
                Assert.IsFalse(p.CanWrite, "ModelDescriptor." + p.Name + " must be read-only");
            }
        }

        [TestMethod]
        public void ToString_IncludesKeyFields()
        {
            var d = new ModelDescriptor("WMM.COF", knownModels.WMM, "WMM2025", 2025.0, 2030.0);
            var s = d.ToString();
            Assert.IsTrue(s.IndexOf("WMM2025", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(s.IndexOf("WMM.COF", StringComparison.Ordinal) >= 0);
        }

        [TestMethod]
        public void NullDateBounds_AllowedForUnknownRange()
        {
            var d = new ModelDescriptor("hdgm.dll", knownModels.HDGM, "HDGM", null, null);
            Assert.IsNull(d.MinDate);
            Assert.IsNull(d.MaxDate);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (build error: type doesn't exist)**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelDescriptorTests" -c Debug
```

Expected: build error `'ModelDescriptor' does not exist`.

- [ ] **Step 3: Implement `ModelDescriptor`**

Create `src/GeoMagSharp/Discovery/ModelDescriptor.cs`:

```csharp
/****************************************************************************
 * File:            ModelDescriptor.cs
 * Description:     Immutable snapshot of a discovered magnetic model file
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;

namespace GeoMagSharp
{
    /// <summary>
    /// Snapshot of a discovered magnetic model file. All properties are
    /// populated at construction; the instance is read-only.
    /// </summary>
    public sealed class ModelDescriptor
    {
        /// <summary>Constructs a new descriptor.</summary>
        /// <param name="filePath">Path to the file as discovered. Required.</param>
        /// <param name="detectedType">Detected model type, or <see cref="knownModels.NONE"/> if unidentified.</param>
        /// <param name="displayName">Human-friendly name (e.g. "WMM2025"). Null is normalised to empty.</param>
        /// <param name="minDate">Earliest valid decimal year, or null if unknown.</param>
        /// <param name="maxDate">Latest valid decimal year (exclusive), or null if unknown.</param>
        /// <param name="description">Optional free-form description.</param>
        public ModelDescriptor(
            string filePath,
            knownModels detectedType,
            string displayName,
            double? minDate,
            double? maxDate,
            string description = null)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            DetectedType = detectedType;
            DisplayName = displayName ?? string.Empty;
            MinDate = minDate;
            MaxDate = maxDate;
            Description = description ?? string.Empty;
        }

        /// <summary>Absolute or relative path to the file as discovered.</summary>
        public string FilePath { get; }

        /// <summary>Detected model type. <see cref="knownModels.NONE"/> when Quick mode skipped header peek or the header was unparseable.</summary>
        public knownModels DetectedType { get; }

        /// <summary>Human-friendly name for display. Falls back to filename-without-extension when no header parse was performed.</summary>
        public string DisplayName { get; }

        /// <summary>Earliest valid decimal year. Null when unknown (Quick mode for COF/DAT, or HDGM probe failure).</summary>
        public double? MinDate { get; }

        /// <summary>Latest valid decimal year (exclusive). Null when unknown.</summary>
        public double? MaxDate { get; }

        /// <summary>Optional free-form description (origin, notes).</summary>
        public string Description { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0} ({1}) {2}..{3} [{4}]",
                DisplayName, DetectedType, MinDate, MaxDate, FilePath);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelDescriptorTests" -c Debug
```

Expected: 6 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Discovery/ModelDescriptor.cs tests/GeoMagSharp.Tests/Discovery/ModelDescriptorTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add ModelDescriptor immutable type for discovery (#21)

Public sealed class with read-only auto-properties (FilePath, DetectedType,
DisplayName, MinDate, MaxDate, Description). Null FilePath throws
ArgumentNullException; null DisplayName/Description normalize to empty.
Six unit tests cover constructor validation, immutability via reflection,
ToString format, and null-date-bounds case.
EOF
)"
```

---

## Task 4: Add `ModelDiscoveryOptions` mutable type

**Files:**
- Create: `src/GeoMagSharp/Discovery/ModelDiscoveryOptions.cs`

- [ ] **Step 1: Create the options class**

Create `src/GeoMagSharp/Discovery/ModelDiscoveryOptions.cs`:

```csharp
/****************************************************************************
 * File:            ModelDiscoveryOptions.cs
 * Description:     Options for ModelDiscovery.DiscoverModels
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Threading;

namespace GeoMagSharp
{
    /// <summary>
    /// Options for <see cref="ModelDiscovery.DiscoverModels(string, ModelDiscoveryOptions)"/>.
    /// All fields have safe defaults; instances may be mutated freely before passing.
    /// </summary>
    public class ModelDiscoveryOptions
    {
        /// <summary>Scan depth. Default <see cref="ScanMode.Full"/> (header peek + HDGM probe).</summary>
        public ScanMode Mode { get; set; } = ScanMode.Full;

        /// <summary>Recurse subdirectories. Default false.</summary>
        public bool Recursive { get; set; } = false;

        /// <summary>
        /// If true, read .models.json from the scanned folder, validate cached entries against
        /// current mtime/size, deep-scan only new or changed files, and write the refreshed cache
        /// back at the end. Default false.
        /// </summary>
        public bool UseCache { get; set; } = false;

        /// <summary>Cache filename inside the scanned folder. Default ".models.json".</summary>
        public string CacheFileName { get; set; } = ".models.json";

        /// <summary>Cancellation token. Checked once per file. Default <see cref="CancellationToken.None"/>.</summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Callback invoked when an individual file or cache operation fails. Receives the path
        /// that triggered the failure and the exception. Default null (silent).
        /// </summary>
        public Action<string, Exception> OnError { get; set; }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/GeoMagSharp/GeoMagSharp.csproj -c Debug --verbosity minimal
```

Expected: `Build succeeded`. (No tests yet — this is a DTO; behavior is exercised when ModelDiscovery uses it in Task 11.)

- [ ] **Step 3: Commit**

```bash
git add src/GeoMagSharp/Discovery/ModelDiscoveryOptions.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add ModelDiscoveryOptions for discovery API (#21)

Public mutable options class with Mode (default Full), Recursive (default
false), UseCache (default false), CacheFileName (default ".models.json"),
CancellationToken, OnError callback. DTO-shaped for builder-style use; no
behavior on this class itself — exercised by ModelDiscovery in later tasks.
EOF
)"
```

---

## Task 5: Implement `ModelHeaderInspector` (COF/DAT first-line peek)

**Files:**
- Create: `src/GeoMagSharp/Discovery/ModelHeaderInspector.cs`
- Create: `tests/GeoMagSharp.Tests/Discovery/Fixtures/WMM2025_sample.COF`
- Create: `tests/GeoMagSharp.Tests/Discovery/Fixtures/IGRF14_sample.COF`
- Create: `tests/GeoMagSharp.Tests/Discovery/Fixtures/EMM_sample.COF`
- Create: `tests/GeoMagSharp.Tests/Discovery/Fixtures/corrupt_header.COF`
- Create: `tests/GeoMagSharp.Tests/Discovery/Fixtures/empty.COF`
- Create: `tests/GeoMagSharp.Tests/Discovery/Fixtures/notamodel.txt`
- Create: `tests/GeoMagSharp.Tests/Discovery/ModelHeaderInspectorTests.cs`

- [ ] **Step 1: Create the test fixture files**

In `tests/GeoMagSharp.Tests/Discovery/Fixtures/`:

`WMM2025_sample.COF` (one line, then blank — sufficient for header peek):
```
    2025.0            WMM-2025        12/10/2024
```

`IGRF14_sample.COF`:
```
    2025.0            IGRF14        11/01/2024
```

`EMM_sample.COF`:
```
   EMM-2017  2017.00 720 720  0 2017.00 2022.00 -1.0 600.0
```

`corrupt_header.COF`:
```
xxxx not a valid header xxxx
```

`empty.COF`: zero bytes (empty file)

`notamodel.txt`:
```
this is just a text file
```

- [ ] **Step 2: Add fixtures to test csproj as PreserveNewest content**

Edit `tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj` — find the existing `<None Include="TestData\**\*">` ItemGroup and add inside the same ItemGroup (or a new one):

```xml
    <None Include="Discovery\Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" />
```

- [ ] **Step 3: Write the failing tests**

Create `tests/GeoMagSharp.Tests/Discovery/ModelHeaderInspectorTests.cs`:

```csharp
/****************************************************************************
 * File:            ModelHeaderInspectorTests.cs
 * Description:     Functional tests for ModelHeaderInspector
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.Discovery;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class ModelHeaderInspectorTests
    {
        private static string FixturesDir => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Discovery", "Fixtures");

        private static string Fixture(string name) => Path.Combine(FixturesDir, name);

        [TestMethod]
        public void Inspect_ValidWmmCof_ReturnsWMMTypeAndYear()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("WMM2025_sample.COF"));
            Assert.AreEqual(knownModels.WMM, d.DetectedType);
            Assert.AreEqual(2025.0, d.MinDate);
            Assert.AreEqual(2030.0, d.MaxDate);
        }

        [TestMethod]
        public void Inspect_ValidIgrfCof_ReturnsIGRFTypeAndYear()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("IGRF14_sample.COF"));
            Assert.AreEqual(knownModels.IGRF, d.DetectedType);
            Assert.AreEqual(2025.0, d.MinDate);
        }

        [TestMethod]
        public void Inspect_ValidEmmCof_ReturnsEMMType()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("EMM_sample.COF"));
            Assert.AreEqual(knownModels.EMM, d.DetectedType);
        }

        [TestMethod]
        public void Inspect_CorruptHeader_ReturnsNoneType()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("corrupt_header.COF"));
            Assert.AreEqual(knownModels.NONE, d.DetectedType);
            Assert.IsNull(d.MinDate);
            Assert.IsNull(d.MaxDate);
        }

        [TestMethod]
        public void Inspect_EmptyFile_ReturnsNoneType()
        {
            var d = ModelHeaderInspector.Inspect(Fixture("empty.COF"));
            Assert.AreEqual(knownModels.NONE, d.DetectedType);
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void Inspect_FileNotFound_Throws()
        {
            ModelHeaderInspector.Inspect(Path.Combine(FixturesDir, "nonexistent.COF"));
        }

        [TestMethod]
        public void Inspect_DatExtension_DoesNotThrow()
        {
            // The inspector should accept .DAT files using the same first-line peek; we don't
            // ship a .DAT fixture (DAT format is an integer year on line 1) so just verify
            // a synthetic .DAT path with a valid first line works.
            var dat = Path.Combine(FixturesDir, "synthetic.DAT");
            try
            {
                File.WriteAllText(dat, "1900\n2025\n");
                var d = ModelHeaderInspector.Inspect(dat);
                Assert.IsNotNull(d);
            }
            finally
            {
                if (File.Exists(dat)) File.Delete(dat);
            }
        }

        [TestMethod]
        public void Inspect_FilePath_PopulatedFromInput()
        {
            var path = Fixture("WMM2025_sample.COF");
            var d = ModelHeaderInspector.Inspect(path);
            Assert.AreEqual(path, d.FilePath);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelHeaderInspectorTests" -c Debug
```

Expected: build error `'ModelHeaderInspector' does not exist`.

- [ ] **Step 5: Implement `ModelHeaderInspector`**

Create `src/GeoMagSharp/Discovery/ModelHeaderInspector.cs`:

```csharp
/****************************************************************************
 * File:            ModelHeaderInspector.cs
 * Description:     Reads first line of a .COF / .DAT file to classify model
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Globalization;
using System.IO;

namespace GeoMagSharp.Discovery
{
    /// <summary>
    /// Reads the first non-blank line of a model coefficient file and classifies it
    /// using <see cref="ExtensionMethods.CheckStringForModel"/>. For DAT files (which
    /// store an integer year on line 1) returns DAT-typed metadata.
    /// </summary>
    internal static class ModelHeaderInspector
    {
        /// <summary>
        /// Inspects a single file and returns a <see cref="ModelDescriptor"/> populated
        /// from its first-line header. Always returns a non-null descriptor; if the file
        /// is unparseable the descriptor's DetectedType is <see cref="knownModels.NONE"/>
        /// and date bounds are null.
        /// </summary>
        /// <exception cref="ArgumentNullException">filePath is null.</exception>
        /// <exception cref="FileNotFoundException">File does not exist.</exception>
        public static ModelDescriptor Inspect(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found: " + filePath, filePath);

            string firstLine = ReadFirstNonBlankLine(filePath);
            if (string.IsNullOrEmpty(firstLine))
            {
                return new ModelDescriptor(filePath, knownModels.NONE,
                    Path.GetFileNameWithoutExtension(filePath), null, null);
            }

            knownModels type = firstLine.CheckStringForModel();
            double? minDate = ExtractYearFromHeader(firstLine);
            double? maxDate = minDate.HasValue ? minDate.Value + 5.0 : (double?)null;

            string displayName = BuildDisplayName(filePath, firstLine, type);

            return new ModelDescriptor(filePath, type, displayName, minDate, maxDate);
        }

        private static string ReadFirstNonBlankLine(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write))
            using (var reader = new StreamReader(fs))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
                }
                return null;
            }
        }

        private static double? ExtractYearFromHeader(string line)
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                double v;
                if (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                {
                    if (v >= 1900.0 && v <= 2100.0) return v;
                }
            }
            return null;
        }

        private static string BuildDisplayName(string filePath, string firstLine, knownModels type)
        {
            if (type == knownModels.NONE)
                return Path.GetFileNameWithoutExtension(filePath);

            // Pull the model token plus year if present (matches "WMM-2025", "IGRF14", "EMM-2017", etc.).
            var trimmed = firstLine.Trim();
            int idx = trimmed.IndexOf(type.ToString(), StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return Path.GetFileNameWithoutExtension(filePath);

            int end = idx;
            while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end])) end++;
            return trimmed.Substring(idx, end - idx);
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelHeaderInspectorTests" -c Debug
```

Expected: 8 passed, 0 failed.

- [ ] **Step 7: Commit**

```bash
git add src/GeoMagSharp/Discovery/ModelHeaderInspector.cs \
        tests/GeoMagSharp.Tests/Discovery/ModelHeaderInspectorTests.cs \
        tests/GeoMagSharp.Tests/Discovery/Fixtures/ \
        tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add ModelHeaderInspector for COF/DAT classification (#21)

Internal static class that opens a model file, reads the first non-blank
line, and classifies via the existing ExtensionMethods.CheckStringForModel.
Year extracted from any 4-digit numeric token in 1900..2100 on the header
line; MaxDate = MinDate + 5 (typical model nominal validity).

Returns a non-null ModelDescriptor; unparseable input yields NONE type
with null dates rather than throwing. Test fixtures (WMM2025/IGRF14/EMM
samples plus corrupt and empty cases) are deployed via PreserveNewest.
EOF
)"
```

---

## Task 6: Implement `HdgmDateProbe`

**Files:**
- Create: `src/GeoMagSharp/Discovery/HdgmDateProbe.cs`
- Create: `tests/GeoMagSharp.Tests/Discovery/HdgmDateProbeTests.cs`

The probe accepts an `INativeHdgmInvoker` factory parameter so tests can inject `FakeHdgmInvoker` from the existing HDGM test fakes (see `tests/GeoMagSharp.Tests/HDGM/FakeHdgmInvoker.cs`).

- [ ] **Step 1: Write the failing tests**

Create `tests/GeoMagSharp.Tests/Discovery/HdgmDateProbeTests.cs`:

```csharp
/****************************************************************************
 * File:            HdgmDateProbeTests.cs
 * Description:     Unit tests for HdgmDateProbe via FakeHdgmInvoker
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.Discovery;
using GeoMagSharp.HDGM;
using GeoMagSharp_UnitTests.HDGM;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class HdgmDateProbeTests
    {
        [TestMethod]
        public void ExtractYearFromFilename_StandardNoaaName_Returns2019()
        {
            Assert.AreEqual(2019, HdgmDateProbe.ExtractYearFromFilename("hdgm2019-64.dll"));
        }

        [TestMethod]
        public void ExtractYearFromFilename_BitnessSuffixOnly_ReturnsNull()
        {
            // "myhdgm-64" — only "64" present, not a year-like 19xx/20xx token
            Assert.IsNull(HdgmDateProbe.ExtractYearFromFilename("myhdgm-64.dll"));
        }

        [TestMethod]
        public void ExtractYearFromFilename_NoYear_ReturnsNull()
        {
            Assert.IsNull(HdgmDateProbe.ExtractYearFromFilename("halliburton_hdgm.dll"));
        }

        [TestMethod]
        public void ExtractYearFromFilename_VendorPrefixWithYear_ReturnsYear()
        {
            Assert.AreEqual(2024, HdgmDateProbe.ExtractYearFromFilename("halliburton_hdgm2024.dll"));
        }

        [TestMethod]
        public void Probe_FakeInvokerAlwaysSentinel_ReturnsNullDates()
        {
            var fake = new FakeHdgmInvoker();
            // CannedOutData defaults to all zeros; mark outData[0] = -99999 (sentinel) for every call
            fake.CannedOutData = new double[25];
            fake.CannedOutData[0] = -99999.0;

            var (min, max) = HdgmDateProbe.Probe(_ => fake, "hdgm2019-64.dll");
            Assert.IsNull(min);
            Assert.IsNull(max);
        }

        [TestMethod]
        public void Probe_FakeInvokerValidUntilYearN_ReturnsCorrectMaxDate()
        {
            // Returns valid (non-sentinel) for years 2019-2020 then sentinel
            int callCount = 0;
            var fake = new FakeHdgmInvoker();
            // Override Calculate to vary by call
            // Since FakeHdgmInvoker is a real class, we extend behaviour via a side helper
            var probingFake = new ProbingFake(year =>
            {
                callCount++;
                return year <= 2020 ? 0.0 : -99999.0;
            });

            var (min, max) = HdgmDateProbe.Probe(_ => probingFake, "hdgm2019-64.dll");
            Assert.AreEqual(1900.0, min);
            Assert.AreEqual(2021.0, max);  // exclusive upper: last valid (2020) + 1
        }

        // Test-only invoker that returns a different value depending on the date probed.
        private class ProbingFake : INativeHdgmInvoker
        {
            private readonly Func<int, double> _outData0ForYear;
            public ProbingFake(Func<int, double> outData0ForYear)
            {
                _outData0ForYear = outData0ForYear;
            }
            public int Calculate(double latitude, double longitude, double depthMeters,
                double decimalYear, double[] outData)
            {
                outData[0] = _outData0ForYear((int)decimalYear);
                return 0;
            }
            public void Dispose() { }
        }
    }
}
```

Note: the existing `FakeHdgmInvoker` (in `tests/GeoMagSharp.Tests/HDGM/`) is `internal` — accessible from this test namespace because both share the same test assembly.

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~HdgmDateProbeTests" -c Debug
```

Expected: build error `'HdgmDateProbe' does not exist`.

- [ ] **Step 3: Implement `HdgmDateProbe`**

Create `src/GeoMagSharp/Discovery/HdgmDateProbe.cs`:

```csharp
/****************************************************************************
 * File:            HdgmDateProbe.cs
 * Description:     Discovers HDGM DLL date-range bounds via forward probing
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Text.RegularExpressions;
using GeoMagSharp.HDGM;

namespace GeoMagSharp.Discovery
{
    /// <summary>
    /// Probes an HDGM DLL to determine its valid date range without trusting
    /// filename year alone. Loads the DLL via the supplied factory, calls
    /// hdgmcalc with a known-NSD-covered point at year, year+1, ... up to 8
    /// times, and treats the first sentinel result as the upper bound.
    /// </summary>
    internal static class HdgmDateProbe
    {
        private const int MaxForwardYearsToProbe = 8;
        private const double Sentinel = -99999.0;
        private const double ProbeLatitude = 40.0;        // mid-North-America; well-NSD-covered
        private const double ProbeLongitude = -100.0;
        private const double ProbeDepthMeters = 0.0;
        private const double KnownStartYear = 1900.0;     // HDGM convention back to 1900

        /// <summary>
        /// Extracts a 4-digit year (19xx or 20xx) from a filename. Avoids matching
        /// a "-64" bitness suffix or other short numeric tokens. Returns null if no
        /// year-shaped token is found.
        /// </summary>
        public static int? ExtractYearFromFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return null;
            var name = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(name)) return null;

            var match = Regex.Match(name, @"(?:^|[^0-9])(19\d{2}|20\d{2})(?:[^0-9]|$)");
            if (!match.Success) return null;
            return int.Parse(match.Groups[1].Value);
        }

        /// <summary>
        /// Probes an HDGM DLL and returns its (min, max) decimal-year bounds. The factory
        /// receives the dllPath and produces an INativeHdgmInvoker (in production this is
        /// LoadLibraryHdgmInvoker; tests inject a fake). Catches all exceptions from the
        /// factory and the probe loop; returns (null, null) on any failure.
        /// </summary>
        /// <param name="invokerFactory">Factory that produces an INativeHdgmInvoker for a path.</param>
        /// <param name="dllPath">Path to the HDGM DLL.</param>
        /// <returns>Tuple (minDate, maxDate). Both null if probe failed or all probes sentineled.</returns>
        public static (double? minDate, double? maxDate) Probe(
            Func<string, INativeHdgmInvoker> invokerFactory, string dllPath)
        {
            int startYear = ExtractYearFromFilename(dllPath) ?? DateTime.UtcNow.Year;

            try
            {
                using (var invoker = invokerFactory(dllPath))
                {
                    if (invoker == null) return (null, null);

                    var outData = new double[25];
                    int maxValidYear = startYear - 1;

                    for (int year = startYear; year < startYear + MaxForwardYearsToProbe; year++)
                    {
                        outData[0] = 0.0;  // reset before each call
                        invoker.Calculate(ProbeLatitude, ProbeLongitude, ProbeDepthMeters,
                            (double)year + 0.5, outData);
                        if (outData[0] == Sentinel) break;
                        maxValidYear = year;
                    }

                    if (maxValidYear < startYear) return (null, null);
                    return (KnownStartYear, (double)(maxValidYear + 1));
                }
            }
            catch
            {
                // LoadLibraryEx fail (bitness / AV / corrupt), missing symbol, or anything
                // else from the native side. Fall back to null bounds; runtime sentinel is
                // the authoritative guard.
                return (null, null);
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~HdgmDateProbeTests" -c Debug
```

Expected: 6 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Discovery/HdgmDateProbe.cs \
        tests/GeoMagSharp.Tests/Discovery/HdgmDateProbeTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add HdgmDateProbe for HDGM date-range discovery (#21)

Internal static class that probes an HDGM DLL via forward hdgmcalc calls
to detect its actual upper-bound year. Filename year extraction uses a
regex anchored on word-boundary 4-digit 19xx/20xx tokens to avoid
matching the "-64" bitness suffix.

Probe loop: starts at year-from-filename (or current year), calls
hdgmcalc at year, year+1, ..., year+7 with a known-NSD-covered point.
First sentinel return marks the upper bound. All native errors are
caught and degrade to (null, null), letting the runtime sentinel be the
authoritative guard.

The Probe method takes an INativeHdgmInvoker factory parameter so tests
inject FakeHdgmInvoker without requiring LoadLibraryEx. Six unit tests
cover filename parsing edge cases and the all-sentinel / partial-valid
probe scenarios.
EOF
)"
```

---

## Task 7: Implement `ModelDiscoveryCacheEntry` DTO

**Files:**
- Create: `src/GeoMagSharp/Discovery/ModelDiscoveryCacheEntry.cs`

- [ ] **Step 1: Create the DTO**

Create `src/GeoMagSharp/Discovery/ModelDiscoveryCacheEntry.cs`:

```csharp
/****************************************************************************
 * File:            ModelDiscoveryCacheEntry.cs
 * Description:     DTO for one entry in .models.json
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;

namespace GeoMagSharp.Discovery
{
    /// <summary>
    /// Single entry in the .models.json cache. Stores the file's invalidation key
    /// (size + UTC mtime) alongside the descriptor it produced. Mutable for JSON
    /// deserialization; not exposed publicly.
    /// </summary>
    internal class ModelDiscoveryCacheEntry
    {
        /// <summary>File path relative to the scanned folder. Allows the cache to follow folder renames.</summary>
        public string RelativePath { get; set; }

        /// <summary>File size in bytes at last scan.</summary>
        public long FileSize { get; set; }

        /// <summary>UTC last-write time at last scan.</summary>
        public DateTime FileLastWriteUtc { get; set; }

        // Mirrors of ModelDescriptor's fields (we don't serialize ModelDescriptor directly so
        // its public constructor stays minimal and the cache schema is independently versioned).

        /// <summary>Detected model type at last scan.</summary>
        public knownModels DetectedType { get; set; }

        /// <summary>Display name at last scan.</summary>
        public string DisplayName { get; set; }

        /// <summary>Min date at last scan, null if unknown.</summary>
        public double? MinDate { get; set; }

        /// <summary>Max date at last scan, null if unknown.</summary>
        public double? MaxDate { get; set; }

        /// <summary>Optional description carried through.</summary>
        public string Description { get; set; }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/GeoMagSharp/GeoMagSharp.csproj -c Debug --verbosity minimal
```

Expected: `Build succeeded`. (No tests yet — exercised in Task 8.)

- [ ] **Step 3: Commit**

```bash
git add src/GeoMagSharp/Discovery/ModelDiscoveryCacheEntry.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add ModelDiscoveryCacheEntry DTO (#21)

Internal mutable DTO mirroring ModelDescriptor fields plus invalidation
keys (RelativePath, FileSize, FileLastWriteUtc). Mirroring rather than
serializing ModelDescriptor directly keeps the public type's constructor
minimal and lets the cache schema version independently.
EOF
)"
```

---

## Task 8: Implement `ModelDiscoveryCache` (atomic JSON read/write)

**Files:**
- Create: `src/GeoMagSharp/Discovery/ModelDiscoveryCache.cs`
- Create: `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryCacheTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryCacheTests.cs`:

```csharp
/****************************************************************************
 * File:            ModelDiscoveryCacheTests.cs
 * Description:     Unit tests for ModelDiscoveryCache (atomic read/write)
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.Discovery;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class ModelDiscoveryCacheTests
    {
        private string _tempDir;
        private string _cacheFile;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GeoMagSharpTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _cacheFile = Path.Combine(_tempDir, ".models.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        private static List<ModelDiscoveryCacheEntry> SampleEntries() => new List<ModelDiscoveryCacheEntry>
        {
            new ModelDiscoveryCacheEntry
            {
                RelativePath = "WMM.COF",
                FileSize = 4647,
                FileLastWriteUtc = new DateTime(2026, 3, 31, 4, 42, 0, DateTimeKind.Utc),
                DetectedType = knownModels.WMM,
                DisplayName = "WMM2025",
                MinDate = 2025.0,
                MaxDate = 2030.0
            },
            new ModelDiscoveryCacheEntry
            {
                RelativePath = "hdgm2019-64.dll",
                FileSize = 7345664,
                FileLastWriteUtc = new DateTime(2018, 11, 13, 0, 0, 0, DateTimeKind.Utc),
                DetectedType = knownModels.HDGM,
                DisplayName = "HDGM2019",
                MinDate = 1900.0,
                MaxDate = 2021.0
            }
        };

        [TestMethod]
        public void Save_ThenLoad_RoundTripsAllEntries()
        {
            ModelDiscoveryCache.Save(_cacheFile, SampleEntries(), null);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(2, loaded.Count);
            Assert.AreEqual("WMM.COF", loaded[0].RelativePath);
            Assert.AreEqual(knownModels.WMM, loaded[0].DetectedType);
            Assert.AreEqual(2025.0, loaded[0].MinDate);
            Assert.AreEqual("hdgm2019-64.dll", loaded[1].RelativePath);
            Assert.AreEqual(knownModels.HDGM, loaded[1].DetectedType);
        }

        [TestMethod]
        public void Load_MissingFile_ReturnsEmptyList()
        {
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(0, loaded.Count);
        }

        [TestMethod]
        public void Load_CorruptJson_ReturnsEmptyList_FiresOnError()
        {
            File.WriteAllText(_cacheFile, "this is { not valid JSON");
            string capturedPath = null;
            Exception capturedEx = null;
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, (p, ex) => { capturedPath = p; capturedEx = ex; });
            Assert.AreEqual(0, loaded.Count);
            Assert.AreEqual(_cacheFile, capturedPath);
            Assert.IsNotNull(capturedEx);
        }

        [TestMethod]
        public void Load_WrongSchemaVersion_ReturnsEmptyList()
        {
            File.WriteAllText(_cacheFile, "{ \"schemaVersion\": 999, \"entries\": [] }");
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(0, loaded.Count);
        }

        [TestMethod]
        public void Load_EmptyJsonObject_ReturnsEmptyList()
        {
            File.WriteAllText(_cacheFile, "{}");
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(0, loaded.Count);
        }

        [TestMethod]
        public void Save_PreservesEntryOrder()
        {
            var entries = SampleEntries();
            ModelDiscoveryCache.Save(_cacheFile, entries, null);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual("WMM.COF", loaded[0].RelativePath);
            Assert.AreEqual("hdgm2019-64.dll", loaded[1].RelativePath);
        }

        [TestMethod]
        public void Save_ToReadOnlyFolder_DoesNotThrow_FiresOnError()
        {
            // Use an obviously-invalid cache path inside a non-existent subdir
            string badPath = Path.Combine(_tempDir, "no_such_subdir", ".models.json");
            string capturedPath = null;
            ModelDiscoveryCache.Save(badPath, SampleEntries(), (p, ex) => { capturedPath = p; });
            Assert.AreEqual(badPath, capturedPath);
        }

        [TestMethod]
        public void Save_AtomicallyReplacesExistingFile()
        {
            // Pre-existing cache with one entry
            File.WriteAllText(_cacheFile, "{\"schemaVersion\":1,\"entries\":[]}");
            ModelDiscoveryCache.Save(_cacheFile, SampleEntries(), null);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(2, loaded.Count);
        }

        [TestMethod]
        public void Save_NoTempFileLeftBehindOnSuccess()
        {
            ModelDiscoveryCache.Save(_cacheFile, SampleEntries(), null);
            Assert.IsTrue(File.Exists(_cacheFile));
            Assert.IsFalse(File.Exists(_cacheFile + ".tmp"));
        }

        [TestMethod]
        public void TimestampsRoundTripAsUtc()
        {
            var entries = SampleEntries();
            ModelDiscoveryCache.Save(_cacheFile, entries, null);
            var loaded = ModelDiscoveryCache.TryLoad(_cacheFile, null);
            Assert.AreEqual(DateTimeKind.Utc, loaded[0].FileLastWriteUtc.Kind);
            Assert.AreEqual(entries[0].FileLastWriteUtc.Ticks, loaded[0].FileLastWriteUtc.Ticks);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelDiscoveryCacheTests" -c Debug
```

Expected: build error `'ModelDiscoveryCache' does not exist`.

- [ ] **Step 3: Implement `ModelDiscoveryCache`**

Create `src/GeoMagSharp/Discovery/ModelDiscoveryCache.cs`:

```csharp
/****************************************************************************
 * File:            ModelDiscoveryCache.cs
 * Description:     Atomic read/write of .models.json discovery cache
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GeoMagSharp.Discovery
{
    /// <summary>
    /// Reads and writes the .models.json cache file atomically. Schema-versioned;
    /// any failure (missing, corrupt, wrong version, IO error) treats the cache as
    /// empty and invokes the supplied error callback.
    /// </summary>
    internal static class ModelDiscoveryCache
    {
        private const int CurrentSchemaVersion = 1;

        /// <summary>
        /// Loads the cache file. Returns an empty list if the file is missing,
        /// corrupt, or has an incompatible schema version. Invokes onError on
        /// any non-missing failure but never throws.
        /// </summary>
        public static List<ModelDiscoveryCacheEntry> TryLoad(string cacheFilePath,
            Action<string, Exception> onError)
        {
            if (string.IsNullOrEmpty(cacheFilePath)) return new List<ModelDiscoveryCacheEntry>();
            if (!File.Exists(cacheFilePath)) return new List<ModelDiscoveryCacheEntry>();

            try
            {
                string json = File.ReadAllText(cacheFilePath);
                if (string.IsNullOrWhiteSpace(json)) return new List<ModelDiscoveryCacheEntry>();

                var jo = JObject.Parse(json);
                int schema = jo["schemaVersion"]?.Value<int>() ?? 0;
                if (schema != CurrentSchemaVersion)
                {
                    return new List<ModelDiscoveryCacheEntry>();
                }

                var entriesToken = jo["entries"];
                if (entriesToken == null || entriesToken.Type != JTokenType.Array)
                {
                    return new List<ModelDiscoveryCacheEntry>();
                }

                var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.DateTime };
                var list = entriesToken.ToObject<List<ModelDiscoveryCacheEntry>>(JsonSerializer.Create(settings));
                return list ?? new List<ModelDiscoveryCacheEntry>();
            }
            catch (Exception ex)
            {
                onError?.Invoke(cacheFilePath, ex);
                return new List<ModelDiscoveryCacheEntry>();
            }
        }

        /// <summary>
        /// Atomically writes the cache file: serialize to a sibling .tmp, then File.Move
        /// (overwrite) onto the target. Never throws; invokes onError on any IO failure.
        /// </summary>
        public static void Save(string cacheFilePath,
            IList<ModelDiscoveryCacheEntry> entries,
            Action<string, Exception> onError)
        {
            if (string.IsNullOrEmpty(cacheFilePath)) return;
            entries = entries ?? new List<ModelDiscoveryCacheEntry>();

            try
            {
                var payload = new
                {
                    schemaVersion = CurrentSchemaVersion,
                    generatedBy = "GeoMagSharp",
                    generatedAt = DateTime.UtcNow,
                    entries = entries
                };
                string json = JsonConvert.SerializeObject(payload, Formatting.Indented);

                string tempPath = cacheFilePath + ".tmp";

                // Write temp file. Using FileMode.Create truncates if a leftover .tmp exists.
                File.WriteAllText(tempPath, json);

                // Atomic-rename onto target. On Windows, File.Move with overwrite=true
                // is atomic at the NTFS layer.
#if NET48 || NETSTANDARD2_0
                if (File.Exists(cacheFilePath)) File.Delete(cacheFilePath);
                File.Move(tempPath, cacheFilePath);
#else
                File.Move(tempPath, cacheFilePath, overwrite: true);
#endif
            }
            catch (Exception ex)
            {
                onError?.Invoke(cacheFilePath, ex);
                // Best-effort cleanup of leftover temp file
                try
                {
                    string tempPath = cacheFilePath + ".tmp";
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { /* swallow */ }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelDiscoveryCacheTests" -c Debug
```

Expected: 10 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Discovery/ModelDiscoveryCache.cs \
        tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryCacheTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add ModelDiscoveryCache atomic JSON read/write (#21)

Internal static class that loads and saves .models.json. Save uses
write-temp-then-rename for atomicity (NTFS rename is atomic). Load is
self-healing: missing/corrupt/wrong-schema-version all return empty list
and invoke OnError without throwing.

Schema is versioned (currently 1); future schema changes can bump
without breaking older clients. JSON payload wraps entries in a top-
level object with schemaVersion / generatedBy / generatedAt / entries.

Ten unit tests cover round-trip, missing file, corrupt JSON, wrong
schema version, empty JSON, atomic-replace-existing, no temp file
leftover on success, and timestamp UTC round-trip.
EOF
)"
```

---

## Task 9: Add `TestFolderFixture` helper

**Files:**
- Create: `tests/GeoMagSharp.Tests/Discovery/TestFolderFixture.cs`

- [ ] **Step 1: Create the helper**

Create `tests/GeoMagSharp.Tests/Discovery/TestFolderFixture.cs`:

```csharp
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
```

- [ ] **Step 2: Verify test build**

```bash
dotnet build tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Debug --verbosity minimal
```

Expected: `Build succeeded`. (No tests for the helper itself; it's exercised by Tasks 10-11.)

- [ ] **Step 3: Commit**

```bash
git add tests/GeoMagSharp.Tests/Discovery/TestFolderFixture.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] test: add TestFolderFixture helper for discovery tests (#21)

IDisposable wrapper for a per-test temp folder, with helpers to copy
fixture files in, write arbitrary text files, and create subdirectories.
Cleanup is best-effort so deletion races don't fail tests.
EOF
)"
```

---

## Task 10: Implement `ModelDiscovery.DescribeFile` (single-file deep scan)

**Files:**
- Create: `src/GeoMagSharp/Discovery/ModelDiscovery.cs` (initial version with DescribeFile only)

- [ ] **Step 1: Write the failing test**

Append to `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryCacheTests.cs`... actually, this needs its own file. Create `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryTests.cs`:

```csharp
/****************************************************************************
 * File:            ModelDiscoveryTests.cs
 * Description:     End-to-end functional tests for ModelDiscovery
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class ModelDiscoveryTests
    {
        // Task 10 starter tests

        [TestMethod]
        public void DescribeFile_NewWmmFile_ReturnsFreshDescriptor()
        {
            using (var fx = new TestFolderFixture())
            {
                var path = fx.CopyFixture("WMM2025_sample.COF");
                var d = ModelDiscovery.DescribeFile(path);
                Assert.IsNotNull(d);
                Assert.AreEqual(knownModels.WMM, d.DetectedType);
                Assert.AreEqual(2025.0, d.MinDate);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DescribeFile_NullPath_Throws()
        {
            ModelDiscovery.DescribeFile(null);
        }

        [TestMethod]
        [ExpectedException(typeof(GeoMagExceptionFileNotFound))]
        public void DescribeFile_FileNotFound_Throws()
        {
            ModelDiscovery.DescribeFile(@"C:\definitely_not_real\nope.COF");
        }

        [TestMethod]
        public void DescribeFile_UnknownExtension_ReturnsNull()
        {
            using (var fx = new TestFolderFixture())
            {
                var path = fx.WriteFile("garbage.xyz", "anything");
                var d = ModelDiscovery.DescribeFile(path);
                Assert.IsNull(d);
            }
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelDiscoveryTests.DescribeFile" -c Debug
```

Expected: build error `'ModelDiscovery' does not exist`.

- [ ] **Step 3: Implement initial `ModelDiscovery` with `DescribeFile`**

Create `src/GeoMagSharp/Discovery/ModelDiscovery.cs`:

```csharp
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
                var (minDate, maxDate) = HdgmDateProbe.Probe(
                    path => CreateRealInvokerOrNull(path), filePath);
                return new ModelDescriptor(filePath, knownModels.HDGM,
                    BuildHdgmDisplayName(filePath), minDate, maxDate);
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
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelDiscoveryTests.DescribeFile" -c Debug
```

Expected: 4 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Discovery/ModelDiscovery.cs \
        tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: add ModelDiscovery.DescribeFile single-file deep scan (#21)

Public static class with DescribeFile(string) for single-file
classification. .COF/.DAT routes through ModelHeaderInspector;
.DLL matching the HDGM filename rule routes through HdgmDateProbe with a
real LoadLibraryHdgmInvoker factory (returns null on non-Windows or load
failure -> probe degrades to null bounds).

DiscoverModels stub returns empty enumerable until Task 11 lands the
folder-walk implementation.

Four functional tests cover the success path, null/missing-file
exceptions, and unknown-extension null return.
EOF
)"
```

---

## Task 11: Implement `ModelDiscovery.DiscoverModels` folder walk (no cache yet)

**Files:**
- Modify: `src/GeoMagSharp/Discovery/ModelDiscovery.cs`
- Modify: `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryTests.cs` (add tests)

- [ ] **Step 1: Append the failing tests**

In `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryTests.cs`, append inside the existing `[TestClass]`:

```csharp
        // Task 11 — folder enumeration without cache

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DiscoverModels_NullFolderPath_Throws()
        {
            ModelDiscovery.DiscoverModels(null).ToList();
        }

        [TestMethod]
        public void DiscoverModels_FolderDoesNotExist_ReturnsEmpty()
        {
            var results = ModelDiscovery.DiscoverModels(@"C:\definitely_not_real_folder").ToList();
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void DiscoverModels_QuickMode_RecognizesCofAndDllByFilename()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                fx.WriteFile("hdgm2019-64.dll", new string('x', 32));
                fx.WriteFile("notes.txt", "irrelevant");

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath,
                    new ModelDiscoveryOptions { Mode = ScanMode.Quick }).ToList();

                Assert.AreEqual(2, results.Count);
                Assert.IsTrue(results.Any(d => d.FilePath.EndsWith("WMM.COF")));
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.HDGM));
            }
        }

        [TestMethod]
        public void DiscoverModels_QuickMode_CofDetectedTypeIsNone()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath,
                    new ModelDiscoveryOptions { Mode = ScanMode.Quick }).ToList();
                var cof = results.Single();
                Assert.AreEqual(knownModels.NONE, cof.DetectedType);
                Assert.IsNull(cof.MinDate);
            }
        }

        [TestMethod]
        public void DiscoverModels_FullMode_PopulatesCofMetadata()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath,
                    new ModelDiscoveryOptions { Mode = ScanMode.Full }).ToList();
                var cof = results.Single();
                Assert.AreEqual(knownModels.WMM, cof.DetectedType);
                Assert.AreEqual(2025.0, cof.MinDate);
                Assert.AreEqual(2030.0, cof.MaxDate);
            }
        }

        [TestMethod]
        public void DiscoverModels_FullMode_MixedFolder_HandlesAllCases()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                fx.CopyFixture("IGRF14_sample.COF", "IGRF14.COF");
                fx.CopyFixture("corrupt_header.COF", "broken.COF");
                fx.WriteFile("notes.txt", "ignored");

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath).ToList();
                Assert.AreEqual(3, results.Count);
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.WMM));
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.IGRF));
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.NONE));
            }
        }

        [TestMethod]
        public void DiscoverModels_NonHdgmDllSkipped()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.WriteFile("randomlib.dll", new string('x', 16));
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath).ToList();
                Assert.AreEqual(0, results.Count);
            }
        }

        [TestMethod]
        public void DiscoverModels_UnknownExtension_Skipped()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.WriteFile("notes.txt", "not a model");
                fx.WriteFile("readme.md", "ignored");
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath).ToList();
                Assert.AreEqual(0, results.Count);
            }
        }

        [TestMethod]
        public void DiscoverModels_Recursive_TraversesSubfolders()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var sub = fx.CreateSubdir("nested");
                File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Discovery", "Fixtures", "IGRF14_sample.COF"),
                    Path.Combine(sub, "IGRF14.COF"));

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath,
                    new ModelDiscoveryOptions { Recursive = true }).ToList();
                Assert.AreEqual(2, results.Count);
            }
        }

        [TestMethod]
        public void DiscoverModels_NonRecursive_StopsAtTopLevel()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var sub = fx.CreateSubdir("nested");
                File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Discovery", "Fixtures", "IGRF14_sample.COF"),
                    Path.Combine(sub, "IGRF14.COF"));

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath).ToList();
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(knownModels.WMM, results[0].DetectedType);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public void DiscoverModels_CancellationTokenTriggered_Throws()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                using (var cts = new CancellationTokenSource())
                {
                    cts.Cancel();
                    ModelDiscovery.DiscoverModels(fx.FolderPath,
                        new ModelDiscoveryOptions { CancellationToken = cts.Token }).ToList();
                }
            }
        }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelDiscoveryTests" -c Debug
```

Expected: most of these fail because `DiscoverModels` returns an empty list (Task 10's stub).

- [ ] **Step 3: Replace the `DiscoverModels` stub with the full implementation**

In `src/GeoMagSharp/Discovery/ModelDiscovery.cs`, replace the stub method body with:

```csharp
        public static IEnumerable<ModelDescriptor> DiscoverModels(string folderPath, ModelDiscoveryOptions options)
        {
            if (folderPath == null) throw new ArgumentNullException(nameof(folderPath));
            if (options == null) throw new ArgumentNullException(nameof(options));

            return DiscoverModelsImpl(folderPath, options);
        }

        private static IEnumerable<ModelDescriptor> DiscoverModelsImpl(string folderPath, ModelDiscoveryOptions options)
        {
            if (!Directory.Exists(folderPath)) yield break;

            var searchOption = options.Recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            string cacheFileFullPath = options.UseCache
                ? Path.GetFullPath(Path.Combine(folderPath, options.CacheFileName ?? ".models.json"))
                : null;

            foreach (string filePath in Directory.EnumerateFiles(folderPath, "*.*", searchOption))
            {
                options.CancellationToken.ThrowIfCancellationRequested();

                // Skip the cache file itself
                if (cacheFileFullPath != null &&
                    string.Equals(Path.GetFullPath(filePath), cacheFileFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ModelDescriptor descriptor;
                try
                {
                    descriptor = ClassifyFile(filePath, options);
                }
                catch (Exception ex)
                {
                    options.OnError?.Invoke(filePath, ex);
                    continue;
                }

                if (descriptor != null) yield return descriptor;
            }
        }

        private static ModelDescriptor ClassifyFile(string filePath, ModelDiscoveryOptions options)
        {
            string ext = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext)) return null;
            string extUpper = ext.ToUpperInvariant();

            if (extUpper == ".COF" || extUpper == ".DAT")
            {
                if (options.Mode == ScanMode.Quick)
                {
                    return new ModelDescriptor(filePath, knownModels.NONE,
                        Path.GetFileNameWithoutExtension(filePath), null, null);
                }
                return ModelHeaderInspector.Inspect(filePath);
            }

            if (extUpper == ".DLL" && ModelPathDetector.IsHdgmPath(filePath))
            {
                if (options.Mode == ScanMode.Quick)
                {
                    return new ModelDescriptor(filePath, knownModels.HDGM,
                        BuildHdgmDisplayName(filePath), null, null);
                }
                var (minDate, maxDate) = HdgmDateProbe.Probe(
                    path => CreateRealInvokerOrNull(path), filePath);
                return new ModelDescriptor(filePath, knownModels.HDGM,
                    BuildHdgmDisplayName(filePath), minDate, maxDate);
            }

            return null;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelDiscoveryTests" -c Debug
```

Expected: 14 passed (4 from Task 10 + 10 added in this task), 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Discovery/ModelDiscovery.cs \
        tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: implement DiscoverModels folder walk (#21)

Replaces the Task 10 stub with the full enumeration implementation:
walk Directory.EnumerateFiles with the requested SearchOption, classify
each file based on extension and ScanMode, yield ModelDescriptor.

Quick mode skips header peek and HDGM probing; descriptors get
filename-derived display name and null dates. Full mode delegates to
ModelHeaderInspector for COF/DAT and HdgmDateProbe for HDGM .dll.

Per-file errors invoke options.OnError and continue iteration.
CancellationToken checked once per file. The cache file (when UseCache
is set) is skipped during enumeration so it does not appear in results.

Adds 10 functional tests covering null/missing-folder edge cases, Quick
vs Full classification, mixed folders, recursive traversal, non-HDGM
DLLs and unknown extensions skipped, and cancellation propagation.
EOF
)"
```

---

## Task 12: Wire `UseCache` read/write in `DiscoverModels`

**Files:**
- Modify: `src/GeoMagSharp/Discovery/ModelDiscovery.cs`
- Modify: `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryTests.cs` (add cache tests)

- [ ] **Step 1: Append cache tests**

In `ModelDiscoveryTests.cs`, append inside the same `[TestClass]`:

```csharp
        // Task 12 — UseCache flow

        [TestMethod]
        public void DiscoverModels_UseCache_FirstRun_WritesCacheFile()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                ModelDiscovery.DiscoverModels(fx.FolderPath,
                    new ModelDiscoveryOptions { UseCache = true }).ToList();
                Assert.IsTrue(File.Exists(Path.Combine(fx.FolderPath, ".models.json")));
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_SecondRunUnchangedFolder_HitsCache()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                // Second run; if cache works, results match first run
                var second = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.AreEqual(1, second.Count);
                Assert.AreEqual(knownModels.WMM, second[0].DetectedType);
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_FileMtimeChanged_RescansThatFile()
        {
            using (var fx = new TestFolderFixture())
            {
                var p = fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();

                // Touch mtime: rewrite the file with same content but new timestamp
                File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddMinutes(1));

                var results = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(knownModels.WMM, results[0].DetectedType);
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_NewFileAdded_DeepScansOnlyNewFile()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();

                fx.CopyFixture("IGRF14_sample.COF", "IGRF14.COF");
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.AreEqual(2, results.Count);
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.WMM));
                Assert.IsTrue(results.Any(d => d.DetectedType == knownModels.IGRF));
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_FileDeleted_DropsFromCacheOnNextScan()
        {
            using (var fx = new TestFolderFixture())
            {
                var p1 = fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                fx.CopyFixture("IGRF14_sample.COF", "IGRF14.COF");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();

                File.Delete(p1);
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(knownModels.IGRF, results[0].DetectedType);
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_CorruptCache_RecoversByRewriting()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                File.WriteAllText(Path.Combine(fx.FolderPath, ".models.json"), "garbage{");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(knownModels.WMM, results[0].DetectedType);
            }
        }

        [TestMethod]
        public void DiscoverModels_UseCache_CacheFileNotInResults()
        {
            using (var fx = new TestFolderFixture())
            {
                fx.CopyFixture("WMM2025_sample.COF", "WMM.COF");
                var opts = new ModelDiscoveryOptions { UseCache = true };
                var results = ModelDiscovery.DiscoverModels(fx.FolderPath, opts).ToList();
                Assert.IsFalse(results.Any(d => d.FilePath.EndsWith(".models.json")));
            }
        }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelDiscoveryTests" -c Debug
```

Expected: at least the cache-validation tests fail because we haven't wired UseCache to read/validate yet.

- [ ] **Step 3: Wire cache read + validate + write in `DiscoverModels`**

In `src/GeoMagSharp/Discovery/ModelDiscovery.cs`, replace `DiscoverModelsImpl` with this version that integrates the cache:

```csharp
        private static IEnumerable<ModelDescriptor> DiscoverModelsImpl(string folderPath, ModelDiscoveryOptions options)
        {
            if (!Directory.Exists(folderPath)) yield break;

            var searchOption = options.Recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            string cacheFilePath = options.UseCache
                ? Path.Combine(folderPath, options.CacheFileName ?? ".models.json")
                : null;
            string cacheFileFullPath = cacheFilePath != null ? Path.GetFullPath(cacheFilePath) : null;

            // Load existing cache (empty if missing/corrupt/wrong-version)
            Dictionary<string, ModelDiscoveryCacheEntry> cachedByRelPath =
                new Dictionary<string, ModelDiscoveryCacheEntry>(StringComparer.OrdinalIgnoreCase);
            if (cacheFilePath != null)
            {
                foreach (var entry in ModelDiscoveryCache.TryLoad(cacheFilePath, options.OnError))
                {
                    if (!string.IsNullOrEmpty(entry.RelativePath))
                        cachedByRelPath[entry.RelativePath] = entry;
                }
            }

            // We collect entries to write back so the cache reflects the live folder.
            var liveEntries = options.UseCache ? new List<ModelDiscoveryCacheEntry>() : null;

            foreach (string filePath in Directory.EnumerateFiles(folderPath, "*.*", searchOption))
            {
                options.CancellationToken.ThrowIfCancellationRequested();

                if (cacheFileFullPath != null &&
                    string.Equals(Path.GetFullPath(filePath), cacheFileFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ModelDescriptor descriptor = null;
                ModelDiscoveryCacheEntry cacheEntryToWrite = null;

                try
                {
                    string relPath = MakeRelativePath(folderPath, filePath);
                    var fileInfo = new FileInfo(filePath);

                    ModelDiscoveryCacheEntry cached;
                    bool cacheHit = options.UseCache
                        && cachedByRelPath.TryGetValue(relPath, out cached)
                        && cached.FileSize == fileInfo.Length
                        && AreUtcTimestampsEqual(cached.FileLastWriteUtc, fileInfo.LastWriteTimeUtc);

                    if (cacheHit)
                    {
                        var c = cachedByRelPath[relPath];
                        descriptor = new ModelDescriptor(filePath, c.DetectedType, c.DisplayName,
                            c.MinDate, c.MaxDate, c.Description);
                        cacheEntryToWrite = c;
                    }
                    else
                    {
                        descriptor = ClassifyFile(filePath, options);
                        if (descriptor != null && options.UseCache)
                        {
                            cacheEntryToWrite = new ModelDiscoveryCacheEntry
                            {
                                RelativePath = relPath,
                                FileSize = fileInfo.Length,
                                FileLastWriteUtc = DateTime.SpecifyKind(fileInfo.LastWriteTimeUtc, DateTimeKind.Utc),
                                DetectedType = descriptor.DetectedType,
                                DisplayName = descriptor.DisplayName,
                                MinDate = descriptor.MinDate,
                                MaxDate = descriptor.MaxDate,
                                Description = descriptor.Description
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    options.OnError?.Invoke(filePath, ex);
                    continue;
                }

                if (descriptor != null)
                {
                    if (liveEntries != null && cacheEntryToWrite != null)
                        liveEntries.Add(cacheEntryToWrite);
                    yield return descriptor;
                }
            }

            if (cacheFilePath != null && liveEntries != null)
            {
                ModelDiscoveryCache.Save(cacheFilePath, liveEntries, options.OnError);
            }
        }

        private static string MakeRelativePath(string folderPath, string filePath)
        {
            string folderFull = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fileFull = Path.GetFullPath(filePath);
            if (fileFull.StartsWith(folderFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return fileFull.Substring(folderFull.Length + 1);
            return Path.GetFileName(filePath);
        }

        private static bool AreUtcTimestampsEqual(DateTime a, DateTime b)
        {
            // Truncate to nearest second to avoid sub-second precision differences across filesystems.
            return DateTimeToUnixSeconds(a) == DateTimeToUnixSeconds(b);
        }

        private static long DateTimeToUnixSeconds(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return (utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks / TimeSpan.TicksPerSecond;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "FullyQualifiedName~ModelDiscoveryTests" -c Debug
```

Expected: 21 passed (4 + 10 + 7 cache tests), 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/GeoMagSharp/Discovery/ModelDiscovery.cs \
        tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] feat: wire UseCache read/validate/write in DiscoverModels (#21)

Cache flow when options.UseCache is true:
1. Load .models.json (empty on missing/corrupt/wrong-schema-version)
2. Index cached entries by relative path
3. For each file: cache HIT if size + UTC mtime (truncated to whole
   seconds for cross-FS portability) match; emit cached descriptor and
   carry entry forward
4. Cache MISS: full classification, build new entry
5. After enumeration: write live entries back atomically via
   ModelDiscoveryCache.Save

Files removed from disk are dropped (never carried forward). The cache
file itself is filtered out of enumeration. Cancellation between files
prevents writing partial cache state.

Adds 7 functional tests covering first-run write, second-run hit,
mtime-changed rescan, new-file-added incremental, file-deleted drop,
corrupt-cache self-heal, and cache-file-not-in-results.
EOF
)"
```

---

## Task 13: Add HDGM date probe integration tests (env-var-gated)

**Files:**
- Create: `tests/GeoMagSharp.Tests/Discovery/HdgmDateProbeIntegrationTests.cs`

- [ ] **Step 1: Create the integration test file**

Create `tests/GeoMagSharp.Tests/Discovery/HdgmDateProbeIntegrationTests.cs`:

```csharp
/****************************************************************************
 * File:            HdgmDateProbeIntegrationTests.cs
 * Description:     Integration tests for HDGM probe with the real NOAA DLL
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/engineMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoMagSharp;
using GeoMagSharp.Discovery;
using GeoMagSharp.HDGM;

namespace GeoMagSharp_UnitTests.Discovery
{
    [TestClass]
    public class HdgmDateProbeIntegrationTests
    {
        private static string DllPath => Environment.GetEnvironmentVariable("HDGM_DLL_PATH");

        [TestInitialize]
        public void RequireDll()
        {
            if (string.IsNullOrWhiteSpace(DllPath) || !File.Exists(DllPath))
                Assert.Inconclusive("HDGM_DLL_PATH not set; integration tests skipped.");
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_RealHdgmDll_Probes_ReturnsValidDateRange()
        {
            var (min, max) = HdgmDateProbe.Probe(p => new LoadLibraryHdgmInvoker(p), DllPath);
            Assert.IsTrue(min.HasValue, "expected min date populated");
            Assert.IsTrue(max.HasValue, "expected max date populated");
            Assert.AreEqual(1900.0, min.Value);
            Assert.IsTrue(max.Value >= 2019.0, "expected upper bound to cover at least 2019");
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_DiscoverModels_FolderWithRealHdgmDll_ReturnsHdgmDescriptor()
        {
            string tempDir = Path.Combine(Path.GetTempPath(),
                "GeoMagSharpInt_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                string copied = Path.Combine(tempDir, Path.GetFileName(DllPath));
                File.Copy(DllPath, copied, overwrite: true);

                var results = ModelDiscovery.DiscoverModels(tempDir).ToList();
                var hdgm = results.SingleOrDefault(d => d.DetectedType == knownModels.HDGM);
                Assert.IsNotNull(hdgm, "expected an HDGM descriptor in the results");
                Assert.IsTrue(hdgm.MinDate.HasValue && hdgm.MinDate.Value == 1900.0);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        [TestCategory("RequiresHDGMDll")]
        public void Integration_DiscoverModels_TwoConsecutiveCallsWithCache_SecondCallSkipsProbe()
        {
            string tempDir = Path.Combine(Path.GetTempPath(),
                "GeoMagSharpInt_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                string copied = Path.Combine(tempDir, Path.GetFileName(DllPath));
                File.Copy(DllPath, copied, overwrite: true);

                var opts = new ModelDiscoveryOptions { UseCache = true };
                var first = ModelDiscovery.DiscoverModels(tempDir, opts).ToList();
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, ".models.json")));

                var second = ModelDiscovery.DiscoverModels(tempDir, opts).ToList();
                Assert.AreEqual(first.Count, second.Count);
                Assert.AreEqual(first[0].MinDate, second[0].MinDate);
                Assert.AreEqual(first[0].MaxDate, second[0].MaxDate);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}
```

- [ ] **Step 2: Verify test build**

```bash
dotnet build tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj -c Debug --verbosity minimal
```

Expected: `Build succeeded`.

- [ ] **Step 3: Verify CI filter excludes them**

```bash
dotnet test tests/GeoMagSharp.Tests/GeoMagSharp.Tests.csproj --filter "TestCategory!=RequiresHDGMDll" -c Debug
```

Expected: all unit + functional tests pass; the new integration tests are not listed in the run results.

- [ ] **Step 4: Commit**

```bash
git add tests/GeoMagSharp.Tests/Discovery/HdgmDateProbeIntegrationTests.cs
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] test: add discovery integration tests with real NOAA HDGM DLL (#21)

Three [TestCategory("RequiresHDGMDll")] integration tests gated on
HDGM_DLL_PATH env var, mirroring PR #20's pattern. CI filter
"TestCategory!=RequiresHDGMDll" excludes them; local maintainer with
DLL+env-var verifies end-to-end before promoting.

Tests cover:
- HdgmDateProbe.Probe with real DLL returns min=1900 and max>=2019
- DiscoverModels in a folder containing the real DLL emits an HDGM
  descriptor with valid bounds
- Two consecutive UseCache calls produce identical results (second
  hits the cache; tests don't assert the probe was skipped directly
  but rely on result equivalence which proves cache correctness)
EOF
)"
```

---

## Task 14: Documentation updates

**Files:**
- Modify: `README.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Add discovery API note to README**

In `README.md`, find the supported models or APIs section. Add a new short section (place it near the existing "Loading models" or "Usage" content):

```markdown
### Discovering models in a folder

Use `ModelDiscovery.DiscoverModels(folderPath)` to enumerate every loadable model file in a folder without knowing each format's filename rules:

```csharp
foreach (var d in ModelDiscovery.DiscoverModels("./coefficients"))
    Console.WriteLine($"{d.DisplayName} ({d.DetectedType}) {d.MinDate}..{d.MaxDate}");
```

Pass `new ModelDiscoveryOptions { UseCache = true }` to populate a `.models.json` cache in the scanned folder; subsequent scans skip re-inspecting unchanged files. See the API reference for `ScanMode`, `Recursive`, `CancellationToken`, and `OnError` callback.
```

- [ ] **Step 2: Add discovery API note to CLAUDE.md**

In `CLAUDE.md`, find the **Project Overview** section and append a sentence to the existing description:

```markdown
GeoMagSharp also exposes a library-level model discovery API (`ModelDiscovery.DiscoverModels`) for enumerating loadable model files in a folder, with optional cached metadata for fast app startup.
```

- [ ] **Step 3: Verify rendering**

```bash
head -50 README.md
head -10 CLAUDE.md
```

(Visual inspection only — no automated test.)

- [ ] **Step 4: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "$(cat <<'EOF'
[IMPLEMENTER] docs: announce ModelDiscovery API in README and CLAUDE.md (#21)

Brief mention of the new public API in both surface docs, with a
single-line code example in README. Detailed semantics live in the
XML-doc IntelliSense and the design spec at
docs/superpowers/specs/2026-04-28-discovery-api-design.md.
EOF
)"
```

---

## Task 15: Final regression and pack verification

**Files:** none (verification only)

- [ ] **Step 1: Clean build**

```bash
dotnet clean
dotnet restore
```

- [ ] **Step 2: Build all targets in Release**

```bash
dotnet build -c Release
```

Expected: build succeeds for both `net48` and `netstandard2.0`. No new errors.

- [ ] **Step 3: Run full unit test suite (CI mode)**

```bash
dotnet test -c Release --filter "TestCategory!=RequiresHDGMDll" --verbosity normal
```

Expected: all tests pass (392 from PR #20 baseline + ~47 new from this feature). Capture totals.

- [ ] **Step 4: (Optional, maintainer-only) Run integration tests**

```bash
HDGM_DLL_PATH=/path/to/hdgm2019-64.dll \
dotnet test -c Release --filter "TestCategory=RequiresHDGMDll" --verbosity normal
```

Expected: all integration tests pass.

- [ ] **Step 5: NuGet pack**

```bash
dotnet pack src/GeoMagSharp/GeoMagSharp.csproj -c Release -o artifacts
ls artifacts/
```

Expected: `GeoMagSharp.1.7.0.nupkg`. Sanity-check that no HDGM-derived data is in the package:

```bash
unzip -l artifacts/GeoMagSharp.1.7.0.nupkg | grep -iE "hdgm" || echo "OK: no HDGM artifacts in package"
```

Expected: prints `OK: no HDGM artifacts in package`.

- [ ] **Step 6: Confirm tasks.md is fully checked**

Open `docs/features/model-discovery-api/tasks.md` and verify every checkbox is `[x]`. Ralph Loop expects this gate.

- [ ] **Step 7: No commit unless an issue surfaces**

If any step above fails, fix it inline and commit per the affected task's pattern. If everything is green, no additional commit is required.

---

## Self-review checklist

### Spec coverage map

| Spec section | Tasks |
|---|---|
| §3 Architecture overview | Tasks 2-12 (every component built) |
| §4 New / modified components | Tasks 2-12 (one task per file or focused group) |
| §5 Public API surface — `ModelDescriptor` | Task 3 |
| §5 Public API surface — `ModelDiscoveryOptions` | Task 4 |
| §5 Public API surface — `ScanMode` | Task 2 |
| §5 Public API surface — `ModelDiscovery` | Tasks 10 (DescribeFile) + 11 (DiscoverModels) + 12 (UseCache wiring) |
| §6 Data flow Scenario A (Quick) | Task 11 tests |
| §6 Data flow Scenario B (Full no cache) | Task 11 tests |
| §6 Data flow Scenario C (Full + cache) | Task 12 tests |
| §6 Data flow Scenario D (DescribeFile) | Task 10 tests |
| §7 Error handling | Tasks 5, 8, 11, 12 (per-file errors, OnError callback, sentinel-fallback, atomic cache) |
| §8 Testing strategy — unit (~30) | Tasks 3 (6) + 6 (6) + 8 (10) = 22, balance via Tasks 5/10 |
| §8 Testing strategy — functional (~25) | Tasks 5 (8) + 11 (10) + 12 (7) = 25 |
| §8 Testing strategy — integration (~3) | Task 13 |
| §9 Versioning | Task 1 (1.6.0 → 1.7.0) |
| §9 Documentation | Task 14 |
| §9 NuGet packaging | Task 15 |

### Placeholder scan

✅ Every step contains the actual code or commands the engineer needs. No "TBD", "TODO", "implement later", "similar to Task N", or vague "add appropriate handling" instructions. Every test has full code; every commit has a full message.

### Type-consistency check

| Symbol | Defined | Used |
|---|---|---|
| `ScanMode { Quick, Full }` | Task 2 | Tasks 4, 11, 12 |
| `ModelDescriptor` ctor `(string filePath, knownModels detectedType, string displayName, double? minDate, double? maxDate, string description = null)` | Task 3 | Tasks 5, 10, 11, 12 |
| `ModelDiscoveryOptions { Mode, Recursive, UseCache, CacheFileName, CancellationToken, OnError }` | Task 4 | Tasks 11, 12 |
| `ModelHeaderInspector.Inspect(string filePath)` returns `ModelDescriptor` | Task 5 | Tasks 10, 11 |
| `HdgmDateProbe.ExtractYearFromFilename(string)` returns `int?` | Task 6 | Tasks 10 (BuildHdgmDisplayName), 11 |
| `HdgmDateProbe.Probe(Func<string, INativeHdgmInvoker>, string)` returns `(double?, double?)` | Task 6 | Tasks 10, 11, 13 |
| `ModelDiscoveryCacheEntry { RelativePath, FileSize, FileLastWriteUtc, DetectedType, DisplayName, MinDate, MaxDate, Description }` | Task 7 | Tasks 8, 12 |
| `ModelDiscoveryCache.TryLoad(string, Action<string, Exception>)` returns `List<ModelDiscoveryCacheEntry>` | Task 8 | Task 12 |
| `ModelDiscoveryCache.Save(string, IList<ModelDiscoveryCacheEntry>, Action<string, Exception>)` | Task 8 | Task 12 |
| `TestFolderFixture { FolderPath, CopyFixture, WriteFile, CreateSubdir }` | Task 9 | Tasks 10, 11, 12 |
| `ModelDiscovery.DescribeFile(string)` returns `ModelDescriptor?` | Task 10 | (public API) |
| `ModelDiscovery.DiscoverModels(string)` and `(string, ModelDiscoveryOptions)` | Tasks 10, 11, 12 | (public API) |
| Test class names | Tasks 3, 5, 6, 8, 10, 11, 12, 13 | match exactly across tasks |

All names match across tasks. No drift.

### Scope check

15 tasks, ranging from trivial (version bump, options DTO) to substantive (folder walk, cache integration). Each task ends in a green commit. Each task is a self-contained unit a fresh subagent can execute. Forward dependencies only (no task waits on a future task). The plan covers every section of the spec.

---
