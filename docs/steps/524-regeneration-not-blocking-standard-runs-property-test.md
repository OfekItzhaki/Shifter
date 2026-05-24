# Step 524 — Regeneration Not Blocking Standard Runs Property Test

## Phase

Schedule Regeneration — Property-Based Testing

## Purpose

Verifies that the concurrency guard for schedule regeneration only blocks concurrent regeneration runs, not standard or emergency solver runs. This ensures that normal scheduling operations are never disrupted by an in-progress regeneration, as specified in Requirement 9.4.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Scheduling/RegenerationNotBlockingStandardRunsPropertyTests.cs` | FsCheck property test (100 iterations) + 3 deterministic edge case tests verifying that standard/emergency runs succeed while a regeneration run is in progress |

## Key decisions

- **Tests exercise the real `TriggerSolverCommandHandler`** against an in-memory EF Core database, not mocks — this validates the actual code path that creates standard/emergency runs.
- **FsCheck generates random combinations** of trigger mode (standard/emergency) and regeneration run status (Queued/Running) to cover all permutations.
- **Assertions verify both success and non-interference**: the new run is created with correct trigger type and status, AND the existing regeneration run remains unaffected.
- **Stale-task guard is satisfied** by seeding a future GroupTask, ensuring the test exercises the full handler logic without hitting unrelated guards.

## How it connects

- Validates **Property 7** from the schedule-regeneration design document.
- Validates **Requirement 9.4**: "THE System SHALL NOT prevent standard solver runs or manual overrides while a Regeneration_Run is in progress."
- Complements the concurrent regeneration rejection test (Property 5, task 4.3) which verifies that only regeneration-vs-regeneration is blocked.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~RegenerationNotBlockingStandardRunsPropertyTests" --verbosity normal
```

Expected: 4 tests pass (1 property test × 100 iterations + 3 deterministic examples).

## What comes next

- Remaining property tests for the schedule-regeneration feature (tasks 4.3, 4.4, 5.2, 7.3, 7.5).
- Frontend component implementation for the regeneration UI.

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): property test for regeneration not blocking standard runs"
```
