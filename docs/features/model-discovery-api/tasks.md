# Feature: Model Discovery API

Issue: #21
Branch: feature/21-model-discovery-api
Design: docs/superpowers/specs/2026-04-28-discovery-api-design.md

## Tasks

Tasks below are derived from the approved design. The detailed implementation
plan (sequencing, dependencies, test ordering) will be produced via the
writing-plans skill in a separate step before any code is written.

### Public types
- [ ] Define `ScanMode` enum (Quick, Full)
- [ ] Define `ModelDescriptor` immutable sealed class
- [ ] Define `ModelDiscoveryOptions` mutable options class with defaults
- [ ] Define `ModelDiscovery` static class skeleton with three method signatures

### Internal helpers
- [ ] Implement `ModelHeaderInspector` (open file, read first line, classify via `CheckStringForModel`, extract year)
- [ ] Implement `HdgmDateProbe` with filename year extraction and forward-probe loop
- [ ] Implement `ModelDiscoveryCache` read/write with atomic temp+rename
- [ ] Implement `ModelDiscoveryCacheEntry` DTO with size+mtime+descriptor
- [ ] Make `HdgmDateProbe` accept an `INativeHdgmInvoker` factory parameter for testability

### Discovery orchestration
- [ ] `DiscoverModels(folder)` convenience overload
- [ ] `DiscoverModels(folder, options)` main implementation: enumerate, classify, yield
- [ ] `DescribeFile(path)` single-file inspection
- [ ] Quick-mode classification (filename only)
- [ ] Full-mode classification (header peek + HDGM probe)
- [ ] Cache load → validate → yield → write flow when `UseCache: true`
- [ ] Filter cache filename out of enumeration results
- [ ] Recursive support via `SearchOption.AllDirectories`
- [ ] CancellationToken checks once per file
- [ ] OnError callback invocation for non-fatal errors

### Result-shape extensions
- [ ] None required — discovery is identification-only

### Lifetime
- [ ] None required — `ModelDiscovery` is static; descriptors are value snapshots

### Cleanup
- [ ] Bump `Directory.Build.props` `VersionPrefix` 1.6.0 → 1.7.0

### Tests — unit (CI)
- [ ] `ModelDescriptorTests` (~6 cases)
- [ ] `ModelDiscoveryCacheTests` (~10 cases including atomic write race)
- [ ] `HdgmDateProbeTests` (~6 cases via `FakeHdgmInvoker`)

### Tests — functional (CI, real File I/O on fixtures)
- [ ] `TestFolderFixture` IDisposable helper
- [ ] Fixture files: `WMM2025_sample.COF`, `IGRF14_sample.COF`, `EMM_sample.COF`, `corrupt_header.COF`, `empty.COF`, `notamodel.txt`, `cached.models.json`
- [ ] `ModelHeaderInspectorTests` (~8 cases)
- [ ] `ModelDiscoveryTests` (~17 cases covering Quick/Full/Recursive/UseCache/Cancellation/error paths)

### Tests — integration (env-var-gated)
- [ ] `HdgmDateProbeIntegrationTests` skeleton with `Assert.Inconclusive` skip if `HDGM_DLL_PATH` unset
- [ ] Real-DLL probe round-trip
- [ ] Real-DLL discovery round-trip
- [ ] Cache prevents re-probe assertion

### Documentation
- [ ] Update `README.md` — mention `ModelDiscovery` API in supported-API list with example
- [ ] Update `CLAUDE.md` Project Overview — note discovery API
- [ ] Inline XML doc on every public type and member

### Build / project file
- [ ] Verify multi-target build still passes (net48 + netstandard2.0)
- [ ] Verify NuGet pack produces `GeoMagSharp.1.7.0.nupkg` cleanly

## Completion Criteria

- [ ] All tasks above checked
- [ ] Build succeeds (`dotnet build -c Release`) for both target frameworks
- [ ] All unit tests pass (`dotnet test --filter "TestCategory!=RequiresHDGMDll"`)
- [ ] Integration tests pass locally with `HDGM_DLL_PATH` env var set (manual maintainer verification)
- [ ] Existing GeoMag, ModelReader, Calculator, MagneticModelSet behavior unchanged (zero diffs to those files)
- [ ] 2 clean Ralph Loop cycles (all 6 personas find no issues twice)
