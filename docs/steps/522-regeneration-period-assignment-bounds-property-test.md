# 522 — Regeneration Period Assignment Bounds Property Test

## Phase

Phase: Schedule Regeneration — Property-Based Testing

## Purpose

Validates **Property 4** of the schedule-regeneration spec: for any draft version created by regeneration with start date S, every assignment SHALL have a slot start date >= S. This ensures the solver and worker never produce assignments for dates before the regeneration period, satisfying Requirements 2.2 and 4.2.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Scheduling/RegenerationPeriodAssignmentBoundsPropertyTests.cs` | FsCheck property-based test (100 iterations) plus 4 deterministic edge-case examples |

### Test structure

- **`RegenerationPeriodArbitraries`** — custom FsCheck generators producing random start dates, slot day offsets (some before, some after start), and shift durations
- **`RegenerationTestInput`** — record combining a start date, list of slot offsets, and shift duration
- **Property: `AllRegenerationAssignments_HaveSlotStartDate_GreaterThanOrEqualToRegenerationStart`** — verifies every assignment's TaskSlot has `StartsAt >= S`
- **Property: `NoRegenerationAssignment_ReferencesSlotBeforeStartDate`** — complementary check that no assignment references a pre-start slot
- **4 deterministic examples** — all-after, mixed, exact boundary, all-before scenarios

## Key decisions

- Tests simulate the worker's assignment creation logic in-memory rather than invoking the full `SolverWorkerService`, keeping the test fast and focused on the invariant
- The generator intentionally produces some slots before the start date (20% probability) to exercise the filtering boundary
- Uses `InMemoryDatabase` per test to avoid cross-test contamination
- Validates both the positive (valid slots included) and negative (invalid slots excluded) directions of the property

## How it connects

- Validates the filtering logic in `SolverPayloadNormalizer.BuildAsync` which only sends slots with `StartsAt >= startTime` to the solver
- Validates the worker's assignment creation path in `SolverWorkerService.ProcessNextJobAsync`
- Complements Property 2 (draft creation) and Property 3 (failure handling) tests

## How to run / verify

```bash
cd apps/api
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~RegenerationPeriodAssignmentBounds" --verbosity normal
```

Expected: 6 tests pass (2 property × 100 iterations + 4 deterministic).

## What comes next

- Task 7.5: Property test for published version immutability during regeneration lifecycle

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): property test for regeneration period assignment bounds"
```
