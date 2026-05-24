# Step 523 — Regeneration Failed Error Property Test

## Phase

Schedule Regeneration — Property-Based Testing

## Purpose

Validates Property 3 of the schedule regeneration feature: that any solver failure (timeout, infeasibility, or exception) results in the regeneration run being marked as failed with a non-empty error summary, and no new ScheduleVersion is created. This ensures the system never produces orphaned drafts or corrupts the published schedule when the solver encounters problems.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Scheduling/RegenerationFailedErrorPropertyTests.cs` | FsCheck property test (100 iterations) generating random failure modes and verifying no side effects occur |

## Key decisions

- **Three failure categories generated**: timeout, infeasibility, and exception/error — covering all paths in the worker's failure handling logic
- **TimedOut treated as valid failure state**: The worker uses `MarkTimedOut` for timeout cases (a sub-type of failure), so the property accepts both `Failed` and `TimedOut` as terminal failure states
- **Published version integrity verified**: Each iteration confirms the published version's status and assignment count remain unchanged
- **ResultVersionId null check**: Ensures no draft version is linked to the failed run
- **Deterministic edge case tests included**: Timeout, infeasibility, exception, and zero-assignments scenarios each have a dedicated xUnit Fact for clear failure diagnostics

## How it connects

- Validates Requirements 3.3 (published version unchanged on failure), 3.4 (failure recorded), and 8.4 (failed status with error message)
- Mirrors the failure handling path in `SolverWorkerService.ProcessNextJobAsync` (the `shouldDiscard = true` branch)
- Complements Property 2 (successful regeneration) by testing the opposite outcome
- Uses the same test infrastructure (InMemory EF, entity seeding helpers) as `RegenerationDraftCreationPropertyTests`

## How to run / verify

```bash
cd apps/api
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~RegenerationFailedErrorPropertyTests" --verbosity normal
```

All 5 tests should pass (1 property × 100 iterations + 4 deterministic facts).

## What comes next

- Property 1 (Published version immutability) — task 7.5
- Property 5 (Concurrent regeneration rejection) — task 4.3
- Property 8 (Permission enforcement) — task 5.2

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): property test for failed regeneration error without side effects"
```
