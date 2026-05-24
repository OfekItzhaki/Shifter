# Step 523 — Concurrent Regeneration Rejection Property Test

## Phase

Phase: Schedule Regeneration — Application Layer Property Tests

## Purpose

Validates Property 5 of the schedule-regeneration spec: for any group that already has a regeneration run with status "Queued" or "Running", a new regeneration request SHALL be rejected with a 409 Conflict response and no new ScheduleRun SHALL be created. This ensures the concurrency guard in `TriggerRegenerationCommandHandler` correctly prevents duplicate regeneration runs.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Application/ConcurrentRegenerationRejectionPropertyTests.cs` | FsCheck property test (100 iterations) that randomly generates Queued or Running status for an existing regeneration run, then asserts a second request throws `ConflictException` and no new run is persisted |

## Key decisions

- **Random status generation**: Uses `Gen.Elements(Queued, Running)` to cover both blocking states across 100 iterations
- **Non-stale runs only**: Running runs are seeded with a fresh `StartedAt` (set by `MarkRunning`) so they are not treated as stale by the handler's timeout logic
- **Three-part assertion**: Verifies (1) `ConflictException` is thrown, (2) no new `ScheduleRun` row is created, and (3) the existing run remains in its original status unchanged
- **Follows existing pattern**: Mirrors the structure of `StaleRunTimeoutRecoveryPropertyTests` for consistency

## How it connects

- Validates Requirement 9.1 (concurrency protection)
- Tests the concurrency guard logic in `TriggerRegenerationCommandHandler` (task 4.1)
- Complements the stale run timeout recovery test (task 4.5) which tests the opposite case — stale runs being marked failed to allow new requests

## How to run / verify

```bash
cd apps/api
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~ConcurrentRegenerationRejectionPropertyTests" --verbosity normal
```

Expected: 1 test passes (100 FsCheck iterations).

## What comes next

- Task 4.4: Property test for subscription gating (Property 9)
- Task 5.2: Property test for permission enforcement (Property 8)

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): property test for concurrent regeneration rejection"
```
