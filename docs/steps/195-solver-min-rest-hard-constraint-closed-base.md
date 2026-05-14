# 195 — Solver Min-Rest Hard Constraint for Closed-Base Groups

## Phase

Phase 3 — Solver Home-Leave Constraints (Task 8.4)

## Purpose

Ensures that for closed-base groups with `home_leave_config` enabled, the minimum rest constraint is **strictly hard** — no soft penalty exception for long shifts (24h). This guarantees personnel always get adequate rest regardless of scheduling pressure. Also adds HardConflict reporting for min-rest violations when the solver returns infeasible.

## What was built

| File | Description |
|------|-------------|
| `apps/solver/solver/engine.py` | Modified to override `rest_hours` with `home_leave_config.min_rest_hours` when enabled, and pass `soft_penalties=None` to disable the long-shift soft exception. Added min-rest violation detection in `_build_hard_conflicts`. |
| `apps/solver/solver/i18n.py` | Added `min_rest_violation` translation strings for en, he, ru locales. |
| `apps/solver/tests/test_home_leave_min_rest.py` | New test file with 10 tests covering: hard constraint enforcement for long shifts, config value override, HardConflict reporting, and emergency bypass behavior. |

## Key decisions

| Decision | Rationale |
|----------|-----------|
| Pass `soft_penalties=None` to `add_min_rest_constraints` | The existing function already checks `if is_long_shift and soft_penalties is not None` — passing `None` makes ALL rest constraints hard without modifying `constraints.py`. Minimal change, maximum safety. |
| Override `rest_hours` from `home_leave_config.min_rest_hours` | The config value takes precedence over any `min_rest_hours` hard constraint in the payload, ensuring the closed-base configuration is the single source of truth. |
| Min-rest conflict detection in `_build_hard_conflicts` | Reports all slot pairs that would violate min-rest for each non-bypassed person. This gives admins clear feedback on why the schedule is infeasible. |
| Emergency bypass respected in both constraint and conflict reporting | Consistent with existing emergency bypass behavior — bypassed persons skip all rest checks. |

## How it connects

- Builds on Task 8.1 (`home_leave.py` constraint module) and Task 8.3 (engine integration)
- The `add_min_rest_constraints` function in `constraints.py` is unchanged — the behavior change is controlled entirely by the `soft_penalties` parameter
- HardConflict entries use the same `HardConflict` model from `solver_output.py`
- The i18n strings follow the existing pattern in `i18n.py`

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/test_home_leave_min_rest.py -v
python -m pytest tests/ -q  # full suite — 61 tests pass
```

## What comes next

- Task 9 (Checkpoint): Verify solver module accepts payloads with `home_leave_config`
- Task 15: Property-based tests for solver home-leave properties

## Git commit

```bash
git add -A && git commit -m "feat(solver): add min-rest hard constraint for closed-base groups"
```
