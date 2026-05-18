# 329 — GroupTaskDto Effective Burden and Split Count

## Phase

Split-Burden Scaling — API Layer

## Purpose

Expose the computed effective burden level and split count in the task list API response. This allows the frontend to display both the original and effective burden levels when a task is split, fulfilling Requirement 7.3.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Tasks/Commands/GroupTaskCommands.cs` | Added `string EffectiveBurdenLevel` and `int SplitCount` fields to `GroupTaskDto` record |
| `apps/api/Jobuler.Application/Tasks/Queries/GetGroupTasksQuery.cs` | Added `using Jobuler.Domain.Tasks;` import; updated DTO mapping to call `BurdenScalingService.ComputeEffectiveBurden()` and pass `SplitCount` |

## Key decisions

- **Computed at mapping time**: `EffectiveBurdenLevel` is computed when mapping entity → DTO using the static `BurdenScalingService`, not stored in the database. This keeps the DB schema simple and ensures the value is always consistent with the formula.
- **DTO field ordering**: `EffectiveBurdenLevel` and `SplitCount` are placed immediately after `BurdenLevel` for logical grouping.
- **No DI needed**: `BurdenScalingService` is a static pure function, so no service registration or injection is required.

## How it connects

- Depends on: `BurdenScalingService` (task 1.2), `GroupTask.SplitCount` property (task 1.3)
- Consumed by: Frontend task list display (task 6.2) which shows both original and effective burden when `SplitCount > 1`
- The `TasksController` already returns `List<GroupTaskDto>` via MediatR — no controller changes needed.

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build should succeed with no new errors. The API response for `GET /api/spaces/{id}/groups/{id}/tasks` will now include `effectiveBurdenLevel` and `splitCount` fields in each task object.

## What comes next

- Task 3.4: Property test for split count persistence round-trip
- Task 4.1: AssignmentSnapshotService uses effective burden in snapshots
- Task 6.2: Frontend displays both burden levels for split tasks

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): add EffectiveBurdenLevel and SplitCount to GroupTaskDto"
```
