# 239 — Solver Preview Mode Behavior

## Phase

Home-Leave Slider — Solver Integration

## Purpose

Implements the `preview_mode` solver behavior so that when the frontend requests a quick preview of the schedule impact, the solver runs with reduced time limits and resources, returning a best-effort result within 3 seconds. Also records wall-clock solve time in the output for the API to relay to the frontend.

## What was built

| File | Change |
|------|--------|
| `apps/solver/solver/engine.py` | Added `import time`; added preview_mode configuration block (3s time limit, 1 worker, disabled logging); wrapped `solver.solve()` with wall-clock timing; included `solver_time_ms` in the return value |

## Key decisions

- **Preview mode overrides the default timeout** — When `preview_mode=True`, the solver's `max_time_in_seconds` is set to 3.0, overriding the environment-configured `SOLVER_TIMEOUT` (default 30s). This ensures interactive preview stays fast.
- **Single worker for reduced overhead** — `num_workers=1` prevents the solver from spawning multiple threads, reducing CPU usage for a quick approximate result.
- **Disabled search logging** — `log_search_progress=False` eliminates I/O overhead from solution logging during preview.
- **Wall-clock timing wraps only the solve call** — The `solver_time_ms` measures only the CP-SAT solve phase, not model construction or result extraction, giving an accurate measure of solver computation time.
- **Empty result path unchanged** — The `_empty_result` function relies on the Pydantic default (`solver_time_ms: int = 0`), which is correct since no solving occurs.

## How it connects

- **Task 4.1** added the `preview_mode` field to `SolverInput` and `solver_time_ms` to `SolverOutput` — this task implements the actual behavior.
- **Task 6.2** (PreviewHomeLeaveHandler) will call the solver with `preview_mode=True` and relay `solver_time_ms` to the frontend.
- **Requirement 8.2–8.7** are satisfied: reduced time limit, single worker, no logging, feasible/optimal/no_solution status, wall-clock timing, and no change when preview_mode is false.

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/test_engine.py -v
python -m pytest tests/ -v
```

All 68 existing tests pass — they exercise the non-preview path (preview_mode defaults to False), confirming no regression.

## What comes next

- Task 4.4: Property test for linear weight mapping
- Task 4.5: Property test for hard constraints invariant
- Task 4.6: Property test for solver status mapping
- Task 6.x: Preview endpoint integration (API calls solver with preview_mode=True)

## Git commit

```bash
git add -A && git commit -m "feat(solver): implement preview_mode behavior with 3s timeout and wall-clock timing"
```
