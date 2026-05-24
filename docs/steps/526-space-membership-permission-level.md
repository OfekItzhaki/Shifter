# Step 526 — SpaceMembership Permission Level Enhancement

## Phase

Space Management — Domain Layer

## Purpose

Adds a `PermissionLevel` property to the `SpaceMembership` entity so that each member's authority tier (Member, Admin, GroupOwner, SpaceOwner) is tracked directly on their membership record. This enables the `IPermissionService` to determine a user's permission level within a space without additional lookups.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Spaces/SpaceMembership.cs` | Added `PermissionLevel` property (SpacePermissionLevel, default Member) with private setter and `SetPermissionLevel(SpacePermissionLevel level)` method |

## Key decisions

- Default value is `SpacePermissionLevel.Member` — new members start at the lowest permission level.
- Private setter ensures the property can only be changed through the explicit `SetPermissionLevel` method, maintaining encapsulation.
- No validation in `SetPermissionLevel` — the caller (command handler) is responsible for authorization checks before changing a member's level. This keeps the entity focused on state management.
- Follows the same pattern as existing `Deactivate()` method — simple state mutation without business rule enforcement at the entity level.

## How it connects

- Depends on `SpacePermissionLevel` enum (task 1.4 / step 525)
- Used by `IPermissionService` hierarchy enforcement (task 3.1) to determine user authority
- Used by `AssignSpaceRoleCommand` handler (task 7.3) to change a member's role
- Persisted via EF Core mapping to `permission_level` column (task 2.4)
- Exposed via `GetSpacePermissionLevelsQuery` (task 8.2)

## How to run / verify

```bash
cd apps/api
dotnet build
```

Full solution builds with zero errors.

## What comes next

- Task 2.4: EF Core configuration mapping `PermissionLevel` to `permission_level` int column
- Task 3.1: `PermissionService` reads `SpaceMembership.PermissionLevel` to enforce hierarchy
- Task 7.3: `AssignSpaceRoleCommand` calls `SetPermissionLevel` to change a member's role

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add PermissionLevel property to SpaceMembership entity"
```
