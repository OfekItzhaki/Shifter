# Step 259 — Home-Leave Data Migration

## Phase

Home-Leave Overhaul — Database Foundation

## Purpose

Converts existing threshold-based home-leave configurations to the new day-based system. Existing rows have `eligibility_threshold_hours` and `leave_duration_hours` stored as decimals; this migration computes the equivalent whole-day values (`base_days`, `home_days`) and zeroes out `min_rest_hours` since the new day-based ratio system handles rest implicitly.

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/053_home_leave_overhaul.sql` (appended) | Added section 9: UPDATE statement that computes `base_days` and `home_days` from existing hour-based columns and sets `min_rest_hours = 0` |

### Migration logic

```sql
UPDATE home_leave_configs
SET base_days = GREATEST(1, ROUND(eligibility_threshold_hours / 24))::INTEGER,
    home_days = GREATEST(1, ROUND(leave_duration_hours / 24))::INTEGER,
    min_rest_hours = 0;
```

- `ROUND(x / 24)` — converts hours to days, rounding to nearest whole day (Requirement 10.3)
- `GREATEST(1, ...)` — ensures migrated values are always at least 1 day (satisfies `chk_base_days_min` and `chk_home_days_min` constraints)
- `min_rest_hours = 0` — the new system handles rest implicitly via the eligibility threshold (Requirement 10.4)

## Key decisions

1. **Single UPDATE** — All three column updates are done in one statement for atomicity and performance.
2. **ROUND not CEIL** — The design doc specifies "round to the nearest whole day" for values that don't divide evenly (Requirement 10.3). `ROUND` is the correct choice over `CEIL` or `FLOOR`.
3. **GREATEST(1, ...)** — Protects against edge cases where `eligibility_threshold_hours` or `leave_duration_hours` is 0 or very small, ensuring the CHECK constraints are satisfied.
4. **Cast to INTEGER** — PostgreSQL's `ROUND` returns numeric; the explicit `::INTEGER` cast ensures the value matches the column type.

## How it connects

- **Depends on**: Step 258 / Task 1.1 (columns and constraints must exist before the UPDATE runs)
- **Required by**: Task 1.4 (domain entity expects valid `base_days`/`home_days`), Task 11.1 (verification)
- **Validates**: Requirements 10.1, 10.3, 10.4

## How to run / verify

```bash
# Apply full migration (includes schema + data migration)
psql -U postgres -d rolduler -f infra/migrations/053_home_leave_overhaul.sql

# Verify migrated data
psql -U postgres -d rolduler -c "SELECT id, eligibility_threshold_hours, base_days, leave_duration_hours, home_days, min_rest_hours FROM home_leave_configs LIMIT 10;"

# Verify all base_days >= 1 and home_days >= 1
psql -U postgres -d rolduler -c "SELECT COUNT(*) FROM home_leave_configs WHERE base_days < 1 OR home_days < 1;"
# Expected: 0

# Verify all min_rest_hours = 0
psql -U postgres -d rolduler -c "SELECT COUNT(*) FROM home_leave_configs WHERE min_rest_hours != 0;"
# Expected: 0
```

## What comes next

- Task 1.4: Domain entity update with new properties and methods
- Task 1.5: EF Core configuration mapping for new columns
- Task 11.1: Verification of data migration on sample data

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add data migration to compute base_days and home_days from existing hours"
```
