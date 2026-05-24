# Solver Constraints Fix — Bugfix Design

## Overview

Three related bugs in the CP-SAT scheduling solver for closed-base groups cause broken drafts to reach admins:

1. **Min-rest violation** — The `add_min_rest_constraints` function in `engine.py` falls back to the hard constraint rule `min_rest_hours` when `home_leave_config.min_rest_hours == 0`, but if no such hard constraint exists for the group, `rest_hours` defaults to `8.0` from the fallback `else` branch. However, the actual bug is that the `HomeLeaveConfig.min_rest_hours` field is set to `0` in the database (meaning "use default"), and the engine's fallback logic (`if home_leave_cfg.min_rest_hours > 0`) skips the config value but then relies on a `min_rest_hours` hard constraint rule that may not exist — resulting in `rest_hours = 8.0` from the generic fallback, which is correct in isolation. The real issue is that the solver marks rest as strictly hard (`rest_soft_penalties = None`) but the `long_shift_threshold` in `add_min_rest_constraints` is 24h — meaning shifts under 24h between different locations (e.g., a 3h guard shift ending at 03:00 and a 5h shift starting at 05:00) are correctly constrained as hard. If the solver still produces a 2-hour gap, the root cause is likely in the **post-solve validation** — the solver finds a feasible solution that technically satisfies the model but the constraint was never added for that specific pair (e.g., because the slots are at the same location and treated as a single continuous assignment, or because emergency bypass is incorrectly applied).

2. **Home-leave imbalance** — `ELIGIBILITY_WEIGHT = balance_value × 20 = 1000` equals `coverage_weight` (1000 in `objectives.py`), so the solver treats "send person home" as equally important as "cover a mission slot". Combined with `leave_capacity` being set too high (or not enforced relative to mission needs), this results in 11/20 people at home simultaneously.

3. **Missing infeasibility alert** — The `SolverWorkerService` discards results when `!output.Feasible || assignments == 0 || hasUncoveredSlots`, but it does NOT check for hard constraint violations in a "feasible" result. If CP-SAT returns `FEASIBLE` with assignments that violate min-rest (because the constraint was never added for certain pairs), the worker creates a draft version with broken assignments.

The fix strategy: (a) ensure min-rest constraints are correctly applied for ALL non-long shift pairs in closed-base mode, (b) reduce eligibility weight below coverage weight and add a concurrent-leave cap relative to mission needs, (c) add post-solve validation that detects hard constraint violations and treats them as infeasible.

## Glossary

- **Bug_Condition (C)**: The set of inputs where a closed-base group solver run produces assignments with min-rest violations, excessive concurrent home-leave, or undetected hard constraint violations
- **Property (P)**: The desired behavior — min-rest is always enforced, home-leave never starves missions, and violated constraints trigger infeasibility alerts
- **Preservation**: Existing behavior for non-closed-base groups, long-shift soft penalties, valid feasible results, and emergency bypass logic must remain unchanged
- **`add_min_rest_constraints`**: Function in `solver/constraints.py` that adds pairwise rest constraints between slots
- **`add_home_leave_eligibility_preference`**: Function in `solver/home_leave.py` that penalizes NOT sending eligible people home
- **`ELIGIBILITY_WEIGHT`**: `balance_value × 20` — currently 1000 for default `balance_value=50`
- **`coverage_weight`**: 1000 — penalty for uncovered mission slots (in `objectives.py`)
- **`SolverWorkerService`**: Background service in `Jobuler.Infrastructure` that processes solver results and creates draft versions
- **`rest_soft_penalties`**: When `None`, all rest is hard; when a list, long-shift rest violations are soft penalties

## Bug Details

### Bug Condition

The bug manifests when a closed-base group with `home_leave_config.enabled = true` runs the solver and one or more of the following conditions hold:

1. Two non-long shifts (< 24h each) have a gap smaller than `min_rest_hours` but the constraint pair is not added to the model (e.g., due to incorrect `min_rest_hours` resolution when `home_leave_config.min_rest_hours == 0` and no hard constraint rule exists)
2. The eligibility weight equals or exceeds coverage weight, causing the solver to prefer sending people home over covering missions
3. The solver returns `feasible=true` with assignments that violate hard constraints, and the worker creates a draft without post-solve validation

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type SolverInput with home_leave_config.enabled = true
  OUTPUT: boolean
  
  LET rest_hours = resolveMinRestHours(input)
  LET result = solve(input)
  
  RETURN (
    // Bug 1: Rest violation in output
    EXISTS (a1, a2) IN result.assignments WHERE
      a1.person_id == a2.person_id
      AND gap(a1.slot.ends_at, a2.slot.starts_at) < rest_hours * 3600
      AND a1.person_id NOT IN emergency_person_ids
    
    OR
    
    // Bug 2: Home-leave starves missions
    COUNT(result.home_leave_assignments concurrent at time T) > 
      len(input.people) - totalMissionHeadcountAt(T)
    AND result.uncovered_slot_ids IS NOT EMPTY
    
    OR
    
    // Bug 3: Feasible result with hard constraint violations
    result.feasible == true
    AND hasHardConstraintViolations(result.assignments, input)
    AND draftVersionCreated(result)
  )
END FUNCTION
```

### Examples

- **Rest violation**: Group of 20 soldiers, `home_leave_config.min_rest_hours = 0` (meaning use default 8h), no `min_rest_hours` hard constraint rule configured. Soldier A assigned to guard 00:00–03:00 and patrol 05:00–08:00 — only 2h gap instead of 8h minimum.
- **Home-leave imbalance**: 20 soldiers, `balance_value = 50` → `ELIGIBILITY_WEIGHT = 1000 = coverage_weight`. 11 soldiers sent home simultaneously, 4 on mission, 5 idle in base. Missions understaffed because solver sees no penalty difference between covering a mission and sending someone home.
- **Missing alert**: Solver returns `feasible=true` with 45 assignments including 2 that violate min-rest. Worker creates draft version, admin sees broken schedule with no warning.
- **Edge case (correct)**: `balance_value = 0` → `ELIGIBILITY_WEIGHT = 0`, home-leave preference disabled entirely. All missions covered. No bug.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Non-closed-base groups (home_leave_config disabled/absent) continue using existing soft/hard rest logic based on `long_shift_threshold`
- Long shifts (≥ 24h) in non-closed-base groups continue using soft penalty path when `soft_penalties` list is provided
- Emergency-bypassed people continue skipping all rest, availability, and overlap constraints
- Fully feasible solutions with no violations continue creating draft versions normally
- Timed-out results with partial valid assignments (no hard violations) continue creating drafts marked as `timed_out`
- Existing home-leave capacity constraint (`leave_capacity`) continues to be enforced per-hour
- People currently at home (`presence_window.state = "at_home"`) continue to have highest-priority preservation of their leave
- Stability weights, fairness objectives, and burden-level penalties remain unchanged

**Scope:**
All inputs where `home_leave_config` is disabled or absent, and all inputs where the solver produces a valid result with no hard constraint violations, should be completely unaffected by this fix.

## Hypothesized Root Cause

Based on code analysis, the most likely issues are:

1. **Min-rest resolution when `min_rest_hours == 0`**: In `engine.py` lines 108-115, when `home_leave_cfg.min_rest_hours == 0`, the code falls through to the generic fallback which looks for a `min_rest_hours` hard constraint rule. If none exists, it defaults to `8.0`. This is actually correct — the real issue may be that the constraint IS added with 8h but the solver finds a way around it (unlikely with CP-SAT hard constraints). More likely: the `min_rest_hours` field in `HomeLeaveConfig` is being set to a non-zero but incorrect value (e.g., `2`) in the database, or the hard constraint rule has `hours: 2` in its payload. Need to verify the actual data path.

2. **Eligibility weight equals coverage weight**: In `home_leave.py` line ~280, `ELIGIBILITY_WEIGHT = balance * 20` with default `balance=50` gives 1000. The `coverage_weight` in `objectives.py` is also 1000. This means the solver sees no net benefit to covering a mission over sending someone home — it's a zero-sum trade-off. The fix is to cap `ELIGIBILITY_WEIGHT` below `coverage_weight`.

3. **No concurrent-leave cap relative to missions**: The `leave_capacity` constraint caps absolute concurrent leave but doesn't consider how many people are needed for missions at the same time. If `leave_capacity = 15` but only 9 people are needed for missions, 11 can go home — but if missions need 16 people, only 4 should go home.

4. **No post-solve validation**: `SolverWorkerService.ProcessNextJobAsync` checks `output.Feasible` and `output.UncoveredSlotIds` but never validates that assignments actually satisfy hard constraints. CP-SAT should guarantee this, but if constraints were never added to the model (due to bug 1), the "feasible" result is technically correct from the solver's perspective but violates the business rules.

## Correctness Properties

Property 1: Bug Condition - Min-Rest Enforcement for Closed-Base Groups

_For any_ solver input where `home_leave_config.enabled = true` and two non-long shifts (< 24h each) assigned to the same non-emergency person have a gap smaller than the resolved `min_rest_hours`, the solver SHALL NOT assign that person to both shifts — the hard constraint must prevent this assignment combination.

**Validates: Requirements 2.1, 2.2**

Property 2: Bug Condition - Home-Leave Weight Below Coverage

_For any_ solver input where `home_leave_config.enabled = true` and `balance_value` is at its default (50), the eligibility weight used for NEW home-leave assignments SHALL be strictly less than the coverage weight (1000), ensuring mission coverage is always prioritized over sending additional people home.

**Validates: Requirements 2.3, 2.5**

Property 3: Bug Condition - Post-Solve Validation Rejects Violations

_For any_ solver output where `feasible = true` but assignments contain hard constraint violations (min-rest, qualification mismatch, availability conflict), the system SHALL NOT create a draft version and SHALL notify the admin with details of which constraints were violated.

**Validates: Requirements 2.4**

Property 4: Preservation - Non-Closed-Base Rest Logic Unchanged

_For any_ solver input where `home_leave_config` is disabled or absent, the rest constraint logic SHALL produce the same model constraints as the original code — including soft penalties for long shifts (≥ 24h) when `soft_penalties` is not None.

**Validates: Requirements 3.1, 3.2**

Property 5: Preservation - Valid Feasible Results Create Drafts

_For any_ solver output where `feasible = true`, all assignments satisfy hard constraints, and no slots are uncovered, the system SHALL create a draft version exactly as before.

**Validates: Requirements 3.3, 3.6**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `apps/solver/solver/engine.py`

**Function**: `solve` (min-rest resolution block)

**Specific Changes**:
1. **Fix min-rest fallback for `min_rest_hours == 0`**: When `home_leave_cfg.min_rest_hours == 0`, explicitly fall back to the hard constraint rule value, and if that also doesn't exist, use `8.0` as the absolute default. Add a log warning when the fallback is used so admins can configure it explicitly.
   - Current: `if home_leave_cfg.min_rest_hours > 0: rest_hours = home_leave_cfg.min_rest_hours` — else falls through to generic logic which may or may not find a hard constraint
   - Fixed: Make the fallback chain explicit and guaranteed: `config value > hard constraint rule > 8.0 default`

2. **Add post-solve validation function**: After `solver.solve(model)` returns FEASIBLE/OPTIMAL, validate all assignments against hard constraints (min-rest, qualifications, roles, availability, overlap). If any violation is found, set `feasible = False` and populate `hard_conflicts` with the specific violations.

---

**File**: `apps/solver/solver/home_leave.py`

**Function**: `add_home_leave_eligibility_preference`

**Specific Changes**:
3. **Reduce eligibility weight formula**: Change from `balance × 20` to `balance × 10`, capping at `coverage_weight - 1` (i.e., max 999). This ensures mission coverage always wins over new home-leave assignments. For `balance_value=50`, weight becomes 500 instead of 1000.
   - Exception: People already at home (presence_window state = "at_home") retain highest priority via the existing availability constraint (they're blocked from missions anyway).

4. **Add dynamic concurrent-leave cap**: Add a new constraint that limits concurrent home-leave to `len(people) - max_concurrent_mission_headcount`. This ensures enough people are always available for missions regardless of `leave_capacity` setting.

---

**File**: `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs`

**Function**: `ProcessNextJobAsync`

**Specific Changes**:
5. **Add post-solve hard constraint validation**: Before creating a draft version, validate the solver output assignments against the input constraints. Check for:
   - Min-rest violations between assignments for the same person
   - Qualification/role mismatches
   - Availability conflicts
   If violations are found, treat as infeasible: don't create a draft, mark run as failed, notify admin with violation details.

6. **Include violation details in notification**: When infeasibility is detected (either from solver or post-solve validation), include specific constraint names, affected people, and actionable guidance in the admin notification.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write tests that construct `SolverInput` payloads for closed-base groups with specific shift configurations and run them through the `solve()` function. Assert that the output violates expected invariants. Run these tests on the UNFIXED code to observe failures.

**Test Cases**:
1. **Rest Violation Test**: Create two 4h shifts with a 2h gap for a closed-base group with `min_rest_hours=0` and no hard constraint rule. Verify the solver assigns the same person to both (will demonstrate bug on unfixed code).
2. **Weight Equality Test**: Create a scenario with 20 people, 10 mission slots, and `balance_value=50`. Verify that >50% of people are sent home while missions are uncovered (will demonstrate bug on unfixed code).
3. **No Validation Test**: Manually construct a `SolverOutput` with feasible=true but containing rest violations. Pass through the worker logic and verify a draft is created (will demonstrate bug on unfixed code).
4. **Fallback Chain Test**: Set `home_leave_config.min_rest_hours=0` with no hard constraint rule. Verify the resolved rest_hours value and whether constraints are actually added to the model.

**Expected Counterexamples**:
- Person assigned to two shifts with < 8h gap in closed-base mode
- 11/20 people on home-leave while missions have uncovered slots
- Draft version created with assignments that violate min-rest
- Possible causes: incorrect weight balance, missing constraint pairs, no post-solve validation

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := solve_fixed(input)
  
  // Property 1: No rest violations in output
  FOR ALL (a1, a2) IN result.assignments WHERE a1.person_id == a2.person_id DO
    ASSERT gap(a1, a2) >= resolved_min_rest_hours OR person IN emergency_ids
  END FOR
  
  // Property 2: Home-leave doesn't starve missions
  ASSERT ELIGIBILITY_WEIGHT < coverage_weight
  ASSERT concurrent_leave_at_any_T <= len(people) - mission_headcount_at_T
  
  // Property 3: Violations detected and reported
  IF hasViolations(result) THEN
    ASSERT result.feasible == false
    ASSERT draftNotCreated(result)
    ASSERT adminNotified(result)
  END IF
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT solve_original(input).assignments == solve_fixed(input).assignments
  ASSERT solve_original(input).feasible == solve_fixed(input).feasible
  ASSERT solve_original(input).home_leave_assignments == solve_fixed(input).home_leave_assignments
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many solver input configurations automatically across the input domain
- It catches edge cases in the weight/constraint logic that manual tests might miss
- It provides strong guarantees that non-closed-base behavior is unchanged

**Test Plan**: Observe behavior on UNFIXED code first for non-closed-base groups and valid closed-base scenarios, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Non-Closed-Base Preservation**: Generate random `SolverInput` with `home_leave_config=None`. Verify rest constraint logic produces identical model constraints before and after fix.
2. **Long-Shift Soft Penalty Preservation**: Generate inputs with shifts ≥ 24h and `soft_penalties` list. Verify soft penalty path is still used for non-closed-base groups.
3. **Emergency Bypass Preservation**: Generate inputs with emergency constraints. Verify bypassed people still skip all constraint checks.
4. **Valid Feasible Draft Creation**: Generate solver outputs with `feasible=true` and no violations. Verify draft versions are still created normally.

### Unit Tests

- Test `resolveMinRestHours` fallback chain: config value → hard constraint → 8.0 default
- Test eligibility weight calculation with various `balance_value` inputs (0, 25, 50, 75, 100)
- Test concurrent-leave cap calculation: `len(people) - max_mission_headcount`
- Test post-solve validation function: detect rest violations, qualification mismatches, availability conflicts
- Test that emergency-bypassed people are excluded from post-solve validation

### Property-Based Tests

- Generate random closed-base `SolverInput` with varying `min_rest_hours`, shift gaps, and people counts. Verify no output assignment violates min-rest for non-emergency people.
- Generate random `balance_value` (0–100) and verify `ELIGIBILITY_WEIGHT < coverage_weight` always holds (except when `balance_value > 50` which is an explicit admin choice to prioritize leave).
- Generate random solver outputs with and without violations. Verify post-solve validation correctly classifies them.
- Generate random non-closed-base inputs. Verify the fix produces identical constraint sets as the original code.

### Integration Tests

- End-to-end test: trigger solver for a closed-base group with tight scheduling. Verify no rest violations in the resulting draft.
- End-to-end test: trigger solver with `balance_value=50` and verify missions are fully covered before home-leave is assigned.
- End-to-end test: inject a solver output with violations into the worker pipeline. Verify no draft is created and admin receives notification with violation details.
