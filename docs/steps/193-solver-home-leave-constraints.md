# 193 — Solver Home-Leave Constraint Module

## Phase

Phase 3 — Solver Extension (Home-Leave Scheduling)

## Purpose

Implements the core CP-SAT constraint functions for home-leave scheduling in the Python solver. This module creates boolean decision variables for leave slots and enforces hard constraints (capacity, no-overlap with missions, min-rest gate, one-at-a-time) plus a soft eligibility preference that encourages sending people home once they've rested enough.

## What was built

| File | Description |
|------|-------------|
| `apps/solver/solver/home_leave.py` | New module with `add_home_leave_constraints()` and `add_home_leave_eligibility_preference()` functions |

### `add_home_leave_constraints()`

Creates boolean decision variables (one per person per possible start hour within the horizon) and enforces:
- **Capacity constraint**: at most `leave_capacity` people on leave per hour
- **No-overlap with missions**: if a person is on leave, they cannot be assigned to any overlapping mission slot
- **Min-rest gate**: a person must have at least `min_rest_hours` of free time before leave starts
- **One-at-a-time**: a person cannot have two overlapping leave slots

### `add_home_leave_eligibility_preference()`

Soft preference that returns penalty terms for the objective function:
- Once a person exceeds `eligibility_threshold_hours` of continuous `free_in_base` time, a penalty is incurred if they are NOT sent on leave
- Weight = 200 (below coverage at 1000, below fairness at 500, but meaningful)

## Key decisions

| Decision | Rationale |
|----------|-----------|
| Leave slots generated at every hour boundary | Gives the solver maximum flexibility to find optimal start times |
| Emergency-bypassed people excluded from leave vars | Consistent with existing emergency bypass behavior — these people are needed for missions |
| Min-rest gate uses same pattern as existing `add_min_rest_constraints` | Consistency with codebase patterns; prevents assignment + leave_var both being 1 |
| Eligibility uses `add_max_equality` for "any leave after eligible" | Efficient CP-SAT encoding — single boolean tracks whether any eligible leave slot is chosen |
| Separate `_to_timestamp` helper duplicated from constraints.py | Avoids circular imports; module is self-contained |

## How it connects

- Called by `engine.py` (task 8.3) when `home_leave_config.enabled == True`
- Uses `HomeLeaveConfig` from `models/solver_input.py` (task 7.1)
- Returns `home_leave_vars` dict used by the fairness objective (task 8.2) and result extraction (task 8.3)
- Eligibility preference returns penalty terms added to the objective alongside other soft penalties

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/ -v          # all 51 existing tests pass
python -c "from solver.home_leave import add_home_leave_constraints, add_home_leave_eligibility_preference; print('OK')"
```

## What comes next

- Task 8.2: Fairness objective (`add_home_leave_fairness_objective`)
- Task 8.3: Integration into `engine.py` — wiring the constraint functions into the solve loop
- Task 8.4: Min-rest hard constraint enforcement for closed-base groups

## Git commit

```bash
git add -A && git commit -m "feat(phase3): solver home-leave constraint module with capacity, overlap, rest-gate, and eligibility"
```
