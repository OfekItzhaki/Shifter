# 330 — AssignmentSnapshotService Uses Effective Burden Level

## Phase

Split-Burden Scaling — Backend Integration

## Purpose

DailySnapshots must store the effective (split-adjusted) burden level rather than the raw task burden level. This ensures that fairness counters, statistics, and exports all reflect the actual burden experienced per person after splitting.

## What was built

- **`Jobuler.Infrastructure/Scheduling/AssignmentSnapshotService.cs`** — Modified `ResolveGroupTaskSlot` to call `BurdenScalingService.ComputeEffectiveBurden(task.BurdenLevel, task.SplitCount, task.ShiftDurationMinutes)` instead of using `task.BurdenLevel` directly. The effective burden level is now stored in the DailySnapshot `burden_level` field.

## Key decisions

- Only the snapshot path is modified. The solver payload (`SolverPayloadNormalizer`) continues to use the original burden level — this is intentional so the solver's fairness balancing is unaffected.
- The `BurdenScalingService` is a static pure function already in the `Jobuler.Domain.Tasks` namespace, which was already imported by the service — no new `using` statements needed.

## How it connects

- **Upstream**: `BurdenScalingService` (step 323) provides the computation. `GroupTask.SplitCount` (step 325) provides the split count data.
- **Downstream**: `UpdateFairnessCountersCommand` reads from DailySnapshots and will now see effective burden levels. Exports and UI statistics also read from snapshots.

## How to run / verify

```bash
cd apps/api/Jobuler.Infrastructure
dotnet build --no-restore
```

Integration test: create a split task (Hard, split 2, 180 min shifts = 360 min original), run solver, publish, verify the DailySnapshot stores "normal" instead of "hard".

## What comes next

- Task 4.2: Update `UpdateFairnessCountersCommand` to use effective burden from snapshots for hard task counting.
- Task 4.4: Unit/integration tests for snapshot and fairness integration.

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): snapshot service stores effective burden level"
```
