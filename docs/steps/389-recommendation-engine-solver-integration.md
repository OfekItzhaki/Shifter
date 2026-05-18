# 389 — Recommendation Engine Solver Integration

## Phase

Feature: Double-Shift Recommendation (Task 5.3)

## Purpose

Integrates the `IRecommendationEngine` into `SolverWorkerService.ProcessNextJobAsync` so that after every feasible solver run, the engine analyzes the output for staffing shortfalls and produces double-shift recommendations. If recommendations are generated, a notification is dispatched to space admins. The entire recommendation flow is wrapped in try/catch to ensure it never disrupts the core solver pipeline.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs` | Added recommendation engine integration block after run status is persisted and before the main notification dispatch. Resolves `IRecommendationEngine` from the DI scope, calls `AnalyzeAsync`, and dispatches a `double_shift_recommendation` notification if recommendations are produced. Wrapped in try/catch with warning-level logging on failure. |

## Key decisions

1. **Placement**: The recommendation engine runs after both the success and failure paths have persisted the run status, but only when the solver output is feasible (`output.Feasible`). This ensures the engine has access to valid solver output data including uncovered slots and home leave assignments.

2. **Guard condition**: `input is not null && output.Feasible` — the engine only runs when we have valid solver input/output. The engine itself handles the "no uncovered slots" case by returning early with an empty result.

3. **Try/catch isolation**: The entire recommendation block is wrapped in a try/catch that logs a warning and continues. This satisfies the requirement that recommendation failures never disrupt the solver flow.

4. **Notification metadata**: Includes `run_id` and `total_uncovered_slots` in the notification metadata JSON, enabling the frontend to link back to the specific run and display context.

5. **Locale-aware notifications**: Uses the solver input's locale to produce Hebrew, Russian, or English notification titles and bodies.

6. **Admin skip**: The `NotifySpaceAdminsAsync` method already handles the case where no admins exist in the group (it skips silently), satisfying Requirement 3.4.

## How it connects

- Depends on `IRecommendationEngine` (implemented in task 5.1 as `RecommendationEngine`)
- Depends on `INotificationService.NotifySpaceAdminsAsync` (existing notification infrastructure)
- The engine's `AnalyzeAsync` handles persistence of recommendation entities (task 5.2)
- Frontend components (tasks 13, 14) will consume the notifications and query the stored recommendations

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The build should succeed with no new errors. The existing CS8602 warning on line 519 is pre-existing (unrelated to this change).

## What comes next

- Task 5.4–5.10: Property-based tests for the recommendation engine
- Task 7: Command handlers for dismiss/accept actions
- Task 10.3: DI registration of `IRecommendationEngine`

## Git commit

```bash
git add -A && git commit -m "feat(double-shift): integrate recommendation engine into SolverWorkerService"
```
