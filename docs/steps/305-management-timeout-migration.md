# Step 305 — Management Timeout Migration

## Phase
Admin Session Timeout Feature — Database Schema

## Purpose
Add the `management_timeout_minutes` column to the `groups` table so each group can configure how long an admin's management mode session stays active before prompting for re-confirmation. This is the foundational schema change for the admin session timeout feature.

## What was built

| File | Description |
|---|---|
| `infra/migrations/060_management_timeout_minutes.sql` | Adds `management_timeout_minutes` INTEGER NOT NULL DEFAULT 15 column to `groups`, with a CHECK constraint enforcing the range [5, 120]. Backfills existing rows to 15. |

## Key decisions

- **Idempotent migration**: Uses `IF NOT EXISTS` checks for both column and constraint, matching the pattern established in migration 055.
- **DEFAULT handles backfill**: PostgreSQL applies the DEFAULT value to existing rows during `ALTER TABLE ADD COLUMN ... DEFAULT`, but an explicit UPDATE is included as a safety net.
- **CHECK constraint name**: `chk_management_timeout_range` follows the existing naming convention (e.g., `chk_min_rest_between_shifts_hours`).

## How it connects
- The `Group` domain entity (task 1.3) will gain a `ManagementTimeoutMinutes` property mapped to this column.
- The EF Core `GroupConfiguration` (task 1.3) will register the column mapping.
- The `UpdateGroupSettingsCommand` (task 3.2) will allow admins to change this value via the API.
- The frontend inactivity timer (task 6.2) reads this value to determine timeout duration.

## How to run / verify

```bash
# With postgres container running:
docker compose -f infra/compose/docker-compose.yml exec postgres psql -U jobuler -d jobuler

# Run the migration:
\i /docker-entrypoint-initdb.d/060_management_timeout_minutes.sql

# Verify column exists with correct default:
SELECT column_name, data_type, column_default, is_nullable
FROM information_schema.columns
WHERE table_name = 'groups' AND column_name = 'management_timeout_minutes';

# Verify CHECK constraint:
SELECT constraint_name FROM information_schema.table_constraints
WHERE table_name = 'groups' AND constraint_name = 'chk_management_timeout_range';

# Verify existing groups have value 15:
SELECT id, management_timeout_minutes FROM groups LIMIT 5;

# Verify constraint rejects out-of-range values:
UPDATE groups SET management_timeout_minutes = 3 WHERE id = (SELECT id FROM groups LIMIT 1);
-- Should fail with CHECK violation
```

## What comes next
- Task 1.2: Create `platform_settings` table migration
- Task 1.3: Extend `Group` domain entity with `ManagementTimeoutMinutes` property and EF Core mapping

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): add management_timeout_minutes column to groups"
```
