# Implementation Plan: Solver Constraints Fix

## Overview

This plan fixes three related bugs in the CP-SAT scheduling solver for closed-base groups: (1) min-rest constraint violations due to incorrect fallback resolution, (2) home-leave weight imbalance where eligibility weight equals coverage weight causing understaffing, and (3) missing post-solve validation that allows broken drafts to reach admins. The fix follows the exploratory bugfix workflow: write tests to confirm bugs exist, write preservation tests to capture baseline behavior, then implement the fix and verify.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Min-Rest Violation & Home-Leave Imbalance in Closed-Base Solver
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the three related bugs exist
  - **Scoped PBT Approach**: Scope the property to concrete failing cases for each bug:
    - Bug 1: Two non-long shifts (< 24h) with gap < min_rest_hours assigned to same non-emergency person in closed-base group with `home_leave_config.min_rest_hours = 0` and no hard constraint rule
    - Bug 2: `balance_value = 50` producing `ELIGIBILITY_WEIGHT = 1000 = coverage_weight`, causing > 50% concurrent home-leave while missions are uncovered
    - Bug 3: Solver output with `feasible = true` containing rest violations still creates a draft version
  - Test file: `apps/solver/tests/test_bug_condition_constraints.py`
  - Test that for a closed-base group with `home_leave_config.enabled = true`:
    - `isBugCondition(input)` where two 4h shifts have a 2h gap → solver assigns same person to both (violates 8h min-rest)
    - `isBugCondition(input)` where `balance_value = 50` → `ELIGIBILITY_WEIGHT = 1000 = coverage_weight` → 11/20 people sent home while missions uncovered
    - `isBugCondition(input)` where feasible result with violations → draft created without validation
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (this is correct - it proves the bugs exist)
  - Document counterexamples found:
    - e.g., "Person A assigned guard 00:00–03:00 and patrol 05:00–08:00 with only 2h gap"
    - e.g., "11/20 people on home-leave, 4 mission slots uncovered, ELIGIBILITY_WEIGHT = coverage_weight = 1000"
    - e.g., "Draft version created with 2 min-rest violations, no admin notification"
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Non-Closed-Base Rest Logic & Valid Draft Creation Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Observe behavior on UNFIXED code for non-buggy inputs (cases where `isBugCondition` returns false):
    - Non-closed-base groups with `home_leave_config` disabled/absent
    - Long shifts (≥ 24h) with `soft_penalties` list in non-closed-base groups
    - Emergency-bypassed people skipping all constraints
    - Fully feasible solutions with no violations creating draft versions normally
    - Timed-out results with partial valid assignments creating drafts marked as `timed_out`
  - Test file: `apps/solver/tests/test_preservation_constraints.py`
  - Observe: Non-closed-base group with `home_leave_config=None` → rest constraints use existing soft/hard logic based on `long_shift_threshold`
  - Observe: Long shift (≥ 24h) with `soft_penalties` list → soft penalty path applied
  - Observe: Emergency-bypassed person → skips rest, availability, overlap constraints
  - Observe: Feasible result with no violations → draft version created normally
  - Observe: Timed-out result with partial valid assignments → draft created, marked `timed_out`
  - Write property-based tests:
    - For all inputs where `home_leave_config` is disabled/absent, rest constraint logic produces identical model constraints before and after fix
    - For all long shifts (≥ 24h) in non-closed-base groups with `soft_penalties` not None, soft penalty path is used
    - For all emergency-bypassed people, constraint checks are skipped
    - For all solver outputs with `feasible=true` and no hard constraint violations, draft versions are created
    - For all timed-out results with partial valid assignments and no violations, drafts are created with `timed_out` status
  - Verify tests PASS on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.5, 3.6_

- [x] 3. Fix for min-rest constraint violation, home-leave weight imbalance, and missing infeasibility alert

  - [x] 3.1 Fix min-rest fallback resolution in engine.py
    - File: `apps/solver/solver/engine.py`
    - Make the fallback chain explicit and guaranteed: `config value > hard constraint rule > 8.0 default`
    - When `home_leave_cfg.min_rest_hours == 0`, explicitly fall back to the hard constraint rule value
    - If no hard constraint rule exists, use `8.0` as the absolute default
    - Add a log warning when the fallback is used so admins can configure it explicitly
    - Ensure min-rest constraints are correctly applied for ALL non-long shift pairs in closed-base mode
    - _Bug_Condition: isBugCondition(input) where home_leave_config.min_rest_hours == 0 AND no hard constraint rule exists → rest_hours resolution fails_
    - _Expected_Behavior: resolveMinRestHours always returns a valid value via chain: config value > hard constraint rule > 8.0 default_
    - _Preservation: Non-closed-base groups continue using existing soft/hard logic based on long_shift_threshold_
    - _Requirements: 2.1, 2.2, 3.1, 3.2_

  - [x] 3.2 Reduce home-leave eligibility weight in home_leave.py
    - File: `apps/solver/solver/home_leave.py`
    - Change eligibility weight formula from `balance × 20` to `balance × 10`
    - Cap at `coverage_weight - 1` (max 999) to ensure mission coverage always wins
    - For `balance_value=50`, weight becomes 500 instead of 1000
    - People already at home (`presence_window.state = "at_home"`) retain highest priority via existing availability constraint
    - _Bug_Condition: isBugCondition(input) where ELIGIBILITY_WEIGHT = balance × 20 = 1000 = coverage_weight_
    - _Expected_Behavior: ELIGIBILITY_WEIGHT = min(balance × 10, coverage_weight - 1) < coverage_weight always_
    - _Preservation: People currently at home retain highest-priority preservation; stability weights and fairness objectives unchanged_
    - _Requirements: 2.3, 2.5, 3.4, 3.7_

  - [x] 3.3 Add dynamic concurrent-leave cap in home_leave.py
    - File: `apps/solver/solver/home_leave.py`
    - Add constraint: concurrent home-leave ≤ `len(people) - max_concurrent_mission_headcount`
    - Ensures enough people are always available for missions regardless of `leave_capacity` setting
    - Existing `leave_capacity` constraint continues to be enforced per-hour as before
    - _Bug_Condition: isBugCondition(input) where concurrent_leave > len(people) - totalMissionHeadcountAt(T) AND uncovered slots exist_
    - _Expected_Behavior: concurrent_leave_at_any_T <= len(people) - mission_headcount_at_T_
    - _Preservation: Existing leave_capacity constraint unchanged; people already at home retain priority_
    - _Requirements: 2.5, 3.7_

  - [x] 3.4 Add post-solve hard constraint validation in SolverWorkerService.cs
    - File: `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs`
    - Before creating a draft version, validate solver output assignments against input constraints
    - Check for: min-rest violations between assignments for same person, qualification/role mismatches, availability conflicts
    - If violations found: set feasible = false, don't create draft, mark run as failed
    - Include violation details in admin notification: constraint names, affected people, actionable guidance
    - _Bug_Condition: isBugCondition(input) where result.feasible == true AND hasHardConstraintViolations(result) AND draftVersionCreated(result)_
    - _Expected_Behavior: violations detected → feasible = false, no draft created, admin notified with details_
    - _Preservation: Fully feasible solutions with no violations continue creating drafts normally; timed-out partial results with no violations continue creating drafts_
    - _Requirements: 2.4, 3.3, 3.6_

  - [x] 3.5 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Min-Rest Enforced, Weight Below Coverage, Violations Rejected
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms:
      - Min-rest is always enforced for non-emergency people in closed-base groups
      - ELIGIBILITY_WEIGHT < coverage_weight for all valid balance_value inputs
      - Post-solve validation catches hard constraint violations and prevents draft creation
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms all three bugs are fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 3.6 Verify preservation tests still pass
    - **Property 2: Preservation** - Non-Closed-Base Rest Logic & Valid Draft Creation Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix:
      - Non-closed-base rest logic unchanged
      - Long-shift soft penalties unchanged
      - Emergency bypass unchanged
      - Valid feasible results still create drafts
      - Timed-out partial results still create drafts

- [x] 4. Checkpoint - Ensure all tests pass
  - Run full test suite for solver module: `apps/solver/tests/`
  - Run relevant C# tests for SolverWorkerService validation
  - Verify no regressions in existing solver behavior
  - Ensure all property-based tests pass with sufficient iterations
  - Ask the user if questions arise

## Notes

- Property-based tests use **Hypothesis** for Python (solver) and **xUnit** for C# (SolverWorkerService)
- The exploration test (task 1) is expected to FAIL on unfixed code — this confirms the bug exists
- The preservation test (task 2) is expected to PASS on unfixed code — this captures baseline behavior
- After the fix (tasks 3.5, 3.6), both test suites should PASS
- Emergency-bypassed people are excluded from all constraint checks including post-solve validation
- The `leave_capacity` per-hour constraint remains unchanged; the new concurrent-leave cap is additive
- People already at home (`presence_window.state = "at_home"`) retain highest priority and are not affected by weight reduction
- Step documentation under `docs/steps/` should be created alongside each implementation task per workspace conventions

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1"] },
    { "id": 1, "tasks": ["2"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4"] },
    { "id": 3, "tasks": ["3.5", "3.6"] },
    { "id": 4, "tasks": ["4"] }
  ]
}
```
