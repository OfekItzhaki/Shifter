# 393 — GetRecommendationForTaskQuery Handler

## Phase

Feature: Double-Shift Recommendation — Application Layer Queries

## Purpose

Provides a query handler that returns the most recent active double-shift recommendation for a specific task. Used by the frontend to display an inline suggestion next to the task's `AllowsDoubleShift` toggle in group task settings.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Queries/GetRecommendationForTaskQuery.cs` | MediatR query + handler that accepts `SpaceId`, `GroupTaskId`, `UserId`, checks permissions and emergency freeze, then returns the most recent active `RecommendationDto` or null |

## Key decisions

- Uses `HasPermissionAsync` (returns bool) instead of `RequirePermissionAsync` (throws) because the spec says "return empty if not ViewAndEdit or Owner" rather than throwing 403
- Checks `AllowsDoubleShift` on the task itself (Req 6.2) — if already true, returns null since the recommendation is no longer relevant
- Determines the `GroupId` from the `GroupTask` entity to look up the correct `HomeLeaveConfig` for emergency freeze check (Req 6.3)
- Returns the most recent active recommendation (ordered by `CreatedAt` DESC) to handle cases where multiple runs produced recommendations for the same task
- Uses `Permissions.TasksManage` as the permission key, consistent with the dismiss/accept command handlers

## How it connects

- Called by `RecommendationsController` at `GET /spaces/{spaceId}/tasks/{taskId}/recommendation`
- Returns `RecommendationDto` (defined in task 3.2) which the frontend `useRecommendationForTask` hook consumes
- Relies on `DoubleShiftRecommendation` entity (task 1.1) and `HomeLeaveConfig` entity for freeze check
- Complements `GetActiveRecommendationsQuery` (task 8.1, group-level) and `GetRecommendationsForRunQuery` (task 8.2, run-level)

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no new errors or warnings.

## What comes next

- Task 8.4–8.10: Property-based tests for query behavior
- Task 10.1: Wire this query into the `RecommendationsController`
- Task 14.1: Frontend `TaskDoubleShiftSuggestion` component that calls this endpoint

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): add GetRecommendationForTaskQuery handler"
```
