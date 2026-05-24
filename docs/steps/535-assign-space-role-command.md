# 535 — Assign Space Role Command

## Phase

Phase: Space Management — Application Layer (Settings Commands)

## Purpose

Implements the `AssignSpaceRoleCommand` and its handler, allowing users with `permissions.manage` to assign permission levels (Member, Admin, GroupOwner, SpaceOwner) to space members. This enables the four-tier permission hierarchy to be managed by authorized users.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Spaces/Commands/AssignSpaceRoleCommand.cs` | Command record (`SpaceId`, `TargetUserId`, `Level`, `ActorUserId`) and handler that verifies permissions, loads the target membership, assigns the new level, saves, and logs the action via `IAuditLogger` |

## Key decisions

- **Permission key**: Uses `Permissions.PermissionsManage` (`"permissions.manage"`) for the authorization check, consistent with the design doc specifying `permissions.manage` for role assignment.
- **Membership validation**: Throws `KeyNotFoundException("Space membership not found.")` if the target user has no active membership in the space, matching the task specification.
- **Audit before/after snapshot**: Records the previous and new `PermissionLevel` as JSON in the audit log entry for traceability.
- **Pattern consistency**: Follows the same structure as `RestoreSpaceCommand` and `TransferOwnershipCommand` — permission check → load entity → mutate → save → audit.

## How it connects

- Depends on `IPermissionService` (Infrastructure layer) for authorization.
- Depends on `IAuditLogger` (Infrastructure layer) for audit trail.
- Depends on `SpaceMembership.SetPermissionLevel()` (Domain layer) for the mutation.
- Will be dispatched by `SpacesController` endpoint `PUT /spaces/{spaceId}/members/{userId}/role` (task 11.4).

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no new errors.

## What comes next

- Task 7.4: Enhance `UpdateSpaceCommand` handler with name validation
- Task 7.5: Create `RegenerateSpaceInviteCodeCommand` and handler
- Task 11.4: Wire the API endpoint to dispatch this command

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add AssignSpaceRoleCommand and handler"
```
