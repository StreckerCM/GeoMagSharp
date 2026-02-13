# Feature Documentation

Each feature gets its own subdirectory under `docs/features/` containing:

| File | Purpose |
|------|---------|
| `tasks.md` | **Required** - Task checklist (single source of truth) |
| `spec.md` | Optional - Detailed specification |
| `plan.md` | Optional - Implementation plan |

## Creating a New Feature

1. Create a GitHub issue
2. Create a feature branch: `feature/<issue-number>-<short-description>`
3. Create `docs/features/<feature>/tasks.md`
4. Start a Ralph Loop (see `docs/prompts/`)

## tasks.md Format

```markdown
# Feature: <Feature Name>
Issue: #<issue-number>
Branch: feature/<issue-number>-<short-description>

## Tasks
- [ ] Task 1 description
- [ ] Task 2 description

## Completion Criteria
- [ ] All tasks checked
- [ ] Build succeeds (`dotnet build -c Release`)
- [ ] Tests pass (`dotnet test -c Release`)
- [ ] 2 clean Ralph Loop cycles
```
