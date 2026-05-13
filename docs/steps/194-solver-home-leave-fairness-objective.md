# 194 — Solver Home-Leave Fairness Objective

## Phase

Phase 3 — Python Solver (Home-Leave Scheduling)

## Purpose

Implements the fairness objective for home-leave scheduling. The solver should distribute home-leave fairly across all personnel so that everyone gets approximately equal time at home over the scheduling horizon. This uses a minimax formulation to minimize the maximum deviation of any individual's base_time_ratio from the group mean.

## What was built

| File | Description |
|------|-------------|
| `apps/solver/solver/home_leave.py` | Added `add_home_leave_fairness_objective()` function |

## Key decisions

| Decision | Rationale |
|----------|-----------|
| Minimax formulation over variance minimization | Minimizing variance alone allows outliers; minimax ensures no single person is disproportionately burdened (per design doc). |
| Weight = 500 | Below coverage constraints (1000) but above burden-level preferences (≤99), as specified in requirements. |
| Proxy via leave-count deviation | Since actual ratios are decision variables, we use the algebraic equivalence: minimizing max deviation of base_time_ratio is equivalent to minimizing max deviation of leave counts (scaled by num_eligible). |
| Skip when < 2 eligible members | Per Requirement 6.6 — fairness is meaningless with fewer than 2 people. |
| Integer arithmetic with CP-SAT | CP-SAT requires integer variables; by working in "leave units" and scaling by num_eligible, we avoid floating-point while preserving the minimax property. |

## How it connects

- Called by `engine.py` (task 8.3) after `add_home_leave_constraints()` and `add_home_leave_eligibility_preference()`
- Returns penalty terms that are added to the model's minimization objective alongside coverage and burden penalties
- Uses `home_leave_vars` created by `add_home_leave_constraints()` (task 8.1)
- Validates Requirements 6.3, 6.4, 6.6

## How to run / verify

```bash
cd apps/solver
python -c "import sys; sys.path.insert(0, '.'); from solver.home_leave import add_home_leave_fairness_objective; print('Import OK')"
```

Functional verification:
```bash
python -c "
import sys; sys.path.insert(0, '.')
from ortools.sat.python import cp_model
from solver.home_leave import add_home_leave_fairness_objective
from models.solver_input import HomeLeaveConfig, PersonEligibility

model = cp_model.CpModel()
people = [PersonEligibility(person_id=f'p{i}', role_ids=[], qualification_ids=[], group_ids=[]) for i in range(3)]
config = HomeLeaveConfig(enabled=True, min_rest_hours=8, eligibility_threshold_hours=24, leave_capacity=1, leave_duration_hours=48)
home_leave_vars = {(p, h): model.new_bool_var(f'hl_{p}_{h}') for p in range(3) for h in range(73)}
penalties = add_home_leave_fairness_objective(model, home_leave_vars, {}, [], people, config, 0, 120*3600)
assert len(penalties) == 1
print('Fairness objective OK')
"
```

## What comes next

- Task 8.3: Integrate home-leave module into `engine.py` (calls this function and adds penalties to objective)
- Task 8.4: Add min-rest hard constraint for closed-base groups
- Property-based tests (task 15) will validate fairness properties

## Git commit

```bash
git add -A && git commit -m "feat(solver): add home-leave fairness objective (minimax deviation)"
```
