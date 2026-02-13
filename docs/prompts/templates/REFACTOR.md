# Refactoring Prompt Template

Copy and customize this template for code refactoring tasks.

---

## Template

```
Using the REFACTORER persona from docs/prompts/PERSONAS.md:

## Refactoring Goal
[What improvement you want to achieve]

## Scope
[Files/classes affected]

## Constraints
- Preserve existing behavior exactly
- Maintain backward compatibility
- No functional changes

## Instructions
1. Understand current implementation thoroughly
2. Make small, incremental changes
3. Run tests after each change
4. Commit frequently with descriptive messages
5. Document any API changes for downstream users

## Success Criteria
- All existing tests still pass
- Build succeeds with no warnings
- Code is cleaner/better organized
- No functional changes introduced

## Completion
When all success criteria are met, output:
<promise>REFACTOR COMPLETE</promise>
```

---

## Example: Extract Helper Methods

```bash
/ralph-loop "Using the REFACTORER persona from docs/prompts/PERSONAS.md:

## Refactoring Goal
Extract common coordinate validation logic into shared helper methods.

## Scope
- src/GeoMagSharp/Models/Coordinates/Latitude.cs
- src/GeoMagSharp/Models/Coordinates/Longitude.cs
- src/GeoMagSharp/Models/Coordinates/Coordinate.cs

## Constraints
- Preserve all existing behavior
- Keep backward compatibility (public API unchanged)
- No functional changes

## Instructions
1. Run tests to establish baseline
2. Identify duplicated validation logic
3. Extract shared methods to base Coordinate class
4. Build and test after each change
5. Commit each successful extraction

## Success Criteria
- All tests pass
- Build succeeds
- Validation logic is DRY
- No functional changes

## Completion
When all success criteria are met, output:
<promise>REFACTOR COMPLETE</promise>" --completion-promise "REFACTOR COMPLETE" --max-iterations 20
```
