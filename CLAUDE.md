# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GeoMagSharp is a C# library for geomagnetic field calculations using spherical harmonic models. It is a port of GeoMag 7.0 (NOAA) and supports WMM, WMMHR, IGRF, EMM, and BGGM models for computing magnetic declination, inclination, and field intensity.

**Tech Stack:** .NET multi-target library (net48 + netstandard2.0), SDK-style csproj, NuGet package

## Build Commands

```bash
# Restore dependencies
dotnet restore

# Debug build
dotnet build -c Debug

# Release build
dotnet build -c Release

# Run unit tests
dotnet test -c Release --verbosity normal

# Create NuGet package
dotnet pack src/GeoMagSharp/GeoMagSharp.csproj -c Release -o artifacts
```

## Branching Strategy

```
master ←──────── Stable releases
  ↑
  │ PR merge
  │
preview ←─────── Pre-release testing and development
  ↑
  │ PR merge
  │
feature/* ─────── Feature development work
```

### Branch Guidelines

| Branch | Purpose | Description |
|--------|---------|-------------|
| `master` | Production releases | Stable release builds, NuGet publishes |
| `preview` | Development | Integration testing before release |
| `feature/*` | Feature work | Development branches for new features |

### Workflow
1. Create feature branches from `preview`
2. PR feature branches to `preview` for integration
3. PR `preview` to `master` for releases
4. Tag `master` with `vX.Y.Z` to trigger NuGet publish

### Branch Protection Rules — NEVER VIOLATE

- **NEVER commit directly to `master`.** All changes to `master` must come through reviewed and approved PRs from `preview`.
- **NEVER commit directly to `preview`.** All changes to `preview` must come through PRs from `feature/*` branches.
- **NEVER push directly to protected branches.** No force-pushes, no direct commits, no exceptions.
- **NEVER create or merge a PR without explicit user confirmation.** Always ask the user before creating a PR and before merging one. Draft PRs are acceptable without confirmation, but converting to ready-for-review or merging requires approval.
- **All development work happens on `feature/*` branches.** This is the only place where direct commits are allowed.

## Architecture

### Solution Structure

- **GeoMagSharp** (`src/GeoMagSharp/`) - Core calculation library (net48 + netstandard2.0)
- **GeoMagSharp.Tests** (`tests/GeoMagSharp.Tests/`) - MSTest unit tests (net48)

### Key Source Files

| File | Purpose |
|------|--------|
| `src/GeoMagSharp/GeoMag.cs` | Main calculation orchestrator |
| `src/GeoMagSharp/Calculator.cs` | Spherical harmonic calculations |
| `src/GeoMagSharp/ModelReader.cs` | Coefficient file parser (.COF, .DAT) |
| `src/GeoMagSharp/Units.cs` | Unit conversion utilities |
| `src/GeoMagSharp/GeoConstants.cs` | Constants and limits |
| `src/GeoMagSharp/ExtensionMethods.cs` | DateTime/decimal date extensions |

### Model Classes

| File | Purpose |
|------|--------|
| `src/GeoMagSharp/Models/Magnetic/MagneticModelSet.cs` | Single magnetic model set |
| `src/GeoMagSharp/Models/Magnetic/MagneticModelCollection.cs` | Model collection with JSON serialization |
| `src/GeoMagSharp/Models/Configuration/CalculationOptions.cs` | Calculation input configuration |
| `src/GeoMagSharp/Models/Results/MagneticCalculations.cs` | Calculation output results |
| `src/GeoMagSharp/Models/Coordinates/Coordinate.cs` | Coordinate base class |
| `src/GeoMagSharp/Models/Progress/CalculationProgressInfo.cs` | Async progress reporting |

### Data Directories

| Directory | Purpose |
|-----------|--------|
| `coefficient/` | Bundled magnetic model files (.COF) |
| `tests/GeoMagSharp.Tests/TestData/` | Test coefficient files |

### Supported Magnetic Models

- **WMM** (World Magnetic Model)
- **WMMHR** (WMM High Resolution)
- **IGRF** (International Geomagnetic Reference Field)
- **DGRF** (Definitive Geomagnetic Reference Field)
- **EMM** (Enhanced Magnetic Model)
- **BGGM** (BGS Global Geomagnetic Model)

## Development Workflow

### MANDATORY: Ralph Loop for ALL Feature Work

**This is NON-NEGOTIABLE.** Every feature branch (`feature/*`) MUST use the Ralph Wiggum loop (`/ralph-loop`) with rotating personas. There are NO exceptions to this rule, regardless of how simple the task appears.

**Before writing ANY code on a feature branch, you MUST:**

1. Verify a `docs/features/<feature>/tasks.md` file exists
2. If it doesn't exist, create one before proceeding
3. Start a Ralph Loop with the rotating persona pattern

**If you find yourself on a feature branch writing code without an active Ralph Loop, STOP and start one.**

### Step 1: Create a GitHub Issue (if one doesn't exist)

Every feature must have a corresponding GitHub issue before work begins.

### Step 2: Create and Switch to a Feature Branch

```bash
git checkout preview
git pull origin preview
git checkout -b feature/<issue-number>-<short-description>
```

### Step 3: Create or Verify tasks.md (GATE - Required Before Any Code)

Every feature MUST have a `docs/features/<feature>/tasks.md` file. This file is the **single source of truth** for what work needs to be done. No code should be written until this file exists and has been reviewed.

**tasks.md format:**
```markdown
# Feature: <Feature Name>
Issue: #<issue-number>
Branch: feature/<issue-number>-<short-description>

## Tasks
- [ ] Task 1 description
- [ ] Task 2 description
- [ ] Task 3 description

## Completion Criteria
- [ ] All tasks checked
- [ ] Build succeeds
- [ ] Tests pass
- [ ] 2 clean Ralph Loop cycles
```

### Step 4: Start a Ralph Loop (MANDATORY)

Use the Ralph Wiggum loop with the rotating persona pattern defined in `docs/prompts/PERSONAS.md`. See the "Ralph Loop / Iterative Development" section below for the full pattern and completion criteria.

## Key Patterns

- Pure calculation library — no UI code
- JSON configuration via Newtonsoft.Json
- Coefficient files in fixed 80-character record format
- Use existing `ExtensionMethods` utilities
- Multi-target: `net48` and `netstandard2.0`
- Async API with `IProgress<T>` and `CancellationToken` support

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Types | PascalCase | `MagneticModelSet` |
| Methods | PascalCase | `SpotCalculation()` |
| Private fields | _camelCase | `_modelCollection` |
| Parameters | camelCase | `latitude` |
| Test methods | Method_Scenario_Expected | `Read_ValidCOFFile_ReturnsModel` |

## Platform Constraints

- Must compile for both .NET Framework 4.8 and .NET Standard 2.0
- No platform-specific APIs (no WinForms, no WPF, no System.Device)
- NuGet package must work in .NET Framework 4.8, .NET Core 2.0+, and .NET 5+ consumers

## Dependencies

| Package | Purpose |
|---------|---------|
| Newtonsoft.Json 13.0.3 | JSON serialization |
| System.Data.DataSetExtensions 4.5.0 | DataTable support (netstandard2.0 only) |
| Microsoft.SourceLink.GitHub 8.0.0 | Source Link debugging |

## Ralph Loop / Iterative Development

**MANDATORY — NO EXCEPTIONS:** ALL feature branch work (`feature/*`) MUST use Ralph loops with rotating personas. This applies regardless of feature size, complexity, or urgency. Skipping the Ralph Loop is never acceptable.

### Pre-Flight Checklist (Before Starting Ralph Loop)

Before starting any Ralph Loop, verify:

- [ ] GitHub issue exists for this feature
- [ ] Feature branch created from `preview`
- [ ] `docs/features/<feature>/tasks.md` exists with task breakdown
- [ ] PR created (draft is fine) to track work

If any of these are missing, create them first. **Do NOT start coding without tasks.md.**

### Required Persona Rotation

```
Iteration % 6 determines the current persona:

[0] #5 IMPLEMENTER (sonnet)   - Complete tasks, write code
[1] #9 REVIEWER (opus)        - Review for bugs, code quality
[2] #7 TESTER (sonnet)        - Verify functionality, add tests
[3] #3 API_DESIGNER (sonnet)  - Review public API surface, usability
[4] #10 SECURITY (opus)       - Security review, input validation
[5] #2 PROJECT_MGR (haiku)    - Check requirements, update tasks
```

### Each Iteration Must:
1. Identify the current persona based on iteration number
2. Follow that persona's mindset and output format from `docs/prompts/PERSONAS.md`
3. Commit with persona prefix: `[IMPLEMENTER]`, `[REVIEWER]`, etc.
4. Reference the feature's `tasks.md` file and mark tasks complete
5. Post a PR comment summarizing findings and changes

### Completion Criteria
- All tasks in `docs/features/[feature]/tasks.md` marked complete
- Build succeeds with no errors
- Tests pass
- **2 clean cycles** (all 6 personas find no issues twice)

### Why This Matters

The Ralph Loop ensures:
- Multiple perspectives review every change (code quality, security, API design, testing)
- Issues are caught early through systematic rotation
- Progress is tracked via tasks.md
- An audit trail exists via PR comments from each persona
- Features meet a consistent quality bar before merging

See `docs/prompts/README.md` and `docs/prompts/templates/ROTATING_FEATURE.md` for full documentation.
