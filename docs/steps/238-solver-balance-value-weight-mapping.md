# 238 — Solver balance_value × 4 weight mapping

## Phase

Home-Leave Slider — Solver Integration

## Purpose

Replace the hardcoded eligibility preference weight (200) in the solver's `add_home_leave_eligibility_preference` function with a dynamic weight derived from `balance_value`. This allows the admin slider (0–100) to directly control how aggressively the solver schedules home-leave.

## What was built

| File | Change |
|------|--------|
| `apps/solver/solver/home_leave.py` | Replaced `ELIGIBILITY_WEIGHT = 200` with `balance = config.balance_value if config.balance_value is not None else 50; ELIGIBILITY_WEIGHT = balance * 4` |

## Key decisions

- **Linear mapping**: `weight = balance_value × 4` gives range [0, 400]. Simple, predictable, and matches the design doc.
- **None/absent fallback**: When `balance_value` is `None`, defaults to 50 → weight 200, preserving backward compatibility with existing solver payloads that don't include the field.
- **Zero means disabled**: `balance_value = 0` → weight 0, effectively disabling the soft preference (hard constraints still apply).
- **Truthy check vs explicit None check**: Used `is not None` rather than `or 50` to correctly handle `balance_value = 0` (which should produce weight 0, not 200).

## How it connects

- Depends on task 4.1 which added `balance_value: int = 50` to the `HomeLeaveConfig` Pydantic model.
- The API layer (task 6.4) passes `balance_value` from the stored config into the solver payload.
- The preview endpoint (task 6.3) overrides `balance_value` for preview runs.
- Property test (task 4.4) will verify the linear mapping holds for all values in [0, 100].

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/test_home_leave_min_rest.py -v
python -m pytest tests/test_home_leave_properties.py -v
```

Both test suites pass (10 unit tests + 7 property tests).

## What comes next

- Task 4.3: Implement `preview_mode` solver behavior (time limit, workers, solver_time_ms)
- Task 4.4: Property test verifying linear weight mapping for all balance_value in [0, 100]

## Git commit

```bash
git add -A && git commit -m "feat(solver): balance_value × 4 weight mapping in home-leave preference"
```
