# 280 — Historical Schedule Fallback

## Phase
Bugfix — Schedule Viewing

## Purpose
When navigating to a past week in the schedule tab, users saw "אין משימות ביום זה" because the `daily_snapshots` table was empty (backfill never ran). This fix adds a fallback path that queries the `assignments` table directly when no snapshots exist.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Scheduling/Queries/GetHistoricalScheduleQuery.cs` | Added fallback logic: when snapshots are empty, queries the most recent published `schedule_version`'s assignments joined with task slots/group tasks to resolve shift times and task names |
| `apps/api/Jobuler.Application/Scheduling/IAssignmentSnapshotService.cs` | Added optional `TaskTypeName` field to `DailySnapshotDto` |
| `apps/web/lib/api/stats.ts` | Added `taskTypeName` field to `HistoricalSnapshotDto` interface |
| `apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx` | Updated mapping to prefer `snap.taskTypeName` over `snap.burdenLevel` for the task name display |

## Key decisions

- **Fallback order**: First try `daily_snapshots` (new system), then fall back to `assignments` table joined with the most recent published version.
- **Version selection**: Iterates published/archived versions ordered by `PublishedAt` descending, picks the first one with matching assignments.
- **Group scoping**: Filters assignments by group membership (person IDs in the group) since assignments don't have a direct group FK.
- **Slot resolution**: Reuses the same `DeriveShiftGuid` reverse-lookup logic from `AssignmentSnapshotService` to resolve both direct `TaskSlot` references and derived `GroupTask` slots.
- **Graceful degradation**: If slot resolution fails for an assignment, it's skipped rather than crashing.
- **DTO extension**: Added `TaskTypeName` as an optional field (default null) to `DailySnapshotDto` to avoid breaking existing snapshot-based responses.

## How it connects

- The `GetHistoricalScheduleQuery` handler is called by `StatsController.GetHistoricalSchedule` endpoint.
- The frontend `ScheduleTab` fetches this endpoint when navigating to a past week.
- Once the snapshot backfill runs for existing data, the fallback path becomes unnecessary but remains as a safety net.

## How to run / verify

```bash
cd apps/api && dotnet build --no-restore -v q
cd apps/web && npx tsc --noEmit
```

Navigate to a past week in the schedule tab — assignments from published versions should now appear.

## What comes next

- Run the snapshot backfill to populate `daily_snapshots` for historical data.
- The fallback will continue to serve as a safety net for any gaps in snapshot coverage.

## Git commit

```bash
git add -A && git commit -m "fix(schedule): fallback to assignments table for historical schedule viewing"
```
