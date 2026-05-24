# Step 541 — Space Settings API Endpoints

## Phase

Phase: Space Management — API Layer

## Purpose

Adds the space-level settings endpoints to `SpacesController`: management timeout update, home-leave configuration CRUD, and an alternative route for invite code regeneration. These endpoints allow the Space Owner to manage centralized space settings via the API.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Api/Controllers/SpacesController.cs` | Added 4 new endpoints: `PUT /spaces/{spaceId}/management-timeout`, `PUT /spaces/{spaceId}/home-leave-config`, `GET /spaces/{spaceId}/home-leave-config`, `POST /spaces/{spaceId}/regenerate-invite-code`. Added request DTOs: `UpdateManagementTimeoutRequest`, `UpdateHomeLeaveConfigRequest`. Added `using Jobuler.Domain.Groups` for `HomeLeaveMode` enum. |

## Key decisions

- **PUT for management timeout and home-leave config**: Uses PUT since these are idempotent upsert operations on a single resource.
- **GET returns 404 when no config exists**: If no `SpaceHomeLeaveConfig` has been created yet, the endpoint returns 404 rather than an empty object, matching the query handler's null return.
- **204 No Content for PUT endpoints**: Follows existing controller patterns — write operations that don't return data use 204.
- **200 with body for regenerate-invite-code**: Returns the new invite code in the response body (`{ inviteCode: "..." }`), matching the existing `/invite-code/regenerate` endpoint pattern.
- **Alternative route for regenerate**: The task specifies `/regenerate-invite-code` as the route. The existing `/invite-code/regenerate` route is preserved for backward compatibility.
- **All endpoints inherit `[Authorize]`**: The controller-level `[Authorize]` attribute applies to all actions. Permission checks happen in the command handlers via `IPermissionService`.

## How it connects

- Dispatches `UpdateManagementTimeoutCommand` (task 7.1) for timeout updates
- Dispatches `UpdateSpaceHomeLeaveConfigCommand` (task 7.2) for home-leave config updates
- Dispatches `GetSpaceHomeLeaveConfigQuery` (task 8.1) for reading home-leave config
- Dispatches `RegenerateSpaceInviteCodeCommand` (task 7.5) for invite code regeneration
- All commands perform owner-only permission checks via `IPermissionService`
- FluentValidation validators run automatically via the MediatR pipeline

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Test endpoints (requires auth token):
- `PUT /spaces/{id}/management-timeout` with body `{ "minutes": 30 }`
- `PUT /spaces/{id}/home-leave-config` with full config body
- `GET /spaces/{id}/home-leave-config`
- `POST /spaces/{id}/regenerate-invite-code`

## What comes next

- Task 13.1: Frontend API client functions for these endpoints
- Task 14.1: ManagementTimeoutCard component
- Task 15.1: HomeLeaveConfigCard component
- Task 18.1: Invite code management UI

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add settings endpoints to SpacesController"
```
