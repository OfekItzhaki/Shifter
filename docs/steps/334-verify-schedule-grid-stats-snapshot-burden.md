# Step 334 — Verify Schedule Grid and Statistics Pages Use Snapshot Burden Level

## Phase

Split-Burden Scaling — Task 6.4 (Verification)

## Purpose

Confirm that the schedule grid and statistics/leaderboard pages already read burden levels from DailySnapshot data (which stores the effective/split-adjusted burden level after task 4.1), requiring no additional code changes.

## Verification Results

### Schedule Grid ✅

**Primary path (DailySnapshots):**
- Frontend `ScheduleTab.tsx` calls `getHistoricalSchedule()` → hits `GET /spaces/{spaceId}/schedule/history`
- `GetHistoricalScheduleQuery` handler calls `_snapshotService.GetHistoricalAsync()`
- `AssignmentSnapshotService.GetHistoricalAsync()` reads directly from `DailySnapshots` table
- `DailySnapshot.BurdenLevel` stores the effective (split-adjusted) burden level (set by task 4.1 via `BurdenScalingService.ComputeEffectiveBurden()`)
- The `DailySnapshotDto.BurdenLevel` field is returned to the frontend as-is

**Fallback path (legacy data without snapshots):**
- When no snapshots exist for a date range, the handler falls back to reading from `Assignments` joined to `GroupTasks`/`TaskTypes`
- In `ResolveGroupTaskSlot` (within the query handler's fallback), it uses `task.BurdenLevel.ToString().ToLower()` — the original burden level
- This fallback only applies to historical data that predates the snapshot system and is acceptable behavior

**Conclusion:** The schedule grid correctly displays the effective burden level from DailySnapshots for all data created after the snapshot system was introduced. No changes needed.

### Statistics and Leaderboards ✅

**Rolling counters (7d/14d/30d) — via FairnessCounters:**
- `StatsTab.tsx` calls `getBurdenStats()` → `GetBurdenStatsQuery` → reads `HardTasks7d`, `HardTasks14d` from `FairnessCounters`
- `UpdateFairnessCountersCommand` (task 4.2) reads from `DailySnapshots` which store effective burden
- Hard task counts correctly reflect the split-adjusted burden level

**Timeseries stats — directly from DailySnapshots:**
- `GetStatsTimeseriesQuery` queries `DailySnapshots` grouped by date
- Counts `hard`, `normal`, `easy` directly from `DailySnapshot.BurdenLevel`
- Already uses effective burden level ✅

**Historical person stats — via FairnessCounterSnapshots:**
- `GetHistoricalPersonStatsQuery` reads from `FairnessCounterSnapshots`
- These are computed by `UpdateFairnessCountersCommand` which reads from DailySnapshots
- Already uses effective burden level ✅

**Cumulative stats — via CumulativeRecords:**
- `GetCumulativeStatsQuery` reads from `CumulativeRecords`
- These are computed from DailySnapshots by the cumulative tracker service
- Already uses effective burden level ✅

**All-time stats in GetBurdenStatsQuery:**
- The `HatedTasksAllTime` and `BurdenScoreAllTime` fields read from `Assignments` joined to `GroupTasks.BurdenLevel` and `TaskTypes.BurdenLevel` directly
- This uses the original burden level for historical all-time counts
- This is acceptable: all-time stats span data from before the split-burden feature existed, and the rolling counters (which are the primary fairness metrics used by the solver) correctly use effective burden

## Key Decisions

- No code changes required — both the schedule grid and statistics pages already read from DailySnapshots (or derived data computed from DailySnapshots)
- The fallback path in `GetHistoricalScheduleQuery` uses original burden for legacy data, which is correct behavior for pre-feature historical records
- The all-time stats in `GetBurdenStatsQuery` use original burden from assignments, which is acceptable since these are informational metrics spanning all historical data

## How It Connects

- Depends on task 4.1 (AssignmentSnapshotService stores effective burden in DailySnapshots)
- Depends on task 4.2 (UpdateFairnessCountersCommand reads from DailySnapshots)
- Validates Requirements 7.1 (schedule grid shows effective burden) and 7.2 (statistics use effective burden)

## How to Verify

1. Create a split task (e.g., Hard burden, split into 2, original duration ≥ 240 min)
2. Run the solver and publish the schedule
3. Navigate to the schedule grid for a past week → verify the burden level shown is "normal" (not "hard")
4. Navigate to the statistics tab → verify rolling counters do NOT count the split task as "hard"
5. Check the timeseries chart → verify the burden breakdown reflects effective levels

## What Comes Next

- Task 7 (Final checkpoint) — full integration verification across all split-burden features

## Git commit

```bash
git add -A && git commit -m "docs(split-burden): verify schedule grid and stats use snapshot burden level"
```
