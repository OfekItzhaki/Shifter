# 187 — Solver Input: HomeLeaveConfig Model

## Phase

Phase 3 — Solver Extensions (Home-Leave Scheduling)

## Purpose

Extends the Python solver's input model with a `HomeLeaveConfig` Pydantic model so the solver can receive home-leave configuration parameters from the API. The field is optional (`None` by default) to maintain backward compatibility with existing payloads.

## What was built

| File | Description |
|------|-------------|
| `apps/solver/models/solver_input.py` | Added `HomeLeaveConfig` Pydantic model with fields: `enabled`, `min_rest_hours`, `eligibility_threshold_hours`, `leave_capacity`, `leave_duration_hours`. Added optional `home_leave_config: Optional[HomeLeaveConfig] = None` field to `SolverInput`. |

## Key decisions

- Placed `HomeLeaveConfig` class directly above `SolverInput` to keep related models together.
- Used `Optional[HomeLeaveConfig] = None` default so existing payloads without the field continue to deserialize without error.
- Used `float` for hours fields and `int` for capacity, matching the design document's type specifications.

## How it connects

- The .NET API's `SolverPayloadNormalizer` will populate this field when building payloads for closed-base groups (Task 5.3).
- The solver engine (`engine.py`) will check `input.home_leave_config` to decide whether to activate home-leave constraint logic (Task 8.3).
- The solver output model extension (Task 7.2) will add corresponding output fields.

## How to run / verify

```bash
cd apps/solver
python -c "from models.solver_input import HomeLeaveConfig, SolverInput; print('OK')"
```

## What comes next

- Task 7.2: Extend solver output model with home-leave assignment and metrics fields.
- Task 8.1: Create `home_leave.py` constraint module that consumes this config.

## Git commit

```bash
git add -A && git commit -m "feat(solver): add HomeLeaveConfig to solver input model"
```
