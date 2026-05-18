# Step 387: Double-Shift Recommendations Database Migration

## Phase
Feature — Double-Shift Recommendation Engine

## Purpose
Creates the PostgreSQL migration for the `double_shift_recommendations` table, including all columns, indexes, a unique constraint for the upsert pattern, and an RLS policy for tenant isolation.

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/066_double_shift_recommendations.sql` | Raw SQL migration creating the table, indexes, unique constraint, and RLS policy |

## Key decisions

- **Raw SQL migration** — follows the project's existing pattern of numbered `.sql` files in `infra/migrations/` rather than EF Core migrations
- **`gen_random_uuid()`** — used for PK default (consistent with recent migrations like 048, 061)
- **Unique constraint `uq_dsr_space_run_task`** on `(space_id, schedule_run_id, group_task_id)` — enables the upsert pattern so re-running the engine for the same solver run is idempotent
- **RLS policy `dsr_tenant_isolation`** — uses `current_setting('app.current_space_id', TRUE)::UUID` with the `TRUE` parameter to avoid errors when the setting is missing (consistent with other RLS policies in the project)
- **FK references** — `tasks(id)` for `group_task_id` (the DB table is `tasks`, not `group_tasks`), `schedule_runs(id)`, `groups(id)`, `spaces(id)`
- **CASCADE deletes** — all FKs use `ON DELETE CASCADE` to clean up recommendations when parent entities are removed

## How it connects

- **Depends on**: Task 2.1 (EF Core configuration for `DoubleShiftRecommendation`) which defines the column mappings and index names
- **Used by**: The recommendation engine (task 5.2) for persisting and upserting recommendations
- **RLS integration**: Works with `TenantContextMiddleware` which sets `app.current_space_id` before queries

## How to run / verify

Apply the migration against a local PostgreSQL database:
```bash
psql -U postgres -d jobuler -f infra/migrations/066_double_shift_recommendations.sql
```

Verify:
```sql
-- Check table exists
\d double_shift_recommendations

-- Check indexes
\di ix_dsr_*

-- Check unique constraint
\d+ double_shift_recommendations

-- Check RLS policy
SELECT * FROM pg_policies WHERE tablename = 'double_shift_recommendations';
```

## What comes next

- Task 5.1: Implement `RecommendationEngine` core analysis logic
- Task 5.2: Implement recommendation persistence and lifecycle management (uses the upsert pattern enabled by the unique constraint)

## Git commit

```bash
git add -A && git commit -m "feat(double-shift): add database migration for recommendations table"
```
