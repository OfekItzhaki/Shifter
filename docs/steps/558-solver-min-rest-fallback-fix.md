# 558 — Solver Min-Rest Fallback Resolution Fix

## Phase

Bugfix — Solver Constraints Fix (Task 3.1)

## Purpose

Fixes the min-rest fallback resolution logic in the solver engine for closed-base groups. When `home_leave_config.min_rest_hours == 0` (meaning "use default") and no hard constraint rule exists, the previous code silently fell through to a generic default without explicit guarantees or logging. This fix makes the fallback chain explicit, guaranteed, and observable.

Additionally, this fix enforces a minimum floor of 8.0 hours for closed-base groups, preventing misconfigured hard constraints from allowing unsafe rest gaps.

## What was built

### Modified files

- **`apps/solver/solver/engine.py`**
  - Added `_CLOSED_BASE_MIN_REST_FLOOR = 8.0` constant
  - Added `_resolve_min_rest_hours_closed_base()` function implementing the explicit fallback chain: `config value > hard constraint rule > 8.0 default`
  - Logs a warning when fallback is used (so admins can configure explicitly)
  - Enforces 8.0h minimum floor for closed-base groups — overrides any value below this threshold
  - Updated the main `solve()` function to use the new resolution function
  - Updated `_build_hard_conflicts()` to use the same resolution logic for consistency
  - Non-closed-base groups continue using the existing logic unchanged

- **`apps/solver/tests/test_home_leave_min_rest.py`**
  - Updated `test_min_rest_uses_config_value` to reflect the new behavior: a config value of 4.0h is now overridden to 8.0h minimum for closed-base groups

## Key decisions

1. **Explicit fallback chain**: Rather than relying on implicit fall-through logic, the resolution is now a dedicated function with clear priority: config value → hard constraint rule → 8.0 default.

2. **8.0h minimum floor for closed-base groups**: Even if an admin configures a lower value (e.g., 4h) in either the config or a hard constraint rule, the system enforces 8.0h minimum. This prevents unsafe rest gaps in military/closed-base environments.

3. **Warning logs**: Every fallback usage and every floor override produces a warning log, giving admins visibility into the resolution and guidance to configure explicitly.

4. **Non-closed-base preservation**: The fix only affects the closed-base path (`home_leave_config.enabled = true`). Non-closed-base groups continue using `hard_constraint_rest_hours ?? 8.0` with the existing soft/hard logic.

## How it connects

- This is task 3.1 of the solver-constraints-fix spec
- The `_resolve_min_rest_hours_closed_base` function is used both in constraint building and in conflict analysis
- Tasks 3.2–3.4 address the other two bugs (home-leave weight imbalance and post-solve validation)
- Task 3.5 will re-run the bug condition exploration test to confirm all three bugs are fixed

## How to run / verify

```bash
# Run all solver tests (should all pass)
cd apps/solver
python -m pytest tests/ -q --tb=short

# Run specifically the preservation tests
python -m pytest tests/test_preservation_constraints.py -v

# Run the Bug 1 exploration test (should pass now)
python -m pytest tests/test_bug_condition_constraints.py::TestBug1MinRestViolation -v

# Run the min-rest specific tests
python -m pytest tests/test_home_leave_min_rest.py -v
```

## What comes next

- Task 3.2: Reduce home-leave eligibility weight in `home_leave.py`
- Task 3.3: Add dynamic concurrent-leave cap in `home_leave.py`
- Task 3.4: Add post-solve hard constraint validation in `SolverWorkerService.cs`

## Git commit

```bash
git add -A && git commit -m "fix(solver): explicit min-rest fallback chain for closed-base groups"
```
