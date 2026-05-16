# 269 — Home-Leave Migration Verification & Cleanup

## Phase

Home-Leave Overhaul — Data Migration Verification and Cleanup

## Purpose

Verify that the data migration in `053_home_leave_overhaul.sql` correctly handles edge cases for converting `eligibility_threshold_hours` to `base_days`, and remove the deprecated `BalanceSlider` component that has been replaced by `RatioSlider`.

## What was built

### Task 11.1: Migration Verification (review-only)

Verified the migration formula handles all edge cases correctly:

| `eligibility_threshold_hours` | `ROUND(value / 24)` | `GREATEST(1, ...)` | Result |
|------|------|------|------|
| 0 | 0 | 1 | ✅ Minimum enforced |
| 12 | 1 | 1 | ✅ Rounds 0.5 → 1 |
| 36 | 2 | 2 | ✅ Rounds 1.5 → 2 |
| 168 | 7 | 7 | ✅ Exact division |
| 200 | 8 | 8 | ✅ Rounds 8.33 → 8 |

Confirmed:
- `GREATEST(1, ...)` ensures all values are >= 1 (satisfies CHECK constraints)
- `min_rest_hours` is set to 0 for all rows
- PostgreSQL `ROUND()` on numeric types uses standard rounding (half away from zero)

### Task 11.2: BalanceSlider Removal

- **Deleted**: `apps/web/components/home-leave/BalanceSlider.tsx`
- **Deleted**: `apps/web/__tests__/home-leave/balanceSlider.test.tsx`
- **Verified**: No remaining imports of `BalanceSlider` in the codebase
- **Verified**: `HomeLeaveConfigPanel` already uses `RatioSlider` (from task 9.2)
- **Verified**: TypeScript compilation shows no new errors from the deletion

## Key decisions

- Deleted the test file alongside the component since it only tested the removed component
- Left the comment reference in `RatioSlider.tsx` ("replaces the old BalanceSlider") as documentation

## How it connects

- Migration 053 is the foundation for the entire home-leave overhaul mode system
- `RatioSlider` is the replacement component already integrated in `HomeLeaveConfigPanel`
- Pre-existing TypeScript errors in `ImpactSummary.tsx` are unrelated to this change

## How to run / verify

```bash
# Verify no BalanceSlider references remain
grep -r "BalanceSlider" apps/web/ --include="*.tsx" --include="*.ts"

# TypeScript check (pre-existing errors in ImpactSummary.tsx are unrelated)
cd apps/web && npx tsc --noEmit
```

## What comes next

- Task 11.3: Property test for migration rounding correctness (FsCheck)
- Task 11.4: Property test for mode mutual exclusivity (FsCheck)

## Git commit

```bash
git add -A && git commit -m "chore(home-leave): verify migration edge cases and remove deprecated BalanceSlider"
```
