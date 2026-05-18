# 332 — Export PDF Effective Burden

## Phase

Split-Burden Scaling — Export and Frontend Updates

## Purpose

The PDF export was reading burden levels directly from `TaskTypes.BurdenLevel` (the original, unsplit burden). After task 4.1 introduced effective burden storage in DailySnapshots, the export must read from DailySnapshots to display the split-adjusted burden level. This ensures exported schedules reflect the actual burden each person experienced.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Exports/Commands/ExportSchedulePdfCommand.cs` | Replaced direct `TaskTypes.BurdenLevel` join with a `GroupJoin` on `DailySnapshots` to read the effective burden level. Falls back to original burden if no snapshot exists. |

## Key decisions

- **Left join (GroupJoin + DefaultIfEmpty):** DailySnapshots may not exist for every assignment (e.g., draft versions that haven't been published yet). The query falls back to the original `TaskTypes.BurdenLevel` when no matching snapshot is found.
- **Join key:** Matches on `PersonId` + `SlotId` within the same `VersionId` and `SpaceId` to find the correct snapshot row for each assignment.
- **No schema changes:** The DailySnapshot already stores the effective burden level (set in task 4.1). This task only changes the read path.

## How it connects

- Depends on task 4.1 (`AssignmentSnapshotService` storing effective burden in DailySnapshots)
- Satisfies Requirement 8.1: exported schedules show effective burden level
- The same pattern (reading from DailySnapshots) is used by `UpdateFairnessCountersCommand` (task 4.2)

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with 0 errors.

Integration test: create a split task (Hard, split 2, 360 min original), publish a schedule, export PDF, and verify the burden column shows "Normal" instead of "Hard".

## What comes next

- Task 6.2: Frontend task list display of both original and effective burden
- Task 6.3: Frontend SubShiftEditor sending `splitCount` in API requests
- Task 6.4: Verify schedule grid and statistics pages use snapshot burden level

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): export PDF reads effective burden from DailySnapshots"
```
