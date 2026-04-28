# Model Discovery API — Design

**Date:** 2026-04-28
**Status:** Draft pending user review
**Target version:** v1.7.0 (MINOR per semver — additive feature, backward-compatible)
**Issue:** #21
**Branch:** `feature/21-model-discovery-api`

## 1. Problem statement

GeoMagSharpGUI maintains its own folder scanner that hard-codes `*.cof` and `*.dat` extensions. When PR #20 added HDGM `.dll` support to GeoMagSharp 1.6.0, the GUI's filter and discovery logic had to be updated separately (PR #56). Each future model format the library learns to load (BGGM `.bin`, hypothetical `.json` manifests, etc.) repeats the same dance: library knows the rule, every consumer must be updated to mirror it.

The library is the only piece that knows the ground truth about which files it can load. It should expose that knowledge through a discovery API so consumers ask GeoMagSharp "what models exist in this folder?" rather than maintaining parallel format lists.

This was the deferred follow-up explicitly noted in the HDGM design's Section 11:

> `GeoMag.DiscoverModels(folder)` API — Library-level discovery returning `ModelDescriptor` records. Replaces GUI's local scanner.

This spec consolidates the design discussion that followed the HDGM merge (PR #20) and produces a single-PR-scope feature for v1.7.0.

## 2. Scope

### In scope

- New public static class `ModelDiscovery` with three methods: `DiscoverModels(string)`, `DiscoverModels(string, ModelDiscoveryOptions)`, `DescribeFile(string)`
- New public types: `ModelDescriptor` (immutable), `ModelDiscoveryOptions` (mutable), `ScanMode` enum
- Two scan modes: **Quick** (filename only), **Full** (header peek for COF/DAT, native probe for HDGM)
- Optional folder-local cache (`.models.json`) with mtime + size invalidation, atomic write, schema versioning
- HDGM date-range probing in Full mode (LoadLibraryEx + up to 8 forward `hdgmcalc` calls, year extracted from filename or current year)
- Recursive folder traversal (opt-in)
- `CancellationToken` support
- `OnError(string filePath, Exception)` callback for non-fatal errors
- Unit tests with FakeHdgmInvoker for date probe; functional tests against fixture COF/DAT files; env-var-gated integration tests for real NOAA DLL probe

### Out of scope (deferred)

- Auto-watching folders for changes (FileSystemWatcher integration)
- Caching across processes other than the in-folder `.models.json`
- Cross-drive or UNC-path recursion safeguards beyond .NET defaults
- Auto-resolving symbolic-link cycles
- Probing DLLs to validate Authenticode / NOAA provenance
- BGGM2019+ detection (no sample available; separate issue if/when one arrives)
- Async API variant (`IAsyncEnumerable<ModelDescriptor>`) — requires .NET Standard 2.1
- Replacing GeoMagSharpGUI's local scanner (consumer-side migration is a follow-up GUI PR)

## 3. Architecture overview

```
                                    ┌─────────────────────────────┐
                                    │  Consumer (GUI / app)       │
                                    └──────────────┬──────────────┘
                                                   │
                                       DiscoverModels(folder, opts)
                                                   │
                                                   ▼
                                    ┌─────────────────────────────┐
                                    │  ModelDiscovery (public)     │
                                    │  - Folder enumeration        │
                                    │  - File classification        │
                                    │  - Cache read/validate/write │
                                    └──────┬───────┬───────────────┘
                                           │       │
                       ┌───────────────────┘       └───────────────────┐
                       ▼                                                ▼
            ┌──────────────────────┐                          ┌──────────────────────┐
            │ ModelHeaderInspector │                          │ HdgmDateProbe         │
            │ (internal)           │                          │ (internal)            │
            │ - COF / DAT peek     │                          │ - LoadLibraryEx       │
            │ - read first line    │                          │ - hdgmcalc forward    │
            │ - extract type+year  │                          │ - find max valid year │
            └──────────┬───────────┘                          └──────────┬───────────┘
                       │ uses existing                                   │ uses existing
                       ▼                                                 ▼
            ┌──────────────────────┐                          ┌──────────────────────┐
            │ ExtensionMethods     │                          │ LoadLibraryHdgmInvoker│
            │ .CheckStringForModel │                          │ (HDGM/, internal)     │
            └──────────────────────┘                          └──────────────────────┘
```

**Key properties:**
- **Discovery-only.** No calculation, no model loading. Just metadata snapshots.
- **Public surface stays minimal.** Four new public types (`ModelDiscovery`, `ModelDiscoveryOptions`, `ScanMode`, `ModelDescriptor`); everything else internal.
- **Cache opt-in.** `UseCache: false` default. Backward-compatible with consumers that just want a one-shot scan.
- **No new dependencies.** Cache uses existing `Newtonsoft.Json`.
- **Side-effects bounded.** Quick mode is filesystem-stat-only. Full mode opens text files (read-only) and may LoadLibraryEx HDGM `.dlls` (briefly file-locks them for ~50–100ms each).

## 4. New and modified components

### New files (production)

| File | Visibility | Purpose |
|---|---|---|
| `src/GeoMagSharp/Discovery/ModelDiscovery.cs` | public static | Entry point. `DiscoverModels(folder)`, `DiscoverModels(folder, opts)`, `DescribeFile(path)` |
| `src/GeoMagSharp/Discovery/ModelDiscoveryOptions.cs` | public | Mutable options object |
| `src/GeoMagSharp/Discovery/ScanMode.cs` | public enum | `Quick`, `Full` |
| `src/GeoMagSharp/Discovery/ModelDescriptor.cs` | public sealed | Immutable read-only properties |
| `src/GeoMagSharp/Discovery/ModelHeaderInspector.cs` | internal | COF/DAT first-line peek |
| `src/GeoMagSharp/Discovery/HdgmDateProbe.cs` | internal | HDGM probe via `LoadLibraryHdgmInvoker` + `hdgmcalc` |
| `src/GeoMagSharp/Discovery/ModelDiscoveryCache.cs` | internal | Atomic JSON read/write of `.models.json` |
| `src/GeoMagSharp/Discovery/ModelDiscoveryCacheEntry.cs` | internal | DTO: file path, size, mtime, descriptor |

### New files (tests)

| File | Coverage |
|---|---|
| `tests/GeoMagSharp.Tests/Discovery/ModelDescriptorTests.cs` | Immutability, constructor, ToString — ~6 tests |
| `tests/GeoMagSharp.Tests/Discovery/ModelHeaderInspectorTests.cs` | COF/DAT type+year extraction, malformed/empty/locked files — ~8 tests |
| `tests/GeoMagSharp.Tests/Discovery/HdgmDateProbeTests.cs` | Filename year extraction, FakeHdgmInvoker-driven probe scenarios — ~6 tests |
| `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryCacheTests.cs` | Round-trip, atomic write, corrupt JSON, schema mismatch, read-only folder — ~10 tests |
| `tests/GeoMagSharp.Tests/Discovery/ModelDiscoveryTests.cs` | End-to-end functional: Quick/Full/Recursive/UseCache/Cancellation/error paths — ~17 tests |
| `tests/GeoMagSharp.Tests/Discovery/HdgmDateProbeIntegrationTests.cs` | env-var-gated real-DLL probe — ~3 tests |
| `tests/GeoMagSharp.Tests/Discovery/TestFolderFixture.cs` | IDisposable temp-folder helper |
| `tests/GeoMagSharp.Tests/Discovery/Fixtures/*.{COF,DAT,txt}` | Small representative files used by functional tests |

### Modified files (production)

| File | Change |
|---|---|
| `Directory.Build.props` | Bump `<VersionPrefix>` from `1.6.0` → `1.7.0` |

### Folder layout

```
src/GeoMagSharp/
├─ Discovery/                          (NEW)
│  ├─ ModelDiscovery.cs                (public)
│  ├─ ModelDiscoveryOptions.cs         (public)
│  ├─ ScanMode.cs                      (public)
│  ├─ ModelDescriptor.cs               (public)
│  ├─ ModelHeaderInspector.cs          (internal)
│  ├─ HdgmDateProbe.cs                 (internal)
│  ├─ ModelDiscoveryCache.cs           (internal)
│  └─ ModelDiscoveryCacheEntry.cs      (internal)
├─ HDGM/                               (existing, unchanged)
├─ Models/                             (existing, unchanged)
├─ Enums/                              (existing, unchanged)
├─ Calculator.cs                       (unchanged)
├─ ModelReader.cs                      (unchanged)
└─ ...
```

### What's not touched

- `Calculator.cs`, `ModelReader.cs`, `MagneticModel.cs`, `MagneticModelSet.cs`, `GeoMag.cs`, `ExtensionMethods.cs` — all unchanged
- `LoadLibraryHdgmInvoker` — reused for the date probe, not modified
- All existing tests — run unchanged

## 5. Public API surface

### `ModelDescriptor` (immutable)

```csharp
namespace GeoMagSharp
{
    public sealed class ModelDescriptor
    {
        public ModelDescriptor(
            string filePath,
            knownModels detectedType,
            string displayName,
            double? minDate,
            double? maxDate,
            string description = null);

        public string FilePath { get; }
        public knownModels DetectedType { get; }
        public string DisplayName { get; }
        public double? MinDate { get; }
        public double? MaxDate { get; }
        public string Description { get; }

        public override string ToString();
    }
}
```

`DetectedType` is `NONE` for files where Quick mode skipped header peek, or where header was unparseable.
`MinDate`/`MaxDate` are null when unknown (Quick mode for COF/DAT, or HDGM probe failure).

### `ModelDiscoveryOptions` (mutable, builder-style)

```csharp
namespace GeoMagSharp
{
    public class ModelDiscoveryOptions
    {
        public ScanMode Mode { get; set; } = ScanMode.Full;
        public bool Recursive { get; set; } = false;
        public bool UseCache { get; set; } = false;
        public string CacheFileName { get; set; } = ".models.json";
        public CancellationToken CancellationToken { get; set; }
        public Action<string, Exception> OnError { get; set; }   // default null = silent
    }
}
```

### `ScanMode`

```csharp
namespace GeoMagSharp
{
    public enum ScanMode
    {
        Quick,
        Full
    }
}
```

### `ModelDiscovery`

```csharp
namespace GeoMagSharp
{
    public static class ModelDiscovery
    {
        public static IEnumerable<ModelDescriptor> DiscoverModels(string folderPath);
        public static IEnumerable<ModelDescriptor> DiscoverModels(string folderPath, ModelDiscoveryOptions options);
        public static ModelDescriptor DescribeFile(string filePath);
    }
}
```

### Sample call sites

**One-shot full scan:**
```csharp
foreach (var d in ModelDiscovery.DiscoverModels("./coefficients"))
    Console.WriteLine($"{d.DisplayName} {d.MinDate}..{d.MaxDate}");
```

**App startup with caching:**
```csharp
var opts = new ModelDiscoveryOptions
{
    Mode = ScanMode.Full,
    UseCache = true,
    CancellationToken = startupToken,
    OnError = (path, ex) => logger.LogWarning("Discovery skipped {Path}: {Error}", path, ex.Message)
};
var models = ModelDiscovery.DiscoverModels(ModelFolder, opts).ToList();
```

**Quick scan + incremental deep-scan for new files:**
```csharp
var quickOpts = new ModelDiscoveryOptions { Mode = ScanMode.Quick };
var currentPaths = ModelDiscovery.DiscoverModels(folder, quickOpts).Select(d => d.FilePath).ToHashSet();
foreach (var p in currentPaths.Except(_knownPaths))
{
    var descriptor = ModelDiscovery.DescribeFile(p);
    if (descriptor != null) AddToCollection(descriptor);
}
```

## 6. Data flow

### Scenario A — `ScanMode.Quick`, no cache

```
DiscoverModels(folder, { Mode = Quick })
  ├─ Validate folder exists; if not → return empty
  └─ for each file in EnumerateFiles(folder):
       ├─ cancellationToken.ThrowIfCancellationRequested()
       ├─ extension classify:
       │    .COF / .DAT  → DetectedType = NONE (not peeked), name = filename, dates null
       │    .DLL + ModelPathDetector.IsHdgmPath → DetectedType = HDGM, dates null
       │    other        → skip
       └─ yield ModelDescriptor

Cost: <10ms for 100 files on local SSD.
```

### Scenario B — `ScanMode.Full`, no cache

```
DiscoverModels(folder, { Mode = Full })
  └─ for each file in EnumerateFiles(folder):
       ├─ cancellationToken.ThrowIfCancellationRequested()
       │
       ├─ COF/DAT branch:
       │    ├─ ModelHeaderInspector.Inspect(path):
       │    │    Open file (read+share); read first line; CheckStringForModel; extract year
       │    ├─ DetectedType = WMM | IGRF | EMM | DGRF | WMMHR | NONE
       │    ├─ DisplayName from header
       │    ├─ MinDate = parsed year, MaxDate = MinDate + 5
       │    └─ yield
       │
       ├─ DLL+IsHdgmPath branch:
       │    ├─ HdgmDateProbe.Probe(path):
       │    │    extract year from filename (or current year)
       │    │    using LoadLibraryHdgmInvoker, probe forward up to 8 years
       │    │    catch all native errors → return (null, null)
       │    ├─ DetectedType = HDGM
       │    ├─ DisplayName = filename-derived
       │    ├─ (MinDate, MaxDate) = probe result (or null,null on failure)
       │    └─ yield
       │
       └─ skip other extensions

Cost for 5 COF + 1 HDGM DLL: ~50–150ms.
```

### Scenario C — `ScanMode.Full` + `UseCache: true`

```
DiscoverModels(folder, { Mode = Full, UseCache = true })
  ├─ cachePath = Path.Combine(folder, ".models.json")
  ├─ cached = ModelDiscoveryCache.TryLoad(cachePath)  // empty if missing/corrupt
  ├─ liveEntries = []
  ├─ for each file in EnumerateFiles(folder):
  │    ├─ skip files matching cache filename
  │    ├─ ThrowIfCancellationRequested()
  │    ├─ if cached entry exists AND mtime AND size match:
  │    │    yield cached.descriptor; add to liveEntries (cache HIT)
  │    └─ else:
  │         deep-scan via Scenario B per-file logic (cache MISS)
  │         yield; add to liveEntries
  └─ ModelDiscoveryCache.Save(cachePath, liveEntries):
       Serialize → write {cachePath}.tmp → atomic rename to cachePath
       On IO failure: invoke OnError, don't fail discovery

Cost (warm cache, no changes): ~5–15ms.
Cost (one HDGM DLL changed): warm + ~50–100ms re-probe.
```

### Scenario D — `DescribeFile(path)` for incremental deep-scan

```
DescribeFile(path)
  ├─ if !File.Exists(path) → throw GeoMagExceptionFileNotFound
  ├─ same per-file logic as Scenario B
  └─ return descriptor (or null for unrecognized extension)
```

### Cross-cutting concerns

**Cancellation:** `ThrowIfCancellationRequested()` once per file inside the iterator. HDGM probe doesn't check between sub-probes (8 calls × microseconds = sub-millisecond, no benefit).

**Recursive flag:** `SearchOption.AllDirectories` vs `TopDirectoryOnly`. One-line change. Cache validation walks the same recursion.

**Folder doesn't exist:** Return empty enumerable. No exception.

**Cache file in scanned folder:** Filtered out of the iterator so we don't try to classify our own metadata.

**Concurrent processes:** Read is non-locking. Write is `temp + rename` atomic. Worst case stale cache; never partial.

## 7. Error handling

### Failure modes

| Scenario | Where | Behavior |
|---|---|---|
| `folderPath` is null | `DiscoverModels` entry | `ArgumentNullException` |
| `options` is null | `DiscoverModels(folder, opts)` | `ArgumentNullException` |
| `folderPath` doesn't exist | `DiscoverModels` | Returns empty enumerable |
| `folderPath` exists, no model files | `DiscoverModels` | Returns empty enumerable |
| Folder access denied | `DiscoverModels` | Returns empty + invokes `OnError` |
| `filePath` null in `DescribeFile` | `DescribeFile` | `ArgumentNullException` |
| `filePath` doesn't exist | `DescribeFile` | `GeoMagExceptionFileNotFound` |
| Individual COF/DAT unreadable / locked | per-file inspection | Skip + `OnError`, continue iteration |
| COF/DAT first line malformed | `ModelHeaderInspector` | Yield with `DetectedType = NONE`, dates null |
| HDGM `LoadLibraryEx` fails (bitness / AV / corrupt) | `HdgmDateProbe` | Catch → return `(null, null)`; descriptor still yielded with `DetectedType = HDGM`, dates null |
| HDGM DLL loads but `hdgmcalc` symbol missing | `HdgmDateProbe` | Same — null dates, descriptor still has `DetectedType = HDGM` |
| HDGM probe sentinel for every probe year | `HdgmDateProbe` | Returns `(null, null)` |
| Cache file missing | `ModelDiscoveryCache.TryLoad` | Returns empty list |
| Cache file corrupt JSON | `ModelDiscoveryCache.TryLoad` | Catch `JsonReaderException` → empty list, `OnError`, fresh scan rewrites |
| Cache file wrong `schemaVersion` | `ModelDiscoveryCache.TryLoad` | Treat as no cache, `OnError` |
| Cache write fails | `ModelDiscoveryCache.Save` | Catch IOException → `OnError`, **discovery still succeeds** |
| Cache references missing file | validation | Skip the entry; don't yield |
| Concurrent write race | atomic rename | NTFS rename atomic; later writer wins |
| File mtime+size match cache but content actually changed | validation | False cache hit (acceptable; mtime+size is the standard contract) |
| `CancellationToken` triggered mid-scan | per-file iterator | `OperationCanceledException` propagates; cache **not** written (atomic-or-nothing) |
| Folder is a symlink / junction | enumerator | .NET defaults handle cycle protection |
| File deleted between enumeration and inspection (TOCTOU) | per-file inspection | Catch `FileNotFoundException` → skip silently |
| File matches multiple format heuristics (e.g. `hdgm.cof`) | classifier | `.cof` → COF reader. `.dll`+contains-"hdgm" → HDGM. Each file resolves to exactly one type by extension precedence. |

### Logging policy

The library has no logging dependency. Per-file failures use the `OnError(string filePath, Exception)` callback on `ModelDiscoveryOptions`. Default callback is null (silent). Consumers wire up their own logger.

## 8. Testing strategy

### Test pyramid

```
                    ┌──────────────────────────────────────┐
                    │  Integration (env-var-gated)         │  ~3 tests
                    └──────────────────────────────────────┘
                  ┌──────────────────────────────────────────┐
                  │  Functional (real File I/O on fixtures)  │  ~25 tests
                  └──────────────────────────────────────────┘
              ┌────────────────────────────────────────────────┐
              │  Unit (pure logic, FakeHdgmInvoker for probe)   │  ~30 tests
              └────────────────────────────────────────────────┘
```

### Fixtures

`tests/GeoMagSharp.Tests/Discovery/Fixtures/` holds small representative files:

| File | Role |
|---|---|
| `WMM2025_sample.COF` | Valid WMM header |
| `IGRF14_sample.COF` | Valid IGRF header |
| `EMM_sample.COF` | Valid EMM header |
| `corrupt_header.COF` | Unparseable first line |
| `empty.COF` | Zero bytes |
| `notamodel.txt` | Wrong extension; should be skipped |
| `cached.models.json` | Pre-built cache for cache-validation tests |

Tests use `Path.GetTempPath() + Guid.NewGuid()` per-test temp folders, copying fixtures in via `TestFolderFixture` (IDisposable helper).

### Unit tests

**`ModelDescriptorTests.cs`** (~6 tests):
- `Constructor_NullFilePath_Throws`
- `Constructor_NullDisplayName_DefaultsEmpty`
- `Constructor_AllFieldsSet_PropertiesRoundTrip`
- `Properties_HaveNoSetters`
- `ToString_IncludesKeyFields`
- `EqualValuesEqualReferences_NotEqualByDefault`

**`ModelDiscoveryCacheTests.cs`** (~10 tests):
- `Save_ThenLoad_RoundTripsAllEntries`
- `Save_AtomicWrite_NoPartialFileVisibleDuringWrite`
- `Load_MissingFile_ReturnsEmptyList`
- `Load_CorruptJson_ReturnsEmptyList_FiresOnError`
- `Load_WrongSchemaVersion_ReturnsEmptyList`
- `Load_EmptyJsonObject_ReturnsEmptyList`
- `Save_ToReadOnlyFolder_DoesNotThrow_FiresOnError`
- `CacheEntry_FilePathIsRelativeToFolder`
- `CacheEntry_TimestampUsesUtc`
- `Save_PreservesEntryOrder`

**`HdgmDateProbeTests.cs`** (~6 tests, FakeHdgmInvoker-driven):
- `ExtractYearFromFilename_StandardNoaaName_Returns2019`
- `ExtractYearFromFilename_BitnessSuffixOnly_ReturnsNull`
- `ExtractYearFromFilename_NoYear_ReturnsNull`
- `ExtractYearFromFilename_VendorPrefixWithYear_ReturnsYear`
- `Probe_FakeInvokerAlwaysSentinel_ReturnsNullDates`
- `Probe_FakeInvokerValidUntilYearN_ReturnsCorrectMaxDate`

The probe takes an `INativeHdgmInvoker` factory parameter so tests substitute `FakeHdgmInvoker` without `LoadLibraryEx`.

### Functional tests

**`ModelHeaderInspectorTests.cs`** (~8 tests):
- `Inspect_ValidWmmCof_ReturnsWMMTypeAndYear`
- `Inspect_ValidIgrfCof_ReturnsIGRFTypeAndYear`
- `Inspect_ValidEmmCof_ReturnsEMMType`
- `Inspect_CorruptHeader_ReturnsNoneType`
- `Inspect_EmptyFile_ReturnsNoneType`
- `Inspect_LockedFile_PropagatesAsNoneType_FiresOnError`
- `Inspect_FileNotFound_Throws`
- `Inspect_DatExtension_ReturnsParsedType`

**`ModelDiscoveryTests.cs`** (~17 tests):

Quick mode:
- `DiscoverModels_QuickMode_ReturnsCofAsNoneType_WithoutOpeningFile`
- `DiscoverModels_QuickMode_RecognizesHdgmDllByFilename`
- `DiscoverModels_QuickMode_SkipsUnknownExtensions`

Full mode:
- `DiscoverModels_FullMode_PopulatesCofMetadata`
- `DiscoverModels_FullMode_HandlesMixedFolderCorrectly`
- `DiscoverModels_FullMode_DllNotMatchingHdgmRule_Skipped`
- `DiscoverModels_FullMode_CorruptHeader_YieldsNoneTypeNotSkipped`

Recursive:
- `DiscoverModels_Recursive_TraversesSubfolders`
- `DiscoverModels_NonRecursive_StopsAtTopLevel`

Cache:
- `DiscoverModels_UseCache_FirstRun_WritesCacheFile`
- `DiscoverModels_UseCache_SecondRunUnchangedFolder_HitsCache_NoFileOpens`
- `DiscoverModels_UseCache_FileMtimeChanged_RescansThatFile`
- `DiscoverModels_UseCache_NewFileAdded_DeepScansOnlyNewFile`
- `DiscoverModels_UseCache_FileDeleted_DropsFromCache`
- `DiscoverModels_UseCache_CorruptCache_FullScan_RewritesFresh`
- `DiscoverModels_UseCache_CacheFileNotInResults`

Cancellation / errors:
- `DiscoverModels_CancellationTokenTriggered_ThrowsOperationCanceled`
- `DiscoverModels_FolderDoesNotExist_ReturnsEmpty`
- `DiscoverModels_NullFolderPath_Throws`
- `DescribeFile_NewFile_ReturnsFreshDescriptor`
- `DescribeFile_FileNotFound_ThrowsGeoMagExceptionFileNotFound`

### Integration tests (env-var-gated)

`HdgmDateProbeIntegrationTests.cs` — `[TestCategory("RequiresHDGMDll")]`, gated on `HDGM_DLL_PATH`:

- `Integration_RealHdgmDll_Probes_ReturnsValidDateRange`
- `Integration_DiscoverModels_FolderWithRealHdgmDll_ReturnsHdgmDescriptor`
- `Integration_DiscoverModels_TwoConsecutiveCallsWithCache_SecondCallSkipsProbe`

Existing CI filter `--filter "TestCategory!=RequiresHDGMDll"` excludes these.

### Tolerances and assertions

| Assertion type | Approach |
|---|---|
| File metadata equality | Direct equality on `FilePath`, `DisplayName`, `DetectedType` |
| Date range | Exact equality where possible; `Assert.IsNull` for unknown bounds |
| Cache JSON content | Round-trip via deserialize-then-compare; don't assert exact bytes |
| Cache atomicity | Test process forks — write thread + read thread racing; assert read sees old or new, never partial |

## 9. Versioning, packaging, and process

| Item | Resolution |
|---|---|
| Semver bump | **Minor** — v1.6.0 → **v1.7.0** |
| `Directory.Build.props` `VersionPrefix` | Update as first commit on feature branch |
| Target frameworks | Unchanged — `net48` + `netstandard2.0` |
| New NuGet dependencies | None |
| `[InternalsVisibleTo]` directive | Already in place from PR #20; reused for new internal types |
| Ralph Loop requirements | Required per CLAUDE.md: GitHub issue ✅ #21, feature branch ✅ from `development`, `tasks.md` ✅ at `docs/features/model-discovery-api/tasks.md`, draft PR opened with this commit, rotating-persona Ralph Loop with 2 clean cycles before merge |
| README update | Brief mention of `ModelDiscovery.DiscoverModels` in supported-API list |
| CLAUDE.md update | Note discovery API in Project Overview |

## 10. Deferred follow-ups (separate PRs)

| Follow-up | Brief |
|---|---|
| GeoMagSharpGUI consumer migration | Replace local folder scanner with `ModelDiscovery.DiscoverModels(folder, new { UseCache = true })`. Filter resource constraint relaxed to extension-agnostic. |
| FileSystemWatcher integration | Auto-refresh discovery when folder contents change. |
| Async API | `IAsyncEnumerable<ModelDescriptor>` once GeoMagSharp targets net standard 2.1. |
| Authenticode validation | Verify NOAA-signed HDGM DLLs before probe. |
| BGGM2019+ detection | When sample available; new file format support. |

## 11. References

- HDGM design: `docs/superpowers/specs/2026-04-26-hdgm-support-design.md` Section 11 (where this follow-up was first noted)
- HDGM PR #20 — merged to development as `ac9b183`
- GUI PR #56 — current filter approach this design eventually replaces
- Existing `LoadLibraryHdgmInvoker` — reused for the date probe
- Existing `ExtensionMethods.CheckStringForModel` — reused for header peek
- ISCWSA Rev5.13 model categories (referenced for model-type knowledge)
