# 191 — Home-Leave Config Controller

## Phase
Phase: Home-Leave Scheduling — API Backend

## Purpose
Exposes the home-leave configuration endpoints so the frontend can read and update leave settings for closed-base groups. This controller dispatches commands/queries via MediatR, following the same pattern as `ConstraintsController`.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/HomeLeaveConfigController.cs` | New controller with `GET` and `PUT` endpoints at `/spaces/{spaceId}/groups/{groupId}/home-leave-config`. Dispatches `GetHomeLeaveConfigQuery` and `UpsertHomeLeaveConfigCommand` via MediatR. |

## Key decisions

- **No permission check in controller** — the `UpsertHomeLeaveConfigCommand` handler already performs `IPermissionService.RequirePermissionAsync` and validates group existence/ownership. The GET query returns defaults without requiring elevated permissions (consistent with read-only config access).
- **Request DTO defined in controller file** — follows the same pattern as `ConstraintsController` which defines `CreateConstraintRequest` / `UpdateConstraintRequest` inline.
- **Route pattern** — uses `spaces/{spaceId:guid}/groups/{groupId:guid}/home-leave-config` matching the nested resource pattern used by `GroupRolesController`.

## How it connects

- Dispatches to `UpsertHomeLeaveConfigCommand` (task 3.1) and `GetHomeLeaveConfigQuery` (task 3.2)
- The command handler validates group existence (404), permissions (403), and field ranges (400)
- Frontend (task 12.2) will call these endpoints from the "הגדרות חופשות" panel

## How to run / verify

```bash
dotnet build --no-restore
# API project compiles cleanly with the new controller
```

The endpoints will be accessible once the API is running:
- `GET /spaces/{spaceId}/groups/{groupId}/home-leave-config`
- `PUT /spaces/{spaceId}/groups/{groupId}/home-leave-config`

## What comes next

- Task 4.2: `HomeLeaveTemplatesController` for template CRUD
- Task 4.3: Update `GroupsController` to support `isClosedBase` field
- Task 12.2: Frontend "הגדרות חופשות" panel that calls these endpoints

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add HomeLeaveConfigController with GET/PUT endpoints"
```
