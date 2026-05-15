# 253 — Assignment Snapshot Service

## Phase

Phase 3 — Application Layer (Cumulative Tracking and Periods)

## Purpose

Implements the `IAssignmentSnapshotService` interface and its `AssignmentSnapshotService` implementation. This service is responsible for creating daily snapshots when a schedule version is published, and for retrieving historical assignments for past-date viewing.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/IAssignmentSnapshotService.cs` | Interface defining `CreateSnapshotsAsync` and `GetHistoricalAsync` methods, plus the `DailySnapshotDto` record |
| `apps/api/Jobuler.Infrastructure/Scheduling/AssignmentSnapshotService.cs` | Full implementation with slot resolution (direct TaskSlot + derived GroupTask GUID), past-date immutability (Property 9), future-date upsert, and retention-limit enforcement |
| `apps/api/Jobuler.Api/Program.cs` | DI registration: `AddScoped<IAssignmentSnapshotService, AssignmentSnapshotService>()` |

## Key decisions

- **Slot resolution reuses the same GUID-derivation algorithm** from the backfill command — first tries direct TaskSlot lookup, then reverses the DeriveShiftGuid XOR to find the GroupTask.
- **Past-dated snapshots are never replaced** (Property 9) — the service skips any snapshot_date < today and counts them as "preserved".
- **Future-dated snapshots are replaced** by removing the existing row and inserting a new one, tracking the replaced delta for counter adjustment.
- **Retention limit** is checked via raw SQL query on the `schedule_history_retention_days` column (added in migration 052) since the Group domain entity doesn't expose this property yet.
- **Night shift detection** uses a simple heuristic (start hour between 22:00–06:00) for the replaced delta tracking.

## How it connects

- Called by `PublishVersionCommand` (task 11.1) after a version is published, before cumulative counter updates.
- Returns `SnapshotDiff` which is consumed by `CumulativeTracker.UpdateOnPublishAsync` (task 8.2) to adjust counters.
- `GetHistoricalAsync` is consumed by the historical schedule API endpoint (task 16.3).

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build succeeds with 0 errors.

## What comes next

- Task 7.2: Wire `GetHistoricalAsync` into the schedule history endpoint
- Task 7.3–7.5: Property tests for snapshot creation completeness, replacement, and immutability
- Task 11.1: Hook `CreateSnapshotsAsync` into the publish flow

## Git commit

```bash
git add -A && git commit -m "feat(cumulative): implement IAssignmentSnapshotService interface and service"
```
