# Step 248 — Cumulative Tracking Schema Migration

## Phase

Phase: Cumulative Tracking and Periods — Schema Migration

## Purpose

Creates the database schema for the cumulative tracking feature: three new tables (`subscription_periods`, `cumulative_records`, `daily_snapshots`) and one new column on `groups`. These tables enable cross-run memory for the scheduling system — tracking per-person counters across solver runs, storing immutable daily assignment snapshots, and partitioning data by billing lifecycle periods.

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/052_cumulative_tracking.sql` | Single idempotent migration creating all three tables with indexes, RLS policies, unique constraints, and the `schedule_history_retention_days` column on `groups` |

## Key decisions

- **Single migration file**: All four schema changes (tasks 1.1–1.4) are in one file since they form a cohesive unit and reference each other (e.g., `cumulative_records.period_id` → `subscription_periods.id`).
- **Idempotent patterns**: Uses `CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`, and `DO $$ ... IF NOT EXISTS` blocks for policies so the migration can be re-run safely.
- **RLS with `TRUE` parameter**: Uses `current_setting('app.current_space_id', TRUE)` to return NULL instead of raising an error when the setting is missing, matching the project convention from migration 042.
- **`uuid_generate_v4()`**: Uses the uuid-ossp extension function (available via migration 000) rather than `gen_random_uuid()` for consistency with the majority of existing tables.
- **NULL retention = unlimited**: `schedule_history_retention_days` defaults to NULL meaning no limit, matching the requirement spec.

## How it connects

- **Domain entities** (task 2.x) will map to these tables via EF Core configurations.
- **Backfill scripts** (task 5.x) will populate these tables from existing `assignments`, `schedule_versions`, and `presence_windows` data.
- **Application services** (`CumulativeTracker`, `AssignmentSnapshotService`, `PeriodManager`) will read/write these tables.
- **RLS policies** enforce tenant isolation as the last line of defense, complementing application-layer `space_id` filtering.

## How to run / verify

```bash
# Apply migration against local database
psql -h localhost -U postgres -d rolduler -f infra/migrations/052_cumulative_tracking.sql

# Verify tables exist
psql -h localhost -U postgres -d rolduler -c "\dt subscription_periods"
psql -h localhost -U postgres -d rolduler -c "\dt cumulative_records"
psql -h localhost -U postgres -d rolduler -c "\dt daily_snapshots"

# Verify column added
psql -h localhost -U postgres -d rolduler -c "\d groups" | grep schedule_history_retention_days
```

## What comes next

- Task 2.x: Domain entities (`SubscriptionPeriod`, `CumulativeRecord`, `DailySnapshot`) and value objects
- Task 4.x: EF Core entity configurations mapping to these tables
- Task 5.x: Backfill scripts to populate initial data from existing tables

## Git commit

```bash
git add -A && git commit -m "feat(cumulative): add schema migration for subscription_periods, cumulative_records, daily_snapshots"
```
