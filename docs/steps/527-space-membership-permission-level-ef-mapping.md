# Step 527 — SpaceMembership PermissionLevel EF Configuration

## Phase

Space Management — Infrastructure Layer

## Purpose

Maps the `SpaceMembership.PermissionLevel` property to the `permission_level` integer column in the database, with a default value of `0` (Member). This ensures the four-tier permission hierarchy is persisted correctly and new memberships default to the lowest permission level.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/SpaceConfiguration.cs` | Added `PermissionLevel` property mapping in `SpaceMembershipConfiguration` — stored as int with default `SpacePermissionLevel.Member` |

## Key decisions

- Stored as integer (`HasConversion<int>()`) rather than string — matches the design document specification and is more efficient for comparison/sorting.
- Default value set to `SpacePermissionLevel.Member` (0) — ensures existing rows and new memberships start at the lowest permission level without requiring a data migration.
- Column name `permission_level` follows the existing snake_case convention used throughout the project.

## How it connects

- Depends on `SpaceMembership.PermissionLevel` property (task 1.5 / step 526)
- Depends on `SpacePermissionLevel` enum (task 1.4 / step 525)
- Required by task 2.5: EF migration generation will pick up this mapping
- Required by task 3.1: `PermissionService` queries this column to enforce hierarchy
- Required by task 7.3: `AssignSpaceRoleCommand` persists role changes through this mapping

## How to run / verify

```bash
cd apps/api
dotnet build
```

Full solution builds with zero errors. The mapping will be included in the migration generated in task 2.5.

## What comes next

- Task 2.5: Generate EF migration that includes the `permission_level` column on `space_memberships`
- Task 3.1: `PermissionService` hierarchy enforcement reads this column

## Git commit

```bash
git add -A && git commit -m "feat(space-management): map SpaceMembership.PermissionLevel to permission_level int column"
```
