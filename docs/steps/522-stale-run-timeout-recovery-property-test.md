# Step 522 — Stale Run Timeout Recovery Property Test

## Phase

Phase 4 — Application Layer Property Tests (Schedule Regeneration)

## Purpose

Validates Property 6 from the schedule-regeneration design: for any regeneration run in "Running" status longer than (solver_timeout + grace_period), the system SHALL treat it as failed and allow new regeneration requests. This ensures the system recovers from stale/hung solver runs and doesn't permanently block regeneration for a group.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Application/StaleRunTimeoutRecoveryPropertyTests.cs` | FsCheck property-based test (100 iterations) verifying stale run recovery, plus edge-case unit tests |

## Key decisions

- **FsCheck `[Property(MaxTest = 100)]`** — uses FsCheck.Xunit integration with 100 random iterations as specified in the design document
- **Generator strategy** — generates random `extraSecondsOverThreshold` (1–3600) representing how far past the stale threshold the run is, ensuring the property holds for any duration beyond the threshold
- **In-memory EF Core** — uses `UseInMemoryDatabase` for fast isolated tests without external dependencies
- **StartedAt manipulation via EF entry** — overrides the `StartedAt` property set by `MarkRunning()` to simulate a stale timestamp in the past
- **Complementary edge-case tests** — includes a test confirming non-stale runs still block new regeneration (409 Conflict), and a boundary test at exactly 1 second over threshold

## How it connects

- Tests the stale-run detection logic in `TriggerRegenerationCommandHandler` (step 4.1)
- Validates Requirement 9.3 (concurrency protection with stale timeout recovery)
- Uses the same `SolverTimeoutSeconds` (30) and `StaleGracePeriodMinutes` (5) defaults as the handler
- Complements Property 5 (concurrent regeneration rejection) by testing the recovery path

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~StaleRunTimeoutRecoveryPropertyTests"
```

Expected: 3 tests pass (1 FsCheck property with 100 iterations + 2 edge-case facts).

## What comes next

- Task 5.2: Permission enforcement property test
- Task 7.x: Worker-level property tests for regeneration draft creation and failure handling

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): property test for stale run timeout recovery"
```
