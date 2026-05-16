# Step 258 — Home-Leave Overhaul Migration

## Phase

Home-Leave Overhaul — Database Foundation

## Purpose

Adds the new columns and constraints to `home_leave_configs` that support the three-mode system (Automatic, Manual, Emergency Freeze). This is the foundational schema change that all subsequent domain, application, and frontend work depends on.

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/053_home_leave_overhaul.sql` | Adds 7 new columns and 4 CHECK constraints to `home_leave_configs` |

### New columns

- `mode` TEXT NOT NULL DEFAULT 'automatic' — active operating mode
- `base_days` INTEGER NOT NULL DEFAULT 7 — days at base in the rotation cycle
- `home_days` INTEGER NOT NULL DEFAULT 2 — days at home in the rotation cycle
- `emergency_freeze_active` BOOLEAN NOT NULL DEFAULT FALSE — whether emergency freeze is on
- `emergency_use_for_scheduling` BOOLEAN NOT NULL DEFAULT FALSE — whether frozen personnel are included in task scheduling
- `freeze_started_at` TIMESTAMPTZ (nullable) — when the current freeze started
- `pre_freeze_mode` TEXT NOT NULL DEFAULT 'automatic' — mode to restore after freeze deactivation

### CHECK constraints

- `chk_mode_valid` — mode IN ('automatic', 'manual')
- `chk_pre_freeze_mode_valid` — pre_freeze_mode IN ('automatic', 'manual')
- `chk_base_days_min` — base_days >= 1
- `chk_home_days_min` — home_days >= 1

## Key decisions

1. **Idempotent DDL** — Each column addition and constraint is wrapped in an existence check (`IF NOT EXISTS`) so the migration can be re-run safely.
2. **Safe defaults** — All new columns have defaults that preserve existing behavior (automatic mode, 7:2 ratio, no freeze).
3. **No RLS changes** — The table already has RLS from migration 042; no new policies are needed since the new columns don't change tenant isolation.
4. **Additive only** — No existing columns are removed or renamed. The `min_rest_hours` column is preserved (will be zeroed in the data migration step 1.2).

## How it connects

- **Depends on**: Migration 042 (table creation), 049 (balance_value column)
- **Required by**: Task 1.2 (data migration), Task 1.4 (domain entity update), Task 1.5 (EF Core mapping)

## How to run / verify

```bash
# Apply migration against local PostgreSQL
psql -U postgres -d rolduler -f infra/migrations/053_home_leave_overhaul.sql

# Verify columns exist
psql -U postgres -d rolduler -c "\d home_leave_configs"

# Verify constraints
psql -U postgres -d rolduler -c "SELECT conname FROM pg_constraint WHERE conrelid = 'home_leave_configs'::regclass AND conname LIKE 'chk_%';"
```

## What comes next

- Task 1.2: Data migration to compute `base_days` and `home_days` from existing `eligibility_threshold_hours`
- Task 1.3: `HomeLeaveMode` enum in the Domain layer
- Task 1.4: Domain entity update with new properties and methods
- Task 1.5: EF Core configuration mapping

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add migration 053 — mode system and emergency freeze columns"
```
