# 203 — Home-Leave Solver Property-Based Tests

## Phase

Phase 6 — Testing (Property-Based Tests)

## Purpose

Validates the correctness of the home-leave solver module using property-based testing with Hypothesis. These tests generate random but valid solver inputs and verify that universal invariants hold across all feasible outputs, providing high confidence in the solver's constraint enforcement.

## What was built

- `apps/solver/tests/test_home_leave_properties.py` — 7 property-based tests covering:
  - **Property 3**: Min-rest invariant (no consecutive assignments violate min_rest_hours)
  - **Property 4**: Capacity invariant (at most leave_capacity people on leave per hour)
  - **Property 5**: Leave duration correctness (every leave == leave_duration_hours)
  - **Property 6**: No leave-mission overlap (no mission overlaps any leave for same person)
  - **Property 7**: No concurrent leave per person (no two leaves overlap for same person)
  - **Property 8**: Base-time ratio computation (ratio matches formula rounded to 4dp)
  - **Property 9**: Disabled config produces empty output (no leave data when disabled)

## Key decisions

- Used `deadline=None` in Hypothesis settings because the CP-SAT solver legitimately takes 100-300ms per example
- Fixed horizon of 3 days (72 hours) to keep solver runtime manageable
- Generated 2-4 people and 3-8 slots to keep the problem small enough for fast solving
- Used `assume(result.feasible)` to skip infeasible outputs (properties only apply to feasible solutions)
- HomeLeaveConfig strategy generates min_rest 4-8h, eligibility 12-24h, capacity 1-2, duration 12-24h
- Property 9 uses a separate strategy that generates either None or enabled=False configs

## How it connects

- Tests validate the solver logic in `solver/home_leave.py` and `solver/engine.py`
- Uses the same `SolverInput`/`SolverOutput` models as the production solver
- Validates requirements 3.2, 3.3, 4.5, 5.1, 5.2, 5.3, 5.8, 6.1, 6.2, 7.2, 8.3

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/test_home_leave_properties.py -v
```

All 7 tests should pass (takes ~50s due to solver execution per example).

## What comes next

- Task 16: .NET and frontend property-based tests (FsCheck for config validation, fast-check for fairness warning)

## Git commit

```bash
git add -A && git commit -m "feat(phase6): home-leave solver property-based tests with Hypothesis"
```
