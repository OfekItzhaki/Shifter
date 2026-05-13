# 187 — Solver Output Home-Leave Fields

## Phase

Phase 3 — Solver Extensions (Home-Leave Scheduling)

## Purpose

Extends the Python solver's output model (`SolverOutput`) with home-leave assignment and metrics fields so the solver can communicate leave schedules and fairness data back to the API.

## What was built

| File | Description |
|------|-------------|
| `apps/solver/models/solver_output.py` | Added `HomeLeaveAssignment` model (person_id, starts_at, ends_at), `HomeLeaveMetric` model (person_id, total_base_hours, total_home_hours, base_time_ratio, leave_slot_count), and three new fields on `SolverOutput`: `home_leave_assignments` (default []), `home_leave_metrics` (default []), `fairness_variance` (default None) |

## Key decisions

- Used default empty lists (`= []`) and `Optional[float] = None` so existing solver output without these fields still deserializes correctly — backward compatible.
- Placed new models before `SolverOutput` class to follow the existing file pattern (helper models defined first, main output model last).
- `starts_at` and `ends_at` are strings (ISO 8601 UTC) matching the existing pattern used by the API for datetime serialization.

## How it connects

- **Upstream**: The solver engine (`engine.py`) will populate these fields when home-leave constraints are active (Task 8.3).
- **Downstream**: The .NET API's `SolverOutputDto` (Task 5.2) mirrors these fields to deserialize the solver response.
- **Property tests** (Tasks 15.1–15.7) will validate invariants on these output fields.

## How to run / verify

```bash
cd apps/solver
python -c "from models.solver_output import SolverOutput, HomeLeaveAssignment, HomeLeaveMetric; print('OK')"
```

Verify that creating a `SolverOutput` without the new fields still works (defaults apply), and that providing data populates correctly.

## What comes next

- Task 8.1: Create `home_leave.py` constraint module that produces assignments filling these fields.
- Task 8.3: Integrate into `engine.py` to populate the output.

## Git commit

```bash
git add -A && git commit -m "feat(solver): extend SolverOutput with home-leave assignments and metrics"
```
