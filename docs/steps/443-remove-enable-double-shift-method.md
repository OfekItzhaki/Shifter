# 443 — Remove `EnableDoubleShift` Method from GroupTask

## Phase

Recommendation Approval Flow — Backend Simplification

## Purpose

The `EnableDoubleShift(Guid updatedByUserId)` method on `GroupTask` was a shortcut used by the now-removed `AcceptRecommendationCommand` to silently enable double shift when a recommendation was accepted. With the shift to a passive informational model, the only way to change `AllowsDoubleShift` should be through the full `Update(...)` method (called by the task update endpoint). Removing this method enforces that constraint at the domain level.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Tasks/GroupTask.cs` | Deleted the `EnableDoubleShift(Guid updatedByUserId)` method (including its XML doc comment) |

## Key decisions

- **Delete rather than deprecate**: Since the only caller (`AcceptRecommendationCommand`) was already removed in task 1.1, there are no remaining references. A clean deletion avoids dead code.
- **No replacement method needed**: The existing `Update(...)` method already accepts `allowsDoubleShift` as a parameter, providing the single controlled path for changing this setting.

## How it connects

- Depends on task 1.1 (removal of `AcceptRecommendationCommand`) which was the sole caller
- Satisfies Requirement 1.1 (no automatic modification of `AllowsDoubleShift`) and Requirement 4.4 (task update endpoint is the only way to enable double shift)
- The `Update(...)` method remains unchanged and continues to serve the `TasksController` update endpoint

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain/Jobuler.Domain.csproj
dotnet build  # full solution
dotnet test   # ensure no tests reference the removed method
```

## What comes next

- Task 1.3: Create `TaskConfigSummaryDto` and extend `GetGroupScheduleQuery` response
- Task 1.4: Write unit tests for backend changes (including verifying the task update endpoint is the only way to enable double shift)

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval-flow): remove EnableDoubleShift method from GroupTask domain entity"
```
