# 406 — Freeze Period Changes Count Controller Action

## Phase

Feature: Freeze Period Discard — Task 1.3

## Purpose

Expose the `GetFreezePeriodChangesCountQuery` via an HTTP GET endpoint so the frontend can fetch categorized counts of schedule changes made during an active freeze period. This enables the deactivation dialog to display a summary before the admin confirms.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Api/Controllers/HomeLeaveConfigController.cs` | Added `GetFreezePeriodChangesCount` action with `[HttpGet("freeze-period-changes-count")]` route |
| `Jobuler.Application/HomeLeave/Queries/GetFreezePeriodChangesCountQuery.cs` | Added group existence check (`KeyNotFoundException` → 404) before processing |

## Key decisions

1. **Permission level**: Uses `Permissions.SpaceView` for the read-only endpoint — any authenticated space member can preview the change count. The discard action itself requires elevated `schedule.rollback` permission.
2. **Group existence check in handler**: Added to the query handler (not the controller) per architecture rules — controllers dispatch only, business validation lives in the Application layer.
3. **Error handling via middleware**: `KeyNotFoundException` → 404 and `UnauthorizedAccessException` → 403 are handled by `ExceptionHandlingMiddleware`, keeping the controller clean.
4. **Route**: `GET spaces/{spaceId}/groups/{groupId}/home-leave-config/freeze-period-changes-count` — nested under the existing home-leave-config route prefix.

## How it connects

- **Upstream**: Called by the frontend `FreezeDeactivationDialog` component (task 7.2) when the admin opens the deactivation dialog.
- **Downstream**: Dispatches `GetFreezePeriodChangesCountQuery` (task 1.1) via MediatR, validated by `GetFreezePeriodChangesCountQueryValidator` (task 1.2).
- **Security**: `[Authorize]` at class level ensures authentication; `RequirePermissionAsync(SpaceView)` ensures space membership.

## How to run / verify

```bash
dotnet build --no-restore
# Endpoint: GET /spaces/{spaceId}/groups/{groupId}/home-leave-config/freeze-period-changes-count
# Returns: { overrideCount, manualAssignmentCount, swapCount, totalCount }
# 403 if caller lacks space access, 404 if group does not exist
```

## What comes next

- Task 1.4: Unit tests for the freeze-period change count query
- Task 7.1: Frontend API client function calling this endpoint

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): add GetFreezePeriodChangesCount controller action"
```
