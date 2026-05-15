# 252 — Backfill Daily Snapshots & Cumulative Records

## Phase

Phase 2 — Backfill (Cumulative Tracking and Periods)

## Purpose

Provides one-time backfill commands that populate `daily_snapshots` and `cumulative_records` tables from existing published schedule versions and presence windows. This enables the cumulative tracking system to have historical data from before the feature was deployed.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Commands/BackfillDailySnapshotsCommand.cs` | MediatR command that generates daily snapshot rows from all published schedule versions. Resolves slot times from both TaskSlots and GroupTask-derived GUIDs. Processes versions newest-first so only the most recent version covering each date wins. Idempotent via duplicate key tracking. |
| `apps/api/Jobuler.Application/Scheduling/Commands/BackfillCumulativeRecordsCommand.cs` | MediatR command that computes initial cumulative records from daily snapshots. Counts assignments across 7d/14d/30d/90d/period windows, categorized by burden level, kitchen, and night. Computes consecutive_hours_at_base from presence windows. Uses upsert pattern. |
| `apps/api/Jobuler.Api/Controllers/PlatformController.cs` | Added `POST /platform/backfill/daily-snapshots` and `POST /platform/backfill/cumulative-records` endpoints (platform admin only). |
| `apps/api/Jobuler.Domain/Scheduling/CumulativeRecord.cs` | Added `Create(spaceId, groupId, personId, periodId)` factory method to enable creation from application layer. |

## Key decisions

- **Newest-first processing**: Published versions are processed in descending `PublishedAt` order. A `coveredKeys` HashSet ensures only the most recent version's data is used for each (person, date, slot) combination.
- **Derived GUID resolution**: The `ResolveGroupTaskSlot` method reverses the `DeriveShiftGuid` algorithm by checking if the first 12 bytes of a slot GUID match a GroupTask's ID, then XOR-extracting the shift index from the last 4 bytes.
- **Batch saves per version/group**: SaveChangesAsync is called per version (snapshots) or per group (cumulative records) to manage memory pressure on large datasets.
- **Consecutive hours computation**: Sums contiguous FreeInBase time since the most recent AtHome window end or period start, matching the design spec's definition.
- **Kitchen/night detection**: Reuses the same heuristics as `UpdateFairnessCountersCommand` (name contains "מטבח"/"kitchen" for kitchen, hour >= 22 or < 6 for night).

## How it connects

- Depends on task 5.1 (BackfillSubscriptionPeriodsCommand) — subscription periods must exist before snapshots can reference them.
- The daily snapshots created here feed into the cumulative records backfill (task 5.3 depends on 5.2).
- Both commands follow the same pattern as `BackfillSubscriptionPeriodsCommand` (task 5.1).
- The `CumulativeRecord.Create` factory method added here will also be used by the CumulativeTracker service (tasks 8.x).

## How to run / verify

```bash
# Build
cd apps/api && dotnet build

# Call the endpoints (requires platform admin JWT):
# POST /platform/backfill/daily-snapshots
# POST /platform/backfill/cumulative-records
```

## What comes next

- Task 5.4: Verification query comparing backfilled 7d counters against existing fairness_counters
- Task 7.x: AssignmentSnapshotService (real-time snapshot creation on publish)
- Task 8.x: CumulativeTracker service (real-time counter updates on publish)

## Git commit

```bash
git add -A && git commit -m "feat(phase2): backfill daily snapshots and cumulative records commands"
```
