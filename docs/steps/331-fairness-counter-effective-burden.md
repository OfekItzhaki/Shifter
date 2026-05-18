# 331 — Fairness Counter Effective Burden

## Phase

Split-Burden Scaling — Task 4.2

## Purpose

The `UpdateFairnessCountersCommand` previously derived burden levels by joining Assignments → TaskSlots → TaskTypes, which always returned the **original** burden level. After split-burden scaling, the effective burden level (post-split reduction) is stored in DailySnapshots. This step updates the fairness counter to read from DailySnapshots instead, ensuring `HardTasks7d` and other burden-based metrics reflect the effective burden level.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Scheduling/Commands/UpdateFairnessCountersCommand.cs` | Replaced Assignments → TaskSlots → TaskTypes join with DailySnapshots → TaskTypes join. Burden level is now read from the snapshot's `BurdenLevel` string field (which stores the effective level) and parsed to `TaskBurdenLevel` enum client-side. |

## Key decisions

- **Read from DailySnapshots instead of re-computing**: Rather than calling `BurdenScalingService` again in the fairness counter, we read the already-computed effective burden from DailySnapshots. This ensures a single source of truth (the snapshot) and avoids drift if the formula changes.
- **Two-step query (DB fetch + in-memory parse)**: The `BurdenLevel` field in DailySnapshots is a string. Since `Enum.TryParse` can't be translated to SQL by EF Core, we fetch raw data from the DB and parse the burden level string to the enum in memory.
- **Fallback to Normal**: If a snapshot's `BurdenLevel` string is null or unparseable, it defaults to `TaskBurdenLevel.Normal` — a safe middle-ground that neither inflates hard counts nor deflates them.
- **DateOnly filter**: Added `cutoffDate30d` as a `DateOnly` to filter DailySnapshots by `SnapshotDate` (which is `DateOnly`), while keeping `DateTime` cutoffs for the in-memory time-window grouping.

## How it connects

- **Upstream**: Task 4.1 (`AssignmentSnapshotService`) stores the effective burden level in DailySnapshots. This task consumes that data.
- **Downstream**: The fairness counters feed into the solver's fairness balancing (via `FairnessCounter.HardTasks7d`). Now the solver sees effective burden counts, meaning split tasks won't unfairly penalize people.
- **Related**: The `ExportSchedulePdfCommand` (task 6.1) will also read from DailySnapshots for the same reason.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Integration test scenario:
1. Create a Hard task with `SplitCount = 2` and `ShiftDurationMinutes = 180` (original = 360 min ≥ 240 threshold)
2. Run solver and publish → DailySnapshot stores "normal" as burden level
3. Run `UpdateFairnessCountersCommand` → verify `HardTasks7d` does NOT count this assignment

## What comes next

- Task 4.3: Property test verifying solver payload preserves original burden
- Task 4.4: Unit tests for snapshot and fairness integration

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): fairness counter reads effective burden from snapshots"
```
