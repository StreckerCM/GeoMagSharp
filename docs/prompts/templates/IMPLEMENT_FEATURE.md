# Feature Implementation Prompt Template

Copy and customize this template for implementing features with a single persona.

---

## Template

```
Using the IMPLEMENTER persona from docs/prompts/PERSONAS.md:

## Task
Implement Feature [FEATURE_NAME] for GeoMagSharp.

## Reference Documents
- Specification: docs/features/[FOLDER]/spec.md
- Implementation Plan: docs/features/[FOLDER]/plan.md
- Task Checklist: docs/features/[FOLDER]/tasks.md

## Instructions
1. Read all reference documents first
2. Follow the implementation plan phases in order
3. Check off tasks in tasks.md as you complete them using `[x]`
4. Run `dotnet build -c Release && dotnet test -c Release` after significant changes
5. Commit logical chunks with descriptive messages
6. If blocked, document the issue in tasks.md and continue with other tasks

## Success Criteria
- All tasks in tasks.md are checked `[x]`
- `dotnet build -c Release` succeeds
- `dotnet test -c Release` passes
- No new compiler warnings
- Code follows existing project patterns (see CLAUDE.md)

## Completion
When all success criteria are met, output:
<promise>FEATURE COMPLETE</promise>
```

---

## Example: New Model Format Support

```bash
/ralph-loop "Using the IMPLEMENTER persona from docs/prompts/PERSONAS.md:

## Task
Add support for SHC (Spherical Harmonic Coefficient) file format to ModelReader.

## Instructions
1. Study existing COF and DAT readers in ModelReader.cs
2. Implement SHCreader() following same patterns
3. Add SHC to knownModels enum
4. Update CheckStringForModel() extension method
5. Test with sample SHC file
6. Run dotnet build -c Release && dotnet test -c Release
7. Add unit test for SHC parsing

## Success Criteria
- SHC files load correctly via ModelReader.Read()
- Coefficients match reference values
- Build succeeds with no warnings
- Unit test for SHC parsing passes

## Completion
When all success criteria are met, output:
<promise>FEATURE COMPLETE</promise>" --completion-promise "FEATURE COMPLETE" --max-iterations 20
```
