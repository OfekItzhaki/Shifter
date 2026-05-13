# Step 233 — Home-Leave Balance Value Migration

## Phase
Feature: Home-Leave Slider

## Purpose
Adds the `balance_value` column to the `home_leave_configs` table so the slider position (0–100) can be persisted per group. This is the foundational schema change that all other slider tasks depend on.

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/049_home_leave_balance_value.sql` | Adds `balance_value INTEGER NOT NULL DEFAULT 50` column and a CHECK constraint ensuring the value stays within [0, 100]. Uses idempotent `IF NOT EXISTS` patterns. |

## Key decisions

- **DEFAULT 50** — Existing records automatically receive the midpoint value, which maps to the current solver weight of 200 (no behavioral change for existing groups).
- **CHECK constraint** — Database-level enforcement of the [0, 100] range as a last line of defense, complementing domain and API validation.
- **Idempotent DO blocks** — The migration can be re-run safely without errors, following the project's established pattern.

## How it connects

- **Upstream**: Extends the `home_leave_configs` table created in migration 042.
- **Downstream**: The domain entity (task 1.2), EF Core mapping (task 1.3), and API endpoints (tasks 2.x) all depend on this column existing.
- **Solver**: The stored `balance_value` is included in solver payloads and mapped to a preference weight via `weight = balance_value × 4`.

## How to run / verify

```bash
# Apply the migration against a local database
psql -U postgres -d jobuler -f infra/migrations/049_home_leave_balance_value.sql

# Verify the column exists with correct default and constraint
psql -U postgres -d jobuler -c "\d home_leave_configs"
psql -U postgres -d jobuler -c "SELECT balance_value FROM home_leave_configs LIMIT 5;"

# Verify constraint rejects invalid values
psql -U postgres -d jobuler -c "INSERT INTO home_leave_configs (group_id, space_id, min_rest_hours, eligibility_threshold_hours, leave_capacity, leave_duration_hours, balance_value) VALUES (uuid_generate_v4(), uuid_generate_v4(), 8, 24, 2, 48, 101);"
# Expected: ERROR — violates check constraint "chk_balance_value_range"
```

## What comes next

- Task 1.2: Update `HomeLeaveConfig` domain entity with `BalanceValue` property
- Task 1.3: Update EF Core configuration to map the new column

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-slider): add balance_value column migration"
```
