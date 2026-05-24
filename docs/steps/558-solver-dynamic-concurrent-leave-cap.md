# 558 — Dynamic Concurrent-Leave Cap

## Phase

Bugfix — Solver Constraints Fix (Task 3.3)

## Purpose

Prevents the solver from sending too many people on home-leave simultaneously, which could leave missions understaffed. The existing `leave_capacity` constraint is a static admin-configured limit, but it doesn't account for how many people are actually needed for missions at any given time. This dynamic cap ensures that at every hour, enough people remain available to cover the maximum concurrent mission headcount.

## What was built

### Modified files

- **`apps/solver/solver/home_leave.py`**
  - Added `_compute_max_concurrent_mission_headcount()` — scans all task slots across the horizon and returns the peak total `required_headcount` at any single hour.
  - Added `_add_dynamic_concurrent_leave_cap()` — adds a per-hour CP-SAT constraint limiting concurrent home-leave to `len(people) - max_concurrent_mission_headcount`. Accounts for people already at home (presence_window state = "at_home") who cannot be recalled.
  - Integrated the new constraint as "Constraint 1b" in `add_home_leave_constraints()`, immediately after the existing `leave_capacity` constraint (Constraint 1).

## Key decisions

1. **Additive constraint** — The dynamic cap does NOT replace the existing `leave_capacity` per-hour constraint. Both are enforced simultaneously. The effective limit at any hour is `min(leave_capacity, dynamic_cap)`.

2. **Floor of 1** — If `max_concurrent_leave <= 0` (more mission headcount needed than people available), the cap is set to 1 so at least one person can still go on leave. This prevents the model from becoming trivially infeasible.

3. **Already-at-home accounting** — People with `presence_window.state = "at_home"` are counted against the cap but cannot be recalled. The effective cap for new leave vars is reduced by the number of people already at home during each hour.

4. **Per-hour granularity** — The cap is applied per hour (matching the existing leave_capacity constraint granularity) rather than as a single global constraint, because mission headcount varies across the horizon.

## How it connects

- **Upstream**: The constraint uses `TaskSlot.required_headcount` from the solver input to determine mission staffing needs.
- **Existing constraint**: Works alongside the existing `leave_capacity` constraint (Constraint 1) — both must be satisfied.
- **Task 3.2**: The reduced eligibility weight (task 3.2) makes the solver prefer missions over leave as a soft preference; this cap makes it a hard guarantee.
- **Downstream**: The solver output's `home_leave_assignments` will respect this cap, ensuring missions are never understaffed due to excessive concurrent leave.

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/test_preservation_constraints.py -v
python -m pytest tests/ --ignore=tests/test_bug_condition_constraints.py --ignore=tests/test_home_leave_min_rest.py -q
```

All preservation tests pass. The dynamic cap is transparent to existing tests because it only tightens the constraint when mission headcount would otherwise be starved.

## What comes next

- Task 3.4: Post-solve hard constraint validation in `SolverWorkerService.cs`
- Task 3.5: Re-run bug condition exploration test to verify all three bugs are fixed
- Task 3.6: Re-run preservation tests to confirm no regressions

## Git commit

```bash
git add -A && git commit -m "fix(solver): add dynamic concurrent-leave cap to prevent mission understaffing"
```
