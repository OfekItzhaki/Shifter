# 256 — Solver Cumulative Tracking Extension

## Phase

Phase 4 — Solver Extension (Cumulative Tracking and Periods)

## Purpose

Extends the Python CP-SAT solver to accept and use cumulative tracking data from the API payload. This enables cross-run memory for home-leave eligibility (consecutive hours at base accumulate across solver runs) and fairness balancing (total assignments in the period bias the penalty function).

## What was built

| File | Change |
|------|--------|
| `apps/solver/models/solver_input.py` | Added `CumulativeTracking` Pydantic model and `cumulative_tracking` field to `SolverInput` |
| `apps/solver/solver/home_leave.py` | Added `cumulative_tracking` parameter to `add_home_leave_eligibility_preference`; reduces effective threshold by cumulative hours already at base |
| `apps/solver/solver/objectives.py` | Builds `cumulative_assignment_bias` lookup from `cumulative_tracking`; adds `total_assignments_in_period` as additive bias in fairness penalty |
| `apps/solver/solver/engine.py` | Passes `input.cumulative_tracking` to the eligibility preference function |

## Key decisions

- **Additive bias for fairness**: `total_assignments_in_period` is added directly to the base fairness penalty (not multiplied). This means even persons with zero 7-day history but high period-total still get penalized for additional hard tasks.
- **Threshold reduction for eligibility**: Cumulative hours reduce the remaining threshold needed within the horizon. A person with 40h cumulative and 48h threshold only needs 8h more in-horizon to become eligible.
- **Backward compatible**: `cumulative_tracking` defaults to an empty list. When empty, all lookups return 0 and behavior is identical to pre-change.
- **No new parameter on engine.py call to home_leave_constraints**: Only the eligibility preference function needs cumulative data (hard constraints don't use it).

## How it connects

- The API's `SolverPayloadNormalizer` (task 12.1, already implemented) sends `cumulative_tracking` in the JSON payload.
- The solver parses it via the new Pydantic model and uses it in eligibility + fairness.
- Property tests (tasks 14.4, 14.5) will validate the correctness of these integrations.

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/test_engine.py tests/test_constraints.py -x -q
```

All 20 existing tests pass — backward compatibility confirmed.

## What comes next

- Task 14.4: Property test for eligibility threshold with cumulative hours (Hypothesis)
- Task 14.5: Property test for fairness penalty incorporating cumulative history (Hypothesis)
- Task 15: Checkpoint — solver extension verification

## Git commit

```bash
git add -A && git commit -m "feat(solver): add cumulative tracking support for eligibility and fairness"
```
