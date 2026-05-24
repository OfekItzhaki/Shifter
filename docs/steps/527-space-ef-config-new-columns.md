# Step 527 — Space EF Configuration for New Columns

## Phase

Space Management — Infrastructure Layer

## Purpose

Maps the two new domain properties (`DeletedAt` and `ManagementTimeoutMinutes`) added in task 1.1 to their corresponding PostgreSQL columns in the `spaces` table. This ensures EF Core correctly persists and reads the soft-delete timestamp and management timeout value.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/SpaceConfiguration.cs` | Added `DeletedAt` → `deleted_at` (nullable timestamptz) and `ManagementTimeoutMinutes` → `management_timeout_minutes` (int, default 15) column mappings |

## Key decisions

- `deleted_at` is nullable by convention (DateTime? maps to nullable timestamptz) — no explicit `.IsRequired(false)` needed since the CLR type is nullable.
- `management_timeout_minutes` uses `.HasDefaultValue(15)` to match the domain entity default and the migration SQL `DEFAULT 15`, ensuring new rows get the correct value even if the property isn't explicitly set.
- Column naming follows the existing snake_case convention used throughout the project.
- Mappings are placed before `created_at`/`updated_at` to maintain logical grouping (entity fields before audit fields).

## How it connects

- Depends on task 1.1 which added the `DeletedAt` and `ManagementTimeoutMinutes` properties to the `Space` entity.
- Task 2.5 will generate the EF migration that creates these columns in the database.
- Application layer commands (tasks 5.1, 5.2, 7.1) will persist changes through these mappings.
- Listing queries (task 11.5) will filter on `deleted_at IS NULL`.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
```

Build succeeds with no errors.

## What comes next

- Task 2.3: Update Group EF configuration for `DeletedBySpaceDeletion`
- Task 2.4: Update SpaceMembership EF configuration for `PermissionLevel`
- Task 2.5: Generate EF migration for all schema changes

## Git commit

```bash
git add -A && git commit -m "feat(space-management): map DeletedAt and ManagementTimeoutMinutes in Space EF config"
```
