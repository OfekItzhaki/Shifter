# 540 — Role Assignment API Endpoints

## Phase

Space Management — API Layer

## Purpose

Adds the role assignment and permission levels listing endpoints to `SpacesController`, enabling the frontend to assign permission levels to space members and retrieve the current role assignments for all members.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/SpacesController.cs` | Added `PUT /spaces/{spaceId}/members/{userId}/role` endpoint dispatching `AssignSpaceRoleCommand` |
| `apps/api/Jobuler.Api/Controllers/SpacesController.cs` | Added `GET /spaces/{spaceId}/members/roles` endpoint dispatching `GetSpacePermissionLevelsQuery` |
| `apps/api/Jobuler.Api/Controllers/SpacesController.cs` | Added `AssignSpaceRoleRequest(SpacePermissionLevel Level)` request DTO |
| `apps/api/Jobuler.Api/Controllers/SpacesController.cs` | Added `using Jobuler.Domain.Spaces` for `SpacePermissionLevel` enum |

## Key decisions

- Named the request DTO `AssignSpaceRoleRequest` (not `AssignRoleRequest`) to avoid a naming conflict with the existing `AssignRoleRequest` in `PeopleController.cs` which uses `Guid RoleId` for group role assignment.
- Both endpoints are protected by the class-level `[Authorize]` attribute. Permission checks happen inside the command/query handlers via `IPermissionService`.
- The PUT endpoint returns 204 No Content on success (consistent with other write endpoints in the controller).
- The GET endpoint returns the full list of `SpacePermissionLevelDto` objects (userId + permissionLevel pairs).

## How it connects

- **AssignSpaceRoleCommand** (task 7.3) handles the permission check (`permissions.manage`) and updates the `SpaceMembership.PermissionLevel`.
- **GetSpacePermissionLevelsQuery** (task 8.2) loads all space memberships and returns their permission levels.
- The frontend `RoleAssignmentCard` component (task 17.1) will call these endpoints.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with zero errors.

## What comes next

- Task 11.5: Update listing queries to exclude soft-deleted spaces
- Task 13.1: Frontend API client functions (including `assignSpaceRole` and `getSpacePermissionLevels`)

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add role assignment API endpoints"
```
