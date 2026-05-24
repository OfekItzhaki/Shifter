# 556 — Solver Constraints Bug Exploration Tests

## Phase

Bugfix — Solver Constraints Fix (Exploration)

## Purpose

Write property-based exploration tests that confirm three related bugs exist in the CP-SAT scheduling solver for closed-base groups. These tests encode the expected behavior and will validate the fix once implemented.

## What was built

- `apps/solver/tests/test_bug_condition_constraints.py` — Bug condition exploration tests covering:
  - **Bug 1**: Min-rest fallback resolution allows violations when a misconfigured hard constraint provides an incorrect value (e.g., hours=2 instead of 8)
  - **Bug 2**: Eligibility weight formula `balance × 20` produces `ELIGIBILITY_WEIGHT = 1000 = coverage_weight` for default `balance_value=50`
  - **Bug 3**: Solver returns `feasible=true` with no `hard_conflicts` even when assignments violate expected min-rest for closed-base groups

## Key decisions

- Used Hypothesis property-based testing framework (already in project dependencies)
- Bug 1 test uses a misconfigured hard constraint (`hours=2`) to demonstrate the fallback chain doesn't validate resolved values for closed-base safety
- Bug 2 tests both the direct formula calculation and a property-based test across all balance values (1-100)
- Bug 3 builds on Bug 1's scenario to show that even when violations exist in the output, no `hard_conflicts` are reported

## How it connects

- These tests will PASS after the fix is implemented (tasks 3.1–3.4)
- Task 3.5 re-runs these exact tests to verify the fix works
- The counterexamples documented here guide the fix implementation

## How to run / verify

```bash
cd apps/solver
python -m pytest tests/test_bug_condition_constraints.py -v
```

All 4 tests should FAIL on unfixed code (confirming bugs exist). After the fix, all should PASS.

## Counterexamples found

1. **Bug 1**: Person `soldier-B` assigned to both `slot-guard-night` (00:00-04:00) and `slot-patrol-morning` (07:00-11:00) with only 3h gap. Closed-base min-rest should be 8h but solver used misconfigured 2h from hard constraint.
2. **Bug 2**: `balance_value=50` → `ELIGIBILITY_WEIGHT = 1000 = coverage_weight = 1000`. Solver treats home-leave equally important as mission coverage.
3. **Bug 3**: Solver returned `feasible=true` with 0 `hard_conflicts` despite person having 3h gap (violating expected 8h). No post-solve validation exists.

## What comes next

- Task 2: Write preservation property tests (capture baseline behavior before fix)
- Tasks 3.1–3.4: Implement the fix
- Task 3.5: Re-run these tests to confirm fix works

## Git commit

```bash
git add -A && git commit -m "fix(solver): add bug condition exploration tests for constraints fix"
```
