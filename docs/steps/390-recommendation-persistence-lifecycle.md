# 390 — Recommendation Persistence & Lifecycle Management

## Phase

Feature: Double-Shift Recommendation (Task 5.2)

## Purpose

Adds persistence and lifecycle management to the `RecommendationEngine`. After analysis, recommendations are stored as `DoubleShiftRecommendation` rows with status `Active`. When no shortfall is detected, all existing active recommendations for the group are transitioned to `Cleared`. The upsert pattern on `(space_id, schedule_run_id, group_task_id)` ensures idempotent re-runs. Emergency freeze is respected — no persistence occurs when freeze is active.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Domain/Scheduling/DoubleShiftRecommendation.cs` | Added `Update(...)` method for upsert scenario — resets recommendation to Active with fresh metrics |
| `Jobuler.Infrastructure/Persistence/Configurations/DoubleShiftRecommendationConfiguration.cs` | Added unique index `uq_dsr_space_run_task` on `(SpaceId, ScheduleRunId, GroupTaskId)` to enforce upsert constraint |
| `Jobuler.Infrastructure/Scheduling/RecommendationEngine.cs` | Extended `AnalyzeAsync` with persistence: `PersistRecommendationsAsync` (upsert pattern) and `ClearActiveRecommendationsAsync` (lifecycle clearing) |

## Key decisions

- **Upsert via query-then-update**: Follows the existing codebase pattern (see `UpdateFairnessCountersCommand`) — load existing records, update or insert. No raw SQL needed.
- **Clear on no-shortfall**: Both the "zero uncovered slots" early exit and the "shortfall not detected" path clear active recommendations, satisfying Req 5.1.
- **Emergency freeze check**: Already handled in the analysis logic (returns early before persistence). No additional check needed in persistence methods.
- **Update resets status**: The `Update` method resets status to Active and clears all lifecycle timestamps, ensuring re-runs produce fresh recommendations regardless of prior state.

## How it connects

- **Upstream**: `SolverWorkerService` calls `AnalyzeAsync` after each solver run (task 5.3 handles integration)
- **Downstream**: Persisted recommendations are queried by `GetActiveRecommendationsQuery`, `GetRecommendationsForRunQuery`, and `GetRecommendationForTaskQuery` (tasks 8.1–8.3)
- **Domain**: Uses `DoubleShiftRecommendation.Create()`, `.Update()`, and `.Clear()` lifecycle methods

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build succeeds with no new warnings. The unique constraint will be enforced at the database level after the next migration is applied.

## What comes next

- Task 5.3: Integrate recommendation engine into `SolverWorkerService` (already partially done — notification dispatch exists, persistence now happens inside `AnalyzeAsync`)
- Tasks 7.1–7.3: Command handlers for dismiss/accept actions
- Tasks 8.1–8.3: Query handlers that read persisted recommendations

## Git commit

```bash
git add -A && git commit -m "feat(double-shift): add recommendation persistence and lifecycle management"
```
