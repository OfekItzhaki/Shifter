# 191 — Home-Leave Templates Controller

## Phase
Home-Leave Scheduling — API Backend (Templates)

## Purpose
Exposes REST endpoints for CRUD operations on home-leave configuration templates, allowing group admins to save, list, load, and delete reusable leave configurations scoped to a space.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/HomeLeaveTemplatesController.cs` | New API controller with POST (create), GET (list), GET/{id} (load), DELETE/{id} routes under `spaces/{spaceId}/home-leave-templates`. Follows the same pattern as `ConstraintsController`. |

## Key decisions

- **Permission check in controller**: `constraints.manage` is checked at the controller level before dispatching MediatR commands, consistent with the existing pattern. The command handlers also check permissions for defense-in-depth.
- **Route pattern**: Uses `spaces/{spaceId:guid}/home-leave-templates` matching the design document's API specification.
- **POST returns Created**: Returns HTTP 201 with a Location header pointing to the new template resource.
- **DELETE returns NoContent**: Returns HTTP 204 on successful deletion, consistent with existing delete patterns.
- **Request DTO as record**: `CreateHomeLeaveTemplateRequest` is defined as a record at the bottom of the controller file, following the same pattern as `CreateConstraintRequest` in `ConstraintsController.cs`.

## How it connects

- Dispatches to `CreateHomeLeaveTemplateCommand`, `ListHomeLeaveTemplatesQuery`, `LoadHomeLeaveTemplateQuery`, and `DeleteHomeLeaveTemplateCommand` (created in task 4.1).
- Uses `IPermissionService` and `Permissions.ConstraintsManage` from the domain layer.
- Exception handling (404 from `KeyNotFoundException`, 409 from `ConflictException`) is handled by `ExceptionHandlingMiddleware`.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with zero errors.

## What comes next

- Task 4.3: Update `GroupsController` to support the `isClosedBase` field.
- Frontend template save/load UI (task 12.3) will call these endpoints.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add HomeLeaveTemplatesController with CRUD endpoints"
```
