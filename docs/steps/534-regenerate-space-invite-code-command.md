# Step 534 — RegenerateSpaceInviteCodeCommand

## Phase

Phase — Space Management (Application Layer)

## Purpose

Implements the `RegenerateSpaceInviteCodeCommand` with proper permission enforcement via `IPermissionService`, replacing the previous manual owner check. This ensures the invite code regeneration follows the same authorization pattern as all other owner-only space management commands.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Spaces/Commands/RegenerateSpaceInviteCodeCommand.cs` | Updated command record (renamed `RequestingUserId` → `UserId` for consistency) and handler to use `IPermissionService.RequirePermissionAsync` with `Permissions.OwnershipTransfer` |

## Key decisions

- **Permission key**: Uses `Permissions.OwnershipTransfer` (owner-only) consistent with other space management commands like `UpdateManagementTimeoutCommand` and `TransferOwnershipCommand`.
- **Parameter naming**: Renamed `RequestingUserId` to `UserId` to match the convention used across other space commands.
- **Delegation to domain**: The handler calls `Space.RegenerateInviteCode()` which already handles generating a new 8-character alphanumeric code internally.
- **Return type**: Returns the new code as `string` via `IRequest<string>`.

## How it connects

- Called by `SpacesController.RegenerateInviteCode` endpoint (`POST /spaces/{spaceId}/invite-code/regenerate`)
- Uses `IPermissionService` (implemented in Infrastructure layer) for authorization
- Relies on `Space.RegenerateInviteCode()` domain method for code generation
- Follows the same pattern as `UpdateManagementTimeoutCommand` and other owner-only commands

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build succeeds with no errors.

## What comes next

- Property tests for invite code regeneration (Task 7.6, Property 11)
- API endpoint is already wired in `SpacesController`

## Git commit

```bash
git add -A && git commit -m "feat(space-management): regenerate invite code command with IPermissionService"
```
