# 392 — Accept Recommendation Command

## Phase

Feature: Double-Shift Recommendation — Application Layer Commands

## Purpose

Implements the `AcceptRecommendationCommand` handler that allows admins to accept a double-shift recommendation. This enables `AllowsDoubleShift` on the referenced task, marks the recommendation as resolved, and optionally triggers a new solver run.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Commands/AcceptRecommendationCommand.cs` | MediatR command, validator, handler, and result types for accepting a recommendation |
| `apps/api/Jobuler.Domain/Tasks/GroupTask.cs` | Added `EnableDoubleShift(Guid updatedByUserId)` method for targeted double-shift enablement |

## Key decisions

- **Dedicated `EnableDoubleShift` method on `GroupTask`**: The existing `Update()` method requires all fields. A focused method avoids requiring callers to supply unrelated fields just to flip one boolean.
- **Uses `IMediator` to dispatch `TriggerSolverCommand`**: Rather than directly using `ISolverJobQueue`, the handler dispatches the existing `TriggerSolverCommand` which handles all the run creation logic (baseline lookup, draft discard, etc.).
- **Three-outcome result type**: `AcceptRecommendationOutcome` enum distinguishes between `Accepted`, `AlreadyEnabled`, and `TaskNotFound` — the controller can map these to appropriate HTTP status codes.
- **Task not found → Clear + 404**: If the referenced task was deleted between recommendation creation and acceptance, the recommendation is marked as `Cleared` and a `KeyNotFoundException` is thrown (mapped to 404 by middleware).
- **Permission check uses `Permissions.TasksManage`**: This is the existing permission key that maps to ViewAndEdit/Owner level access for task operations.

## How it connects

- Consumed by `RecommendationsController` (task 10.2) via MediatR dispatch
- Uses `DoubleShiftRecommendation.Resolve()` and `DoubleShiftRecommendation.Clear()` from the domain entity (task 1.1)
- Uses `GroupTask.EnableDoubleShift()` — new method added in this step
- Dispatches `TriggerSolverCommand` (existing) when `TriggerNewRun` is true
- Validates via FluentValidation (consistent with project patterns)

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build should succeed with no new errors or warnings.

## What comes next

- Task 7.3: Auto-resolve on manual double-shift enable
- Task 10.2: Wire accept endpoint in `RecommendationsController`
- Task 17.2: Unit tests for command handlers

## Git commit

```bash
git add -A && git commit -m "feat(double-shift): implement AcceptRecommendationCommand handler"
```
