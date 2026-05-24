# 530 — Space Management EF Migration

## Phase

Phase 2 — Infrastructure (Persistence & Migrations)

## Purpose

Generates the EF Core migration that applies all space-management schema changes to the PostgreSQL database. This consolidates the domain and configuration changes from tasks 2.1–2.4 into a single migration that can be applied to the database.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Persistence/Migrations/20260524123601_AddSpaceManagement.cs` | Migration adding new columns and table |
| `apps/api/Jobuler.Application/Persistence/Migrations/20260524123601_AddSpaceManagement.Designer.cs` | Auto-generated designer file |
| `apps/api/Jobuler.Application/Persistence/Migrations/AppDbContextModelSnapshot.cs` | Updated model snapshot |

## Key decisions

1. **Single migration for all space-management schema changes** — Rather than one migration per entity change, all changes are bundled into `AddSpaceManagement` for atomic application.
2. **RLS policy added via raw SQL** — EF Core doesn't natively support RLS, so `migrationBuilder.Sql()` is used to enable RLS and create the `tenant_isolation` policy on `space_home_leave_configs`.
3. **Down migration drops RLS before table** — The rollback correctly drops the policy before dropping the table to avoid orphaned policies.
4. **Default values match domain defaults** — `management_timeout_minutes` defaults to 15, `permission_level` defaults to 0 (Member), `deleted_by_space_deletion` defaults to false.

## Schema changes

- `spaces` table: added `deleted_at` (nullable timestamptz), `management_timeout_minutes` (int, default 15)
- `groups` table: added `deleted_by_space_deletion` (bool, default false)
- `space_memberships` table: added `permission_level` (int, default 0)
- New `space_home_leave_configs` table with unique index on `space_id` and RLS tenant isolation policy

## How it connects

- Depends on: tasks 2.1–2.4 (EF configurations for Space, Group, SpaceMembership, SpaceHomeLeaveConfig)
- Enables: Application layer commands (soft-delete, restore, transfer, settings) that write to these new columns
- RLS policy ensures tenant isolation at the database level for the new table

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Api/Jobuler.Api.csproj
# Apply to database:
dotnet ef database update --project Jobuler.Application --startup-project Jobuler.Api --context AppDbContext
```

## What comes next

- Task 3.1: Enhance `PermissionService` to enforce the four-tier hierarchy using the new `permission_level` column
- Task 5.1–5.2: Soft-delete and restore commands that use `deleted_at` and `deleted_by_space_deletion`

## Git commit

```bash
git add -A && git commit -m "feat(phase2): add EF migration for space management schema changes"
```
