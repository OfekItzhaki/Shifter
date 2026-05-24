# 522 — Regeneration Draft Creation Property Test

## Phase
Phase: Schedule Regeneration — Property-Based Testing

## Purpose
Validates **Property 2** of the schedule-regeneration spec: for any valid solver output produced by a regeneration run, the system creates exactly one new `ScheduleVersion` with `status=Draft`, `SourceRunId` matching the run ID, `SupersedesVersionId` matching the published version ID, and `SourceType="regeneration"`.

This ensures the regeneration draft is always correctly linked to both the run that produced it and the published version it intends to supersede, regardless of the solver output shape (varying assignment counts, person IDs, solver timing).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Scheduling/RegenerationDraftCreationPropertyTests.cs` | FsCheck property test (100 iterations) + 3 deterministic edge-case examples |

### Test structure
- **`RegenerationDraftArbitraries`** — FsCheck generator producing random valid solver outputs with 1–50 assignments, random person/slot GUIDs, and random solver timing
- **`ValidSolverOutput`** — Record holding generated test data
- **`SuccessfulRegeneration_CreatesExactlyOneDraft_WithCorrectLinks`** — Property test asserting the invariant across 100 random inputs
- **`Regeneration_WithSingleAssignment_CreatesCorrectDraft`** — Edge case: minimal single assignment
- **`Regeneration_WithManyAssignments_CreatesCorrectDraft`** — Edge case: 100 assignments, verifies all stored
- **`Regeneration_RunIsLinkedToResultVersion`** — Verifies `run.ResultVersionId` points to the new draft (Requirement 8.3)

## Key decisions
- **Simulates worker logic directly** rather than invoking the full `SolverWorkerService` — avoids InMemory DB limitations with `ExecuteSqlRawAsync` (PostgreSQL RLS) while testing the same code path
- **Uses InMemory EF Core** for fast, isolated test execution
- **Generates random solver outputs** with varying assignment counts (1–50) to cover the input space
- **Tests the domain factory + persistence path** which is the core logic the worker delegates to

## How it connects
- Validates Requirements 2.3, 3.1, 4.3, 8.3 from the schedule-regeneration spec
- Tests the same `ScheduleVersion.CreateRegenerationDraft` factory used by `SolverWorkerService`
- Complements task 7.1 (worker implementation) with formal correctness verification
- Works alongside Property 3 (failed regeneration) and Property 1 (published version immutability)

## How to run / verify
```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~RegenerationDraftCreationPropertyTests" --verbosity normal
```

Expected: 4 tests pass (1 property × 100 iterations + 3 deterministic examples).

## What comes next
- Property 3: Failed regeneration records error without side effects (task 7.3)
- Property 4: Regeneration period assignment bounds (task 7.4)
- Property 1: Published version immutability (task 7.5)

## Git commit
```bash
git add -A && git commit -m "feat(schedule-regeneration): property test for draft creation correctness"
```
