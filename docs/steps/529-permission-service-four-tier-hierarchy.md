# Step 529 — Permission Service Four-Tier Hierarchy

## Phase

Phase 10 — Space Management

## Purpose

Enhances the `PermissionService` implementation to enforce the four-tier permission hierarchy (SpaceOwner > GroupOwner > Admin > Member). Previously, the service only checked if the user was the space owner or had an explicit `SpacePermissionGrant` row. Now it respects the `SpacePermissionLevel` on `SpaceMembership` to grant implicit permissions based on the user's tier.

## What was built

### `apps/api/Jobuler.Infrastructure/Auth/PermissionService.cs` (modified)

- **Tier 1 — Space Owner**: If user is `Space.OwnerUserId`, all permissions are granted implicitly (unchanged behavior).
- **Tier 2 — Admin**: If user has `SpacePermissionLevel.Admin` on their `SpaceMembership`, management permissions are granted (people, schedules, constraints, tasks, admin mode, logs, restrictions). Owner-only permissions (ownership transfer, billing, permissions management) are denied.
- **Tier 3 — GroupOwner**: If user has `SpacePermissionLevel.GroupOwner`, admin-level permissions are granted only if the user owns at least one group in the space (verified via `GroupMembership.IsOwner` joined with `Person.LinkedUserId`). Owner-only permissions are denied.
- **Tier 4 — Member**: Falls through to checking explicit `SpacePermissionGrant` rows (unchanged behavior).
- Added `OwnerOnlyPermissions` and `AdminPermissions` static sets for clear categorization.
- Added `IsGroupOwnerInSpaceAsync` helper method that joins `GroupMemberships` with `People` to verify group ownership.

## Key decisions

- **No interface change**: The `IPermissionService` interface remains unchanged — the hierarchy is purely an implementation detail.
- **Permission categorization via static HashSets**: Clear separation between owner-only and admin-level permissions makes it easy to add new permission keys in the future.
- **GroupOwner scoping via GroupMembership.IsOwner + Person.LinkedUserId**: Since `HasPermissionAsync` doesn't accept a `groupId` parameter, GroupOwner permissions are granted if the user owns *any* group in the space. This is consistent with the design doc's intent.
- **Fall-through to explicit grants**: All tiers can still benefit from explicit `SpacePermissionGrant` rows for permissions not covered by their tier.

## How it connects

- `IPermissionService` is called by all command handlers before performing privileged operations.
- The `SpacePermissionLevel` enum and `SpaceMembership.PermissionLevel` property were added in tasks 1.4 and 1.5.
- The `AssignSpaceRoleCommand` (task 7.3) will set permission levels on memberships, which this service now respects.
- Controllers remain thin — they dispatch commands, and handlers call `RequirePermissionAsync`.

## How to run / verify

1. Build the solution: `dotnet build apps/api/Jobuler.Api/Jobuler.Api.csproj`
2. Run existing tests: `dotnet test apps/api/Jobuler.Tests/Jobuler.Tests.csproj`
3. The property test for permission hierarchy enforcement (task 3.2) will validate this implementation.

## What comes next

- Task 3.2: Property test for permission hierarchy enforcement (Property 4)
- Application layer commands (tasks 5.x, 6.x, 7.x) that rely on this hierarchy

## Git commit

```bash
git add -A && git commit -m "feat(space-management): enforce four-tier permission hierarchy in PermissionService"
```
