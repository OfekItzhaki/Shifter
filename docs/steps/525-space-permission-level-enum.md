# Step 525 — SpacePermissionLevel Enum

## Phase

Space Management — Domain Layer

## Purpose

Defines the four-tier permission hierarchy for space members. This enum establishes the authority levels (Member, Admin, GroupOwner, SpaceOwner) that the `IPermissionService` uses to enforce access control across the space.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Spaces/SpacePermissionLevel.cs` | New enum with four values: Member (0), Admin (1), GroupOwner (2), SpaceOwner (3) |

## Key decisions

- Values are explicitly numbered (0–3) to ensure stable database storage and allow comparison-based hierarchy checks (higher value = more authority).
- Placed in `Jobuler.Domain/Spaces/` namespace alongside related space entities, following the same pattern as `HomeLeaveMode` in `Groups/`.
- Kept as a standalone file (not embedded in another class) for discoverability and reuse across `SpaceMembership`, `IPermissionService`, and command handlers.

## How it connects

- Used by `SpaceMembership.PermissionLevel` property (task 1.5)
- Consumed by `IPermissionService` hierarchy enforcement (task 3.1)
- Referenced in `AssignSpaceRoleCommand` (task 7.3)
- Stored as `permission_level` int column in `space_memberships` table (task 2.4)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain/Jobuler.Domain.csproj
```

Build succeeds with zero errors.

## What comes next

- Task 1.5: Add `PermissionLevel` property to `SpaceMembership` entity using this enum
- Task 2.4: EF Core mapping for the new column
- Task 3.1: `PermissionService` hierarchy enforcement logic

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add SpacePermissionLevel enum to Domain layer"
```
