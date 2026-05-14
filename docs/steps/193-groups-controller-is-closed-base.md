# 193 — GroupsController `isClosedBase` Support

## Phase

Home-Leave Scheduling — API Backend (Task 4.3)

## Purpose

Expose the `isClosedBase` flag through the groups API so the frontend can toggle closed-base mode for a group via `PUT /spaces/{spaceId}/groups/{groupId}`.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Groups/Commands/SetGroupClosedBaseCommand.cs` | New MediatR command + handler that validates permissions (`constraints.manage`), loads the group, and calls `group.SetClosedBase(value)` |
| `apps/api/Jobuler.Api/Controllers/GroupsController.cs` | Added `PUT /spaces/{spaceId}/groups/{groupId}` endpoint with `UpdateGroupRequest` DTO containing `IsClosedBase` (bool?) |
| `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs` | Extended `GroupDto` to include `IsClosedBase` field in the response |

## Key decisions

- Used a dedicated `PUT` endpoint as specified in the requirements, rather than adding to the existing PATCH endpoints.
- Permission check uses `constraints.manage` (not `people.manage`) since toggling closed-base mode is a constraint-level operation.
- The `IsClosedBase` field in the request is nullable (`bool?`) — if not provided, the endpoint is a no-op. This allows the PUT endpoint to be extended with additional fields in the future.
- The permission check in the handler is duplicated in the controller for defense-in-depth (controller checks before dispatching, handler also checks).

## How it connects

- Depends on Task 1.4 (the `SetClosedBase` method on the `Group` entity) — already implemented.
- The frontend (Task 12.1) will call this endpoint when the "בסיס סגור" toggle is changed.
- The `GroupDto` response now includes `isClosedBase` so the frontend can read the current state.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

The build succeeds with no errors.

## What comes next

- Frontend toggle component (Task 12.1) will consume this endpoint.
- Checkpoint 6 verifies all API endpoints are wired correctly.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add isClosedBase to GroupsController PUT endpoint"
```
