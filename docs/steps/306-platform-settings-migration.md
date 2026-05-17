# Step 306 — Platform Settings Migration

## Phase
Admin Session Timeout Feature — Database Schema

## Purpose
Create the `platform_settings` table to store system-level key-value configuration. The initial use case is the super platform mode session timeout duration, but the table is generic enough to hold any platform-wide setting without schema changes.

## What was built

| File | Description |
|---|---|
| `infra/migrations/061_platform_settings.sql` | Creates `platform_settings` table with `id` (uuid PK), `key` (varchar(100) UNIQUE NOT NULL), `value` (text NOT NULL), `created_at` (timestamptz), `updated_at` (timestamptz). Seeds the `platform_timeout_minutes` row with value `"15"`. |

## Key decisions

- **Generic key-value design**: Rather than a single-row config table, a key-value approach allows adding new platform settings without migrations.
- **Idempotent**: Uses `CREATE TABLE IF NOT EXISTS` and `ON CONFLICT (key) DO NOTHING` for the seed row, so the migration is safe to re-run.
- **No space_id**: This is a system-level table, not tenant-scoped. It stores global platform configuration.
- **varchar(100) for key**: Matches the design document specification and provides enough room for descriptive setting names.

## How it connects
- Task 1.4 will create the `PlatformSettings` domain entity and EF Core configuration mapped to this table.
- Task 3.3 (`UpdatePlatformSettingsCommand`) will read/write the `platform_timeout_minutes` row.
- Task 4.4 will expose `GET/PATCH /platform/settings` endpoints for the super-admin to manage the timeout.
- The frontend platform settings page (task 10.2) will consume these endpoints.

## How to run / verify

```bash
# With postgres container running:
docker compose -f infra/compose/docker-compose.yml exec postgres psql -U jobuler -d jobuler

# Run the migration:
\i /docker-entrypoint-initdb.d/061_platform_settings.sql

# Verify table exists:
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_name = 'platform_settings'
ORDER BY ordinal_position;

# Verify unique constraint on key:
SELECT constraint_name, constraint_type
FROM information_schema.table_constraints
WHERE table_name = 'platform_settings';

# Verify seed row:
SELECT * FROM platform_settings WHERE key = 'platform_timeout_minutes';
```

## What comes next
- Task 1.3: Extend `Group` domain entity with `ManagementTimeoutMinutes` property
- Task 1.4: Create `PlatformSettings` domain entity and EF Core configuration

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): create platform_settings table migration"
```
