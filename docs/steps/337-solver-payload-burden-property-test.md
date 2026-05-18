# 337 — Solver Payload Burden Property Test

## Phase

Phase: Split-Burden Scaling (Property Tests)

## Purpose

Validates that the `SolverPayloadNormalizer` always sends the **original** burden level in `TaskSlotDto` entries — never the effective (split-adjusted) burden level. This is critical because the solver must use the raw burden for its fairness balancing algorithm; the effective burden is only for display and tracking purposes.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Scheduling/SolverPayloadBurdenPropertyTests.cs` | FsCheck property test (100 iterations) + 5 deterministic unit tests verifying solver payload preserves original burden |

## Key decisions

- **FsCheck for true property-based testing** — uses random `(BurdenLevel, SplitCount, ShiftDurationMinutes)` combinations to verify the property holds universally, not just for hand-picked examples.
- **Synchronous wrapper for FsCheck** — FsCheck 2.x doesn't support `async Task<bool>` as a testable type, so the property test uses `.GetAwaiter().GetResult()` to call the async normalizer.
- **In-memory EF Core** — each test gets a fresh database to avoid cross-test contamination.
- **Vacuous truth for empty slots** — if a shift duration is too long to generate any slots within the 7-day horizon, the property holds vacuously (no slots to check).
- **Dedicated `BurdenSplitInput` record** — provides readable `ToString()` output for FsCheck counterexamples.

## How it connects

- Validates **Requirement 4.1**: the solver always receives the original burden level.
- Tests the `SolverPayloadNormalizer` (Infrastructure layer) end-to-end with real `GroupTask` entities.
- Complements the `BurdenScalingService` unit tests (step 335) which verify the formula itself.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~SolverPayloadBurdenPropertyTests"
```

All 6 tests should pass (1 FsCheck property × 100 iterations + 5 deterministic examples).

## What comes next

- Task 4.4: Unit tests for snapshot and fairness integration (verifying effective burden flows into snapshots and fairness counters correctly).

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): solver payload burden property test (FsCheck)"
```
