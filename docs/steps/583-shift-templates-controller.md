# Step 583 — ShiftTemplatesController

## Phase

Self-Service Scheduling — API Layer (Task 14.2)

## Purpose

Expose CRUD endpoints for shift templates so group admins can create, read, update, and soft-delete recurring weekly shift patterns via the REST API.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/ShiftTemplatesController.cs` | New controller with GET list, GET by id, POST create, PUT update, DELETE (soft-delete) endpoints |

## Key decisions

- **Route**: `spaces/{spaceId}/groups/{groupId}/shift-templates` — follows the existing group-scoped resource pattern (same as qualifications, home-leave-config, etc.)
- **Permission**: `TasksManage` required for all operations (read and write) — consistent with how the command handlers already enforce permissions
- **Request DTOs**: Separate `CreateShiftTemplateRequest` and `UpdateShiftTemplateRequest` records defined in the controller file, keeping the API contract decoupled from internal commands
- **Response pattern**: Create returns `201 Created` with the full DTO; Update returns `200 OK` with the updated DTO; Delete returns `204 No Content`; Get returns `404` when not found
- **Query parameter**: `includeDeleted` on the list endpoint allows admins to see soft-deleted templates when needed

## How it connects

- Dispatches to `CreateShiftTemplateCommand`, `UpdateShiftTemplateCommand`, `DeleteShiftTemplateCommand` (from task 5.1)
- Dispatches to `GetShiftTemplateQuery`, `ListShiftTemplatesQuery` (from task 5.1)
- Uses `IPermissionService` for authorization checks before dispatching write commands
- Follows the same controller patterns as `HomeLeaveTemplatesController` and `QualificationsController`

## How to run / verify

```bash
dotnet build apps/api/Jobuler.Api
```

Note: There is a pre-existing build error in `GroupsController.cs` (missing `ChangeSchedulingModeRequest` from task 14.7) that is unrelated to this controller.

## What comes next

- Task 14.3: ShiftSlotsController
- Task 14.4: ShiftRequestsController
- Task 14.5: WaitlistController
- Task 14.6: ShiftSwapsController

## Git commit

```bash
git add -A && git commit -m "feat(api): add ShiftTemplatesController with CRUD endpoints"
```
