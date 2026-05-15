# 254 — CumulativeTracker Service

## Phase
Phase 3 — Application Layer (Cumulative Tracking and Periods)

## Purpose
Implements the `ICumulativeTracker` interface and its `CumulativeTracker` service class. This is the core service responsible for maintaining per-person cumulative counters across solver runs — incrementing on publish, recomputing on presence edits/rollback, resetting on new periods, and providing data for the solver payload.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/ICumulativeTracker.cs` | Interface defining the four cumulative tracking operations |
| `apps/api/Jobuler.Infrastructure/Scheduling/CumulativeTracker.cs` | Full implementation with UpdateOnPublish, RecomputeForPerson, ResetPeriodCounters, GetForSolverPayload |
| `apps/api/Jobuler.Api/Program.cs` | DI registration: `AddScoped<ICumulativeTracker, CumulativeTracker>()` |

## Key decisions

- **Reused `ComputeConsecutiveHoursAtBase` logic** from the backfill command, extracted as an `internal static` method on the service for testability and reuse.
- **Kitchen detection** uses task name matching (Hebrew "מטבח" or English "kitchen") — same pattern as the backfill command.
- **Night detection** uses shift start hour (22:00–06:00) — consistent with existing snapshot service.
- **Slot resolution** duplicates the `DeriveShiftGuid` reverse-lookup pattern from `AssignmentSnapshotService` to resolve both direct TaskSlot references and derived GroupTask slots.
- **Period reset** updates the `PeriodId` via EF entry property access since the domain entity uses private setters.
- **GetForSolverPayload** returns zero-valued DTOs for new members without cumulative records, satisfying Requirement 6.4.

## How it connects

- Called by `PublishVersionCommand` (task 11.2) after snapshots are created
- Called by presence-window commands (task 11.3) on AtHome create/update/delete
- Called by `PeriodManager` (task 9.1) on period transitions
- Called by `SolverPayloadNormalizer` (task 12.1) to include cumulative data in solver input

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build should succeed with no new errors.

## What comes next

- Task 9: PeriodManager service (calls ResetPeriodCountersAsync)
- Task 11: Wire up publish flow and event hooks
- Task 12: Extend SolverPayloadNormalizer to call GetForSolverPayloadAsync

## Git commit

```bash
git add -A && git commit -m "feat(cumulative): implement ICumulativeTracker interface and CumulativeTracker service"
```
