# Rotating Persona Feature Implementation

This template uses rotating personas that cycle through different perspectives on each iteration, ensuring comprehensive review from multiple angles.

## How It Works

Each iteration, the persona changes based on `iteration % N`:
- Iteration 0, 6, 12... → Persona 0
- Iteration 1, 7, 13... → Persona 1
- And so on...

This ensures every perspective reviews the work multiple times.

---

## Standard 6-Persona Rotation

Best for most feature implementations:

```bash
/ralph-loop "
Feature: [FEATURE_NUMBER]-[feature-name]
Branch: feature/[FEATURE_NUMBER]-[feature-name]

PHASE 1 - TASK COMPLETION:
- Read docs/features/[FEATURE_FOLDER]/tasks.md
- If any tasks unchecked (- [ ]), complete them first
- Mark completed tasks (- [x]) as you finish them
- Run: dotnet build -c Release && dotnet test -c Release

PHASE 2 - ROTATING PERSONA REVIEW (cycle each iteration):
Current Persona = ITERATION MOD 6:

[0] #5 IMPLEMENTER:
- Check tasks.md for incomplete items
- Implement next unchecked task
- Follow existing code patterns
- Run build after changes

[1] #9 CODE REVIEWER:
- Review recent changes for bugs, edge cases
- Check error handling and null safety
- Verify code follows project patterns
- Fix any issues found

[2] #7 TESTER:
- Run: dotnet test -c Release --verbosity normal
- Check test coverage for new code
- Write missing unit tests
- Verify edge cases are covered

[3] #3 API_DESIGNER:
- Review public API surface changes
- Check XML documentation on public members
- Verify multi-target compatibility (net48 + netstandard2.0)
- Ensure backward compatibility

[4] #10 SECURITY_AUDITOR:
- Check for hardcoded values that should be config
- Verify input validation for coordinates
- Look for potential security issues
- Review any new file I/O code

[5] #2 PROJECT_MANAGER:
- Verify all tasks in tasks.md are checked
- Check requirements are met
- Document any gaps or issues found
- Update tasks.md if new work discovered

EACH ITERATION:
1. Identify current persona (Iteration % 6)
2. Perform that persona's review/work
3. Make improvements or fixes as needed
4. Commit changes with message: '[persona] description'
5. Post PR comment with findings/changes
6. If all tasks complete AND no issues found by ANY persona for 2 full cycles (12 iterations), output completion

OUTPUT <promise>FEATURE COMPLETE</promise> when:
- All tasks in tasks.md are checked [x]
- Build succeeds with no errors
- All personas report no issues for 2 consecutive cycles
" --completion-promise "FEATURE COMPLETE" --max-iterations 30
```

---

## Compact 4-Persona Rotation (Faster)

For simpler features or when speed is preferred:

```bash
/ralph-loop "
Feature: [FEATURE_NUMBER]-[feature-name]

ROTATING PERSONA (ITERATION MOD 4):

[0] #5 IMPLEMENTER: Complete next task from tasks.md, run build
[1] #9 REVIEWER: Review code for bugs/issues, fix problems
[2] #7 TESTER: Verify functionality, add tests if needed
[3] #2 PROJECT_MANAGER: Check all requirements met, update tasks.md

EACH ITERATION:
1. Run current persona's checks
2. Make one fix/improvement
3. Commit: '[persona] description'
4. Post PR comment with findings/changes

OUTPUT <promise>DONE</promise> when all tasks complete and 2 clean cycles.
" --completion-promise "DONE" --max-iterations 20
```

---

## Full 11-Persona Rotation (Comprehensive)

For critical features requiring maximum scrutiny:

```bash
/ralph-loop "
Feature: [FEATURE_NUMBER]-[feature-name]

ROTATING PERSONA (ITERATION MOD 11):

[0] #1 BUSINESS_ANALYST: Verify requirements clarity, check acceptance criteria
[1] #2 PROJECT_MANAGER: Check progress, identify blockers, update tasks
[2] #3 API_DESIGNER: Review public API design, XML docs, compatibility
[3] #4 COMPATIBILITY_REVIEWER: Check multi-target build, cross-framework APIs
[4] #5 IMPLEMENTER: Complete next task, follow patterns
[5] #6 REFACTORER: Clean up code, improve organization
[6] #7 TESTER: Run tests, add coverage, verify edge cases
[7] #8 DEBUGGER: Look for potential bugs, add defensive code
[8] #9 REVIEWER: Full code review, check quality
[9] #10 SECURITY_AUDITOR: Security review, check for vulnerabilities
[10] #11 DOCUMENTER: Update comments, check documentation

OUTPUT <promise>FEATURE COMPLETE</promise> when all tasks done and clean cycle.
" --completion-promise "FEATURE COMPLETE" --max-iterations 44
```

---

## Commit Message Format

Each commit should indicate which persona made the change:

```
[IMPLEMENTER] Add new coefficient file parser
[REVIEWER] Fix array bounds check in ModelReader
[TESTER] Add unit test for async model reading
[API_DESIGNER] Add XML docs to MagneticCalculations
[SECURITY_AUDITOR] Add input validation for model paths
[PROJECT_MANAGER] Mark parsing tasks complete
```

---

## PR Comment Format

Each persona should post a comment to the PR summarizing their findings and changes.

```markdown
## [PERSONA] Review - Iteration N

### Summary
[Brief description of what was reviewed/implemented]

### Findings
- [Finding 1 - issue found or observation]
- [Finding 2]

### Changes Made
- [Change 1 - file:line - description]
- [Change 2]

### Status
- [ ] Issues found requiring follow-up
- [x] Clean pass - no issues found
```
