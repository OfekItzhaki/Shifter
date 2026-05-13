# 196 — Solver Engine Home-Leave Integration

## Phase

Phase 3 — Solver Home-Leave Scheduling

## Purpose

Integrates the home-leave constraint module (`home_leave.py`) into the main solver engine (`engine.py`), completing the solver-side implementation of home-leave scheduling. This wires together the decision variables, constraints, eligibility preferences, and fairness objectives, and extracts results into `HomeLeaveAssignment` and `HomeLeaveMetric` output objects.

## What was built

| File | Change |
|------|--------|
| `apps/solver/solver/engine.py` | Added imports for `HomeLeaveAssignment`, `HomeLeaveMetric`, and the three `home_leave.py` functions. Added `datetime`/`timezone` imports. After existing constraints, checks if `home_leave_config` is enabled, computes horizon timestamps, calls `add_home_leave_constraints()`, `add_home_leave_eligibility_preference()`, and `add_home_leave_fairness_objective()`, adds all penalties to the objective. After solving, extracts active leave vars into `HomeLeaveAssignment` objects (ISO 8601 UTC strings), computes per-person `HomeLeaveMetric` (total_base_hours, total_home_hours, base_time_ratio, leave_slot_count), and calculates `fairness_variance`. Includes all three fields in `SolverOutput`. When config is absent/disabled, returns empty lists and null variance. |

## Key decisions

- **Horizon timestamps computed from `horizon_start`/`horizon_end` dates** — converted to midnight UTC timestamps, consistent with how the solver treats date boundaries.
- **Emergency-bypassed people excluded from metrics** — they don't participate in home-leave scheduling, so they shouldn't skew fairness calculations.
- **Fairness variance uses population variance** (divides by N, not N-1) — matches the design doc's intent for a descriptive statistic of the current schedule.
- **ISO 8601 format with `Z` suffix** — consistent with the rest of the solver output contract.
- **base_time_ratio rounded to 4 decimal places, fairness_variance to 6** — per requirements 6.5 and 6.7.

## How it connects

- Depends on: `home_leave.py` (task 8.1, 8.2), solver input/output models (task 7.1, 7.2), min-rest override logic (task 8.4)
- Consumed by: The API's `SolverWorkerService` which deserializes the solver output and stores home-leave assignments
- Enables: Property-based tests (task 15.x) that exercise the full solver pipeline with home-leave enabled

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/ -v
```

All 61 tests pass, including the 10 home-leave min-rest tests and 14 engine tests.

## What comes next

- Task 10.1: Extend publish service to handle home-leave assignments (create presence windows)
- Task 15.x: Property-based tests for solver home-leave properties

## Git commit

```bash
git add -A && git commit -m "feat(solver): integrate home-leave module into engine.py"
```
