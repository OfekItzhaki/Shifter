# Step 224 — Task Rotation Tracking (Army Template Groups)

## Phase

Statistics Overhaul — Phase 3: Task Rotation

## Purpose

Implements per-person task type rotation tracking for army-template groups. This ensures every person cycles through all task types they are qualified for, promoting fairness in duty assignment. The solver uses rotation data to prefer assigning uncompleted task types.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Scheduling/TaskRotationProgress.cs` | Domain entity tracking per-person rotation progress (cycle number, completed task types, completion percentage) |
| `infra/migrations/047_task_rotation_progress.sql` | SQL migration creating the `task_rotation_progress` table with unique constraint, index, and RLS policy |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/SchedulingConfiguration.cs` | Added `TaskRotationProgressConfiguration` EF Core entity configuration |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Added `TaskRotationProgress` DbSet |
| `apps/api/Jobuler.Application/Scheduling/Commands/ComputeTaskRotationCommand.cs` | Command + handler that computes rotation progress for all members of a group after schedule publish |
| `apps/api/Jobuler.Application/Scheduling/Queries/GetTaskRotationQuery.cs` | Query + handler returning rotation progress per person for a group |
| `apps/api/Jobuler.Api/Controllers/StatsController.cs` | Added `GET /spaces/{spaceId}/stats/rotation?groupId={id}` endpoint |
| `apps/api/Jobuler.Application/Scheduling/Models/SolverInputDto.cs` | Added `TaskRotationDto` record and `TaskRotation` field to `SolverInputDto` |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Loads rotation data for group-scoped solver runs and includes it in the payload |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs` | Calls `ComputeTaskRotationCommand` after `UpdateFairnessCountersCommand` for group-scoped runs |
| `apps/solver/models/solver_input.py` | Added `TaskRotation` Pydantic model and `task_rotation` field to `SolverInput` |
| `apps/solver/solver/objectives.py` | Added Objective 7: rotation penalty (weight 200) for assigning already-completed task types |

## Key decisions

- **GroupTask.Id as task type ID**: Each GroupTask IS a task type in the rotation context — its ID is used as the task type identifier in the solver.
- **Qualification-aware rotation**: Only task types the person is qualified for count toward their rotation total. Unqualified tasks are excluded.
- **Cycle reset logic**: When all qualified types are completed, cycle increments and the completed list resets to contain only the triggering task type.
- **Soft penalty (weight 200)**: The rotation objective is a soft penalty, not a hard constraint. The solver prefers uncompleted types but won't leave slots empty to avoid repetition.
- **Graceful degradation**: If no rotation data exists (new group, no published schedules), the rotation objective is simply skipped.

## How it connects

- Depends on: `FairnessCounter` system (Phase 2), `GroupTask` entity, `GroupMembership`, `PersonQualification`
- Called by: `SolverWorkerService` after each successful solver run (group-scoped)
- Consumed by: Frontend rotation progress display (Phase 4, Task 7.8), Solver fairness objective

## How to run / verify

1. Run the SQL migration: `psql -f infra/migrations/047_task_rotation_progress.sql`
2. Build the API: `dotnet build apps/api/Jobuler.Api/Jobuler.Api.csproj`
3. Test the endpoint: `GET /spaces/{spaceId}/stats/rotation?groupId={groupId}` (returns 404 for non-existent groups, empty list for groups with no rotation data)
4. Trigger a solver run for a group → verify `task_rotation_progress` rows are created after publish

## What comes next

- Phase 4 (Frontend): Rotation progress display component (Task 7.8)
- Property-based tests for rotation percentage and cycle reset (Tasks 9.8, 9.9)

## Git commit

```bash
git add -A && git commit -m "feat(phase3): task rotation tracking for army-template groups"
```
