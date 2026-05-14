# 237 — Solver Pydantic Models: balance_value and preview_mode

## Phase

Home-Leave Slider — Solver Integration

## Purpose

Extend the Python solver's Pydantic models to accept the new `balance_value` field in `HomeLeaveConfig`, the `preview_mode` flag in `SolverInput`, and report `solver_time_ms` in `SolverOutput`. These are all optional fields with defaults, ensuring backward compatibility with existing API payloads.

## What was built

| File | Change |
|------|--------|
| `apps/solver/models/solver_input.py` | Added `balance_value: int = 50` to `HomeLeaveConfig` model |
| `apps/solver/models/solver_input.py` | Added `preview_mode: bool = False` to `SolverInput` model |
| `apps/solver/models/solver_output.py` | Added `solver_time_ms: int = 0` to `SolverOutput` model |

## Key decisions

- All three fields have defaults, so existing payloads without these fields continue to work without modification (backward compatibility).
- `balance_value` defaults to 50, matching the current behavior (weight = 200).
- `preview_mode` defaults to `False`, so normal solver runs are unaffected.
- `solver_time_ms` defaults to 0, so existing output consumers won't break.

## How it connects

- `balance_value` will be used by the weight mapping logic in `home_leave.py` (task 4.2) to compute `weight = balance_value × 4`.
- `preview_mode` will be consumed by `engine.py` (task 4.3) to apply reduced time limits and single-worker configuration.
- `solver_time_ms` will be populated by the engine after solving and returned to the API for the preview response.

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/test_engine.py tests/test_constraints.py tests/test_solver_scenarios.py -x --tb=short -q
```

All existing tests pass without modification, confirming backward compatibility.

## What comes next

- Task 4.2: Implement `balance_value × 4` weight mapping in `add_home_leave_eligibility_preference`
- Task 4.3: Implement `preview_mode` solver behavior (3s limit, 1 worker, timing)

## Git commit

```bash
git add -A && git commit -m "feat(solver): add balance_value, preview_mode, solver_time_ms to Pydantic models"
```
