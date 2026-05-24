# 558 — Reduce Home-Leave Eligibility Weight

## Phase

Bugfix — Solver Constraints Fix (Task 3.2)

## Purpose

The home-leave eligibility weight formula `balance × 20` produced a weight of 1000 when `balance_value = 50` (the default). Since `coverage_weight` is also 1000, the solver treated sending people home as equally important as covering mission slots. This caused understaffing — up to 11/20 people sent home simultaneously while missions went uncovered.

The fix reduces the multiplier from 20 to 10 and caps the result at 999 (`coverage_weight - 1`), ensuring mission coverage **always** takes priority over new home-leave assignments.

## What was built

| File | Change |
|------|--------|
| `apps/solver/solver/home_leave.py` | Changed `ELIGIBILITY_WEIGHT = balance * 20` to `ELIGIBILITY_WEIGHT = min(balance * 10, 999)` in `add_home_leave_eligibility_preference` |

## Key decisions

- **Multiplier reduced from 20 to 10**: For `balance_value=50`, weight becomes 500 instead of 1000. This gives meaningful preference to send eligible people home while keeping it well below coverage weight.
- **Hard cap at 999**: Even with `balance_value=100`, the weight is capped at 999 — strictly below `coverage_weight` (1000). This guarantees mission coverage always wins the trade-off.
- **People already at home unaffected**: Personnel with `presence_window.state = "at_home"` retain highest priority via the existing availability constraint. This change only affects NEW home-leave assignments.
- **Stability weights and fairness objectives unchanged**: The fairness objective (weight 500) and stability weights remain as-is.

## How it connects

- **Bug 2 in solver-constraints-fix spec**: This is the direct fix for the weight imbalance bug.
- **Task 3.3 (concurrent-leave cap)**: Adds an additional safeguard by limiting how many people can be on leave simultaneously relative to mission needs.
- **Task 3.5 (verification)**: The bug condition exploration test will be re-run to confirm this fix resolves the weight imbalance.
- **objectives.py**: The `coverage_weight = 1000` constant in objectives.py is the reference point that this weight must stay below.

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/test_bug_condition_constraints.py::TestBug2HomeLeaveWeightImbalance -v
```

After all fixes are applied (tasks 3.1–3.4), the bug condition tests should pass. For now, the weight formula can be verified directly:

```python
# For any balance_value in [0, 100]:
balance = 50
weight = min(balance * 10, 999)  # = 500 < 1000 ✓

balance = 100
weight = min(balance * 10, 999)  # = 999 < 1000 ✓
```

## What comes next

- Task 3.3: Add dynamic concurrent-leave cap in `home_leave.py`
- Task 3.5: Re-run bug condition exploration tests to verify all fixes work together
- Task 3.6: Re-run preservation tests to confirm no regressions

## Git commit

```bash
git add -A && git commit -m "fix(solver): reduce home-leave eligibility weight below coverage weight"
```
