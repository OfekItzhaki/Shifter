# 557 — Solver Constraints Preservation Tests

## Phase
Bugfix — Solver Constraints Fix (Phase 2: Preservation Tests)

## Purpose
Capture baseline behavior of the solver for non-buggy inputs before implementing the fix. These tests ensure that existing correct behavior is preserved after the fix is applied — preventing regressions in non-closed-base rest logic, long-shift soft penalties, emergency bypass, and draft creation flows.

## What was built
- `apps/solver/tests/test_preservation_constraints.py` — Property-based tests covering 5 preservation properties:
  1. Non-closed-base short shifts enforce hard rest constraints
  2. Long shifts (≥ 24h) in non-closed-base groups use soft penalty path
  3. Emergency-bypassed people skip rest, availability, and overlap constraints
  4. Feasible results with no violations produce correct output for draft creation
  5. Timed-out results with partial valid assignments support draft creation with timed_out status

## Key decisions
- Used observation-first methodology: read the engine/constraints code to understand actual behavior, then wrote tests asserting that behavior
- Tests use Hypothesis for property-based testing with constrained input generation
- Tested at the solver level (calling `solve()` directly) rather than mocking internals
- For timed-out behavior, tested the output model contract since forcing actual timeouts in unit tests is unreliable
- Used `assume()` to filter inputs that don't meet preconditions (e.g., gap must violate min-rest)

## How it connects
- Depends on: Task 1 (bug condition exploration test) — establishes the bug exists
- Feeds into: Tasks 3.1–3.4 (fix implementation) — these tests must continue passing after the fix
- Re-verified in: Task 3.6 — preservation tests re-run after fix to confirm no regressions

## How to run / verify
```bash
cd apps/solver
python -m pytest tests/test_preservation_constraints.py -v
```
All 8 tests should pass (confirmed on unfixed code).

## What comes next
- Task 3: Implement the actual fix (min-rest fallback, weight reduction, concurrent-leave cap, post-solve validation)
- Task 3.6: Re-run these preservation tests to confirm no regressions

## Git commit
```bash
git add -A && git commit -m "feat(solver): add preservation property tests for constraints fix"
```
