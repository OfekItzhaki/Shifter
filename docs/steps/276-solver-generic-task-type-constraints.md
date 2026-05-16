# 276 — Solver Generic Task-Type Constraints

## Phase

Template System Overhaul — Solver Layer (Tasks 5.1, 5.2, 5.3)

## Purpose

Replace the hardcoded kitchen-specific constraint logic in the Python solver with a generic `max_task_type_per_period` system. This allows any task type to have frequency limits, not just "kitchen".

## What was built

| File | Change |
|------|--------|
| `apps/solver/models/solver_input.py` | Removed `disliked_hated_score_7d` and `kitchen_count_7d` from `FairnessCounters`; added `task_type_counts_7d: dict[str, int] = {}` |
| `apps/solver/solver/constraints.py` | Deleted `add_kitchen_frequency_constraints`; added `add_max_task_type_per_period_constraints` that handles any task type generically |
| `apps/solver/solver/engine.py` | Updated import and call site to use `add_max_task_type_per_period_constraints` |
| `apps/solver/solver/objectives.py` | Replaced `disliked_hated_score_7d` reference with `hated_tasks_7d` for fairness weighting |
| `apps/solver/tests/test_constraints.py` | Updated import to new function name |
| `apps/solver/tests/test_solver_scenarios.py` | Replaced `disliked_hated_score_7d` with `hated_tasks_7d` in fairness test |

## Key decisions

- The new constraint function matches slots by `task_type_name` (case-insensitive), dropping the old `task_type_id` fallback since the migration already normalizes constraint payloads.
- Historical counts come from `fairness_counters[].task_type_counts_7d` dict keyed by lowercase task type name.
- The `period_days` field in the payload is accepted but not used for filtering within a single solver horizon — it's used upstream for the historical lookup window.
- Fairness objective now uses `hated_tasks_7d` instead of the removed `disliked_hated_score_7d`.

## How it connects

- Depends on: Task 1.1 (migration converting `max_kitchen_per_week` → `max_task_type_per_period`), Task 1.4 (FairnessCounter entity changes), Task 3.2 (SolverPayloadNormalizer sending `task_type_counts_7d`)
- Consumed by: Task 5.4 (property test for constraint enforcement)

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/ -x -q
```

All 78 tests pass.

## What comes next

- Task 5.4: Property test for `max_task_type_per_period` enforcement (Hypothesis)
- Checkpoint 6: Verify no references to old kitchen/disliked_hated fields remain in solver code

## Git commit

```bash
git add -A && git commit -m "feat(solver): replace max_kitchen_per_week with generic max_task_type_per_period"
```
