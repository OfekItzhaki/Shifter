# Step 185 — Home-Leave Database Migration

## Phase

Home-Leave Scheduling — Database & Domain Layer

## Purpose

Creates the database schema required for home-leave scheduling in closed-base groups. This includes marking groups as closed-base, storing per-group leave configuration, and supporting reusable configuration templates.

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/042_home_leave.sql` | Migration adding `is_closed_base` column to `groups`, creating `home_leave_configs` and `home_leave_templates` tables with indexes, RLS policies, and an `updated_at` trigger |

## Key decisions

- **Separate table for configs** — `home_leave_configs` is a 1:1 relationship with `groups` (enforced via UNIQUE on `group_id`), keeping the groups table lean.
- **Templates scoped to space** — Templates use a composite unique index on `(space_id, name)` so different spaces can reuse the same template names.
- **RLS with `current_setting`** — Both tables use the existing `app.current_space_id` session variable pattern for tenant isolation, consistent with all other tenant-scoped tables.
- **`IF NOT EXISTS` guards** — Used on all CREATE TABLE and CREATE INDEX statements for idempotent re-runs.

## How it connects

- The `groups` table gains `is_closed_base` which the API layer reads to conditionally include home-leave config in solver payloads.
- `home_leave_configs` is referenced by the `SolverPayloadNormalizer` when building solver input.
- `home_leave_templates` is used by the template CRUD endpoints for saving/loading reusable configurations.
- The `set_updated_at()` trigger function (defined in migration 001) is reused for the configs table.

## How to run / verify

```bash
# Apply migration against local database
psql -U postgres -d jobuler -f infra/migrations/042_home_leave.sql

# Verify tables exist
psql -U postgres -d jobuler -c "\dt home_leave_*"

# Verify column added
psql -U postgres -d jobuler -c "\d groups" | grep is_closed_base
```

## What comes next

- Domain entities (`HomeLeaveConfig`, `HomeLeaveTemplate`) in the .NET Domain layer
- `IsClosedBase` property on the `Group` entity
- EF Core configurations mapping the new tables

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add database migration 042 for home-leave scheduling"
```
