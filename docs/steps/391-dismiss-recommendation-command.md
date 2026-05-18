# 391 — Dismiss Recommendation Command

## Phase
Feature: Double-Shift Recommendation — Application Layer Commands

## Purpose
Implements the `DismissRecommendationCommand` handler that allows admins to dismiss a double-shift recommendation. This transitions the recommendation from `Active` to `Dismissed` status, recording who dismissed it and when.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Commands/DismissRecommendationCommand.cs` | MediatR command record, FluentValidation validator, and handler that verifies the recommendation exists, checks `TasksManage` permission, and calls `Dismiss(userId)` on the entity |

## Key decisions
- Used `Permissions.TasksManage` as the permission key since recommendations relate to task management (enabling double shifts on tasks). This aligns with how existing task commands check permissions.
- The handler verifies the recommendation belongs to the space via `SpaceId` filter in the query (tenant isolation).
- The domain entity's `Dismiss()` method enforces the business rule that only `Active` recommendations can be dismissed (throws `InvalidOperationException` otherwise).
- FluentValidation ensures all three required IDs are non-empty before the handler executes.

## How it connects
- Uses the `DoubleShiftRecommendation` domain entity created in step 384
- Will be dispatched by the `RecommendationsController` (task 10.2)
- Follows the same MediatR + FluentValidation pattern as `DiscardVersionCommand` and `TriggerSolverCommand`

## How to run / verify
```bash
cd apps/api
dotnet build --no-restore
```
Build should succeed with no new errors.

## What comes next
- `AcceptRecommendationCommand` handler (task 7.2)
- Auto-resolve on manual double-shift enable (task 7.3)
- API controller endpoints that dispatch this command (task 10.2)

## Git commit
```bash
git add -A && git commit -m "feat(double-shift): implement DismissRecommendationCommand handler"
```
