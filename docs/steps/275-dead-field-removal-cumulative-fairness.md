# Step 275 — Dead Field Removal from CumulativeRecord and FairnessCounter

## Phase

Template System Overhaul — Domain Cleanup

## Purpose

Remove hardcoded domain assumptions (DislikedHatedScore, KitchenCount) from the scheduling domain entities and replace them with a generic task-type counting system stored as JSONB. This makes the platform truly generic — any task type can be tracked without schema changes.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Domain/Scheduling/CumulativeRecord.cs` | Removed 10 dead properties (DislikedHatedScore7d/14d/30d/90d/Period, KitchenCount7d/14d/30d/90d/Period). Added `TaskTypeCountsJson` property. Updated `IncrementCounters` to merge task-type counts into JSONB. Updated `ResetPeriodCounters` to reset JSONB to `{}`. |
| `Jobuler.Domain/Scheduling/CumulativeValueObjects.cs` | Removed `DislikedHatedScore` and `KitchenCount` from `AssignmentCountsDelta`. Added `Dictionary<string, int>? TaskTypeCounts` parameter. |
| `Jobuler.Domain/Scheduling/FairnessCounter.cs` | Removed `DislikedHatedScore7d` and `KitchenCount7d` properties. Removed `kitchen7d` parameter from `Update()`. Added `TaskTypeCountsJson` property and `taskTypeCountsJson` parameter to `Update()`. |
| `Jobuler.Application/Scheduling/Models/SolverInputDto.cs` | Removed `DislikedHatedScore7d` and `KitchenCount7d` from `FairnessCountersDto`. Added `Dictionary<string, int>? TaskTypeCounts7d`. |
| `Jobuler.Application/Scheduling/Queries/GetCumulativeStatsQuery.cs` | Removed `KitchenCount` and `DislikedHatedScore` from `CumulativePersonStatsDto` and query handler. |
| `Jobuler.Application/Scheduling/Queries/GetBurdenStatsQuery.cs` | Removed `DislikedHatedScore7d`, `KitchenCount7d`, and `MostKitchenDuty` leaderboard from DTOs and handler. |
| `Jobuler.Application/Scheduling/Commands/BackfillCumulativeRecordsCommand.cs` | Removed kitchen/disliked computation and EF property writes. |
| `Jobuler.Application/Scheduling/Commands/UpdateFairnessCountersCommand.cs` | Replaced `kitchen7d` with generic task-type counting. Passes `taskTypeCountsJson` to `FairnessCounter.Update()`. |
| `Jobuler.Infrastructure/Scheduling/CumulativeTracker.cs` | Removed `IsKitchenTask()` method. Replaced kitchen-specific counting with generic `Dictionary<string, int>` task-type counting per assignment. |
| `Jobuler.Infrastructure/Scheduling/AssignmentSnapshotService.cs` | Updated `AssignmentCountsDelta` construction (removed dead params). |
| `Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Updated `FairnessCountersDto` construction. Added `DeserializeTaskTypeCounts7d` helper. |
| `Jobuler.Infrastructure/Persistence/Configurations/CumulativeRecordConfiguration.cs` | Removed 10 dead column mappings. Added `task_type_counts` JSONB mapping. |
| `Jobuler.Infrastructure/Persistence/Configurations/SchedulingConfiguration.cs` | Removed `disliked_hated_score_7d` and `kitchen_count_7d` mappings. Added `task_type_counts` JSONB mapping. |

## Key decisions

- **JSONB for task-type counts**: Stores `{"taskType": {"7d": n, "14d": n, ...}}` on CumulativeRecord and `{"taskType": count}` on FairnessCounter. Avoids schema changes when new task types are added.
- **Merge on increment**: `IncrementCounters` deserializes existing JSONB, merges incoming counts, and re-serializes. All time windows are incremented together (decay is handled externally by the backfill/recompute logic).
- **Generic counting in CumulativeTracker**: Each assignment's task name (lowercased) becomes a key in the task-type counts dictionary — no more hardcoded "kitchen" detection.
- **Removed MostKitchenDuty leaderboard**: This was a domain-specific leaderboard that no longer makes sense in a generic system.

## How it connects

- **Migration (step 273)**: The SQL migration already dropped the columns and added `task_type_counts JSONB`. This step makes the C# code match.
- **Solver (tasks 5.x)**: The solver will receive `task_type_counts_7d` in the fairness counters DTO instead of `kitchen_count_7d`.
- **Frontend (tasks 9.x)**: Stats pages will need updating to remove kitchen/disliked columns.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
# Should succeed with 0 errors
```

Grep verification (should return 0 matches in .cs files excluding .kiro/):
```bash
grep -r "DislikedHatedScore\|KitchenCount\|IsKitchenTask" apps/api --include="*.cs"
```

## What comes next

- Task 3.x: Infrastructure layer changes (EF Core configs already done here as they were needed for compilation)
- Task 5.x: Solver layer changes (Python side)
- Task 7.3: API stats response DTO cleanup

## Git commit

```bash
git add -A && git commit -m "feat(template-overhaul): remove dead fields from CumulativeRecord and FairnessCounter, add generic task-type counting"
```
