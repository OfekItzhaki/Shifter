# 249 — Cumulative Tracking Domain Entities

## Phase

Cumulative Tracking and Periods — Phase 2 (Domain Entities and Value Objects)

## Purpose

Define the core domain entities and value objects for the cumulative tracking subsystem. These entities model subscription periods, per-person cumulative counters, daily assignment snapshots, and the deltas/diffs used during publish operations.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Scheduling/SubscriptionPeriod.cs` | Subscription period entity — logical time segment tied to billing lifecycle. Factory `Create`, method `Close()`, computed `IsActive`. |
| `apps/api/Jobuler.Domain/Scheduling/CumulativeRecord.cs` | Per-person cumulative counters across 5 time windows (7d/14d/30d/90d/period) for 5 categories + consecutive hours tracking. Methods: `ResetPeriodCounters`, `UpdateConsecutiveHours`, `IncrementCounters`. |
| `apps/api/Jobuler.Domain/Scheduling/DailySnapshot.cs` | Immutable per-person-per-day assignment record. Factory `Create(...)`, computed `IsPast`. |
| `apps/api/Jobuler.Domain/Scheduling/CumulativeValueObjects.cs` | `AssignmentCountsDelta` record (counter increments) and `SnapshotDiff` record (publish diff result). |

## Key decisions

- All entities follow the existing `Entity + ITenantScoped` pattern (private setters, factory methods, no public constructors).
- `CumulativeRecord` uses private setters with domain methods to enforce invariants — counters can only be modified through `IncrementCounters` or `ResetPeriodCounters`.
- `SubscriptionPeriod.Close()` throws `InvalidOperationException` if not active, matching the project's error handling convention.
- Value objects are C# records for immutability and structural equality.
- `DailySnapshot` has no update methods — past snapshots are immutable by design.

## How it connects

- These entities map to the `subscription_periods`, `cumulative_records`, and `daily_snapshots` tables created in step 248.
- The application layer services (`CumulativeTracker`, `AssignmentSnapshotService`, `PeriodManager`) will operate on these entities.
- `AssignmentCountsDelta` is passed to `CumulativeRecord.IncrementCounters()` during publish.
- `SnapshotDiff` is returned by `AssignmentSnapshotService.CreateSnapshotsAsync()` and used to adjust counters.

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build --no-restore
```

Build succeeds with zero errors.

## What comes next

- EF Core entity configurations (task 4.1) to map these entities to the database tables.
- `CumulativeTrackingDto` for the solver payload (task 4.2).
- Application layer services that use these entities (tasks 7–9).

## Git commit

```bash
git add -A && git commit -m "feat(cumulative): add domain entities and value objects for cumulative tracking"
```
