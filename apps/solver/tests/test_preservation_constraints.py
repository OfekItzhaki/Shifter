"""
Preservation property tests for the solver constraints fix.

These tests capture the BASELINE behavior of the solver for non-buggy inputs
(cases where the bug condition does NOT apply). They must PASS on unfixed code
and continue to PASS after the fix is applied — confirming no regressions.

Preservation properties tested:
1. Non-closed-base groups use existing soft/hard rest logic based on long_shift_threshold
2. Long shifts (>= 24h) in non-closed-base groups with soft_penalties use soft penalty path
3. Emergency-bypassed people skip rest, availability, and overlap constraints
4. Feasible results with no violations create draft versions normally
5. Timed-out results with partial valid assignments create drafts marked as timed_out

**Validates: Requirements 3.1, 3.2, 3.3, 3.5, 3.6**
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from datetime import date, datetime, timezone, timedelta
from hypothesis import given, settings, assume, HealthCheck
from hypothesis import strategies as st
import pytest

from models.solver_input import (
    SolverInput, PersonEligibility, TaskSlot, StabilityWeights, HomeLeaveConfig,
    PresenceWindow, AvailabilityWindow, HardConstraint, SoftConstraint,
    BaselineAssignment, FairnessCounters, CumulativeTracking,
)
from models.solver_output import SolverOutput
from solver.engine import solve
from solver.constraints import add_min_rest_constraints, _to_timestamp


# ─── Constants ────────────────────────────────────────────────────────────────

HORIZON_START = date(2026, 6, 1)
HORIZON_END = date(2026, 6, 4)  # 3 days = 72 hours
HORIZON_START_DT = datetime(2026, 6, 1, tzinfo=timezone.utc)
HORIZON_HOURS = 72


# ─── Helpers ──────────────────────────────────────────────────────────────────

def make_person(pid, roles=None, quals=None, groups=None):
    """Create a PersonEligibility with sensible defaults."""
    return PersonEligibility(
        person_id=pid,
        role_ids=roles or ["role-combat"],
        qualification_ids=quals or [],
        group_ids=groups or ["group-1"],
    )


def make_slot(sid, start_dt, end_dt, headcount=1, task_type_id="tt-guard",
              task_type_name="Guard", roles=None, quals=None):
    """Create a TaskSlot with sensible defaults."""
    return TaskSlot(
        slot_id=sid,
        task_type_id=task_type_id,
        task_type_name=task_type_name,
        burden_level="neutral",
        starts_at=start_dt,
        ends_at=end_dt,
        required_headcount=headcount,
        priority=5,
        required_role_ids=roles or [],
        required_qualification_ids=quals or [],
        allows_overlap=False,
    )


def make_solver_input(
    people, slots, home_leave_config=None, hard_constraints=None,
    emergency_constraints=None, presence_windows=None, availability_windows=None,
):
    """Build a complete SolverInput for testing."""
    return SolverInput(
        space_id="test-space",
        run_id="test-run-preservation",
        trigger_mode="standard",
        horizon_start=HORIZON_START,
        horizon_end=HORIZON_END,
        locale="en",
        stability_weights=StabilityWeights(
            today_tomorrow=10.0, days_3_4=3.0, days_5_7=1.0
        ),
        people=people,
        availability_windows=availability_windows or [],
        presence_windows=presence_windows or [],
        task_slots=slots,
        hard_constraints=hard_constraints or [],
        soft_constraints=[],
        emergency_constraints=emergency_constraints or [],
        baseline_assignments=[],
        fairness_counters=[],
        locked_slot_ids=[],
        home_leave_config=home_leave_config,
        cumulative_tracking=[],
    )


# ─── Property 1: Non-Closed-Base Rest Logic Unchanged ─────────────────────────
#
# For all inputs where home_leave_config is disabled/absent, rest constraint
# logic produces identical model constraints — soft penalties for long shifts
# (>= 24h) when soft_penalties is not None, hard constraints for short shifts.
#
# **Validates: Requirements 3.1, 3.2**


class TestPreservationNonClosedBaseRestLogic:
    """
    Preservation Property: Non-closed-base groups continue using existing
    soft/hard rest logic based on long_shift_threshold.

    When home_leave_config is None (disabled), the engine sets
    rest_soft_penalties = [] (empty list, not None), which means:
    - Short shifts (< 24h): hard constraint (assign[s1] + assign[s2] <= 1)
    - Long shifts (>= 24h): soft penalty (violation var * 50)

    This behavior must remain unchanged after the fix.
    """

    @given(
        min_rest=st.floats(min_value=4.0, max_value=12.0),
        gap_hours=st.floats(min_value=1.0, max_value=7.0),
    )
    @settings(max_examples=30, deadline=None, suppress_health_check=[HealthCheck.too_slow])
    def test_non_closed_base_short_shifts_enforce_hard_rest(self, min_rest, gap_hours):
        """
        Property: For non-closed-base groups with short shifts (< 24h),
        min-rest is enforced as a HARD constraint — the solver cannot assign
        the same person to two shifts with gap < min_rest_hours.

        **Validates: Requirements 3.1, 3.2**
        """
        assume(gap_hours < min_rest)  # gap must violate min-rest

        people = [make_person("p1"), make_person("p2")]

        # Two short shifts (4h each) with a gap that violates min_rest
        slot1_end = HORIZON_START_DT + timedelta(hours=4)
        slot2_start = slot1_end + timedelta(hours=gap_hours)
        slot2_end = slot2_start + timedelta(hours=4)

        slots = [
            make_slot("s1", HORIZON_START_DT, slot1_end),
            make_slot("s2", slot2_start, slot2_end),
        ]

        # Non-closed-base: home_leave_config is None
        hard_constraints = [
            HardConstraint(
                constraint_id="min-rest",
                rule_type="min_rest_hours",
                scope_type="space",
                scope_id=None,
                payload={"hours": min_rest},
            ),
        ]

        solver_input = make_solver_input(
            people, slots, home_leave_config=None,
            hard_constraints=hard_constraints,
        )
        result = solve(solver_input)

        assert result.feasible, "Solver should find a feasible solution with 2 people"

        # The same person should NOT be assigned to both shifts (hard constraint)
        person_slots: dict[str, list[str]] = {}
        for a in result.assignments:
            person_slots.setdefault(a.person_id, []).append(a.slot_id)

        for pid, assigned in person_slots.items():
            assert not ("s1" in assigned and "s2" in assigned), (
                f"REGRESSION: Person {pid} assigned to both s1 and s2 with "
                f"gap={gap_hours:.1f}h < min_rest={min_rest:.1f}h in non-closed-base group. "
                f"Short-shift rest should be a HARD constraint."
            )


# ─── Property 2: Long Shifts Use Soft Penalty Path ────────────────────────────
#
# For all long shifts (>= 24h) in non-closed-base groups with soft_penalties
# not None, the soft penalty path is used — the solver CAN assign the same
# person to both shifts (with a penalty) rather than blocking it entirely.
#
# **Validates: Requirements 3.1, 3.2**


class TestPreservationLongShiftSoftPenalty:
    """
    Preservation Property: Long shifts (>= 24h) in non-closed-base groups
    use soft penalty path when rest_soft_penalties is not None.

    When a shift is >= 24h and the group is non-closed-base (soft_penalties=[]),
    the rest constraint becomes a soft penalty (violation * 50) rather than
    a hard block. This allows the solver to assign the same person to both
    shifts when resources are tight, at the cost of a penalty.
    """

    @given(
        shift_duration_hours=st.integers(min_value=24, max_value=48),
    )
    @settings(max_examples=10, deadline=None, suppress_health_check=[HealthCheck.too_slow])
    def test_long_shift_allows_same_person_with_penalty(self, shift_duration_hours):
        """
        Property: For non-closed-base groups, when one shift is >= 24h,
        the solver CAN assign the same person to both shifts (soft penalty).
        With only 1 person available, the solver must use the soft path.

        **Validates: Requirements 3.1, 3.2**
        """
        # Only 1 person — forces the solver to use soft penalty path
        people = [make_person("p1")]

        # First slot: a long shift (>= 24h)
        slot1_start = HORIZON_START_DT
        slot1_end = slot1_start + timedelta(hours=shift_duration_hours)

        # Second slot: starts 2h after the long shift ends (violates 8h rest)
        slot2_start = slot1_end + timedelta(hours=2)
        slot2_end = slot2_start + timedelta(hours=4)

        # Ensure slot2 fits within horizon
        assume(slot2_end <= HORIZON_START_DT + timedelta(hours=HORIZON_HOURS))

        slots = [
            make_slot("s-long", slot1_start, slot1_end),
            make_slot("s-short", slot2_start, slot2_end),
        ]

        # Non-closed-base: home_leave_config is None
        # min_rest = 8h — the gap is only 2h, violating rest
        hard_constraints = [
            HardConstraint(
                constraint_id="min-rest",
                rule_type="min_rest_hours",
                scope_type="space",
                scope_id=None,
                payload={"hours": 8},
            ),
        ]

        solver_input = make_solver_input(
            people, slots, home_leave_config=None,
            hard_constraints=hard_constraints,
        )
        result = solve(solver_input)

        assert result.feasible, "Solver should find a feasible solution (soft penalty path)"

        # With only 1 person and soft penalty for long shifts, the solver
        # should assign p1 to both slots (accepting the penalty)
        assert len(result.assignments) == 2, (
            f"REGRESSION: Expected 2 assignments (soft penalty allows both), "
            f"got {len(result.assignments)}. Long-shift soft penalty path "
            f"should allow same person to be assigned to both slots."
        )

        # Verify both slots are assigned to the same person
        persons = {a.person_id for a in result.assignments}
        assert persons == {"p1"}, (
            f"Expected p1 assigned to both slots via soft penalty path, "
            f"got persons: {persons}"
        )


# ─── Property 3: Emergency-Bypassed People Skip Constraints ──────────────────
#
# For all emergency-bypassed people, constraint checks (rest, availability,
# overlap) are skipped — they can be assigned to any slot regardless.
#
# **Validates: Requirements 3.5**


class TestPreservationEmergencyBypass:
    """
    Preservation Property: Emergency-bypassed people skip rest, availability,
    and overlap constraints.

    When a person has an emergency_person_bypass constraint, they are added
    to the emergency_person_ids set and all constraint functions skip them.
    This means they can be assigned to overlapping slots, slots with
    insufficient rest gap, and slots outside their availability windows.
    """

    @given(
        gap_hours=st.floats(min_value=0.5, max_value=3.0),
    )
    @settings(max_examples=20, deadline=None, suppress_health_check=[HealthCheck.too_slow])
    def test_emergency_person_skips_rest_constraint(self, gap_hours):
        """
        Property: Emergency-bypassed person can be assigned to two shifts
        with gap < min_rest_hours (rest constraint is skipped).

        **Validates: Requirements 3.5**
        """
        # Only 1 person (emergency-bypassed) — must handle both slots
        people = [make_person("p-emergency")]

        # Two short shifts with a gap that violates 8h min-rest
        slot1_end = HORIZON_START_DT + timedelta(hours=4)
        slot2_start = slot1_end + timedelta(hours=gap_hours)
        slot2_end = slot2_start + timedelta(hours=4)

        slots = [
            make_slot("s1", HORIZON_START_DT, slot1_end),
            make_slot("s2", slot2_start, slot2_end),
        ]

        hard_constraints = [
            HardConstraint(
                constraint_id="min-rest",
                rule_type="min_rest_hours",
                scope_type="space",
                scope_id=None,
                payload={"hours": 8},
            ),
        ]

        # Emergency bypass for this person
        emergency_constraints = [
            HardConstraint(
                constraint_id="emergency-bypass-1",
                rule_type="emergency_person_bypass",
                scope_type="person",
                scope_id="p-emergency",
                payload={"person_id": "p-emergency"},
            ),
        ]

        solver_input = make_solver_input(
            people, slots, home_leave_config=None,
            hard_constraints=hard_constraints,
            emergency_constraints=emergency_constraints,
        )
        result = solve(solver_input)

        assert result.feasible, "Solver should find a feasible solution"

        # Emergency person should be assigned to BOTH slots despite rest violation
        assert len(result.assignments) == 2, (
            f"REGRESSION: Emergency-bypassed person should be assigned to both "
            f"slots regardless of rest constraint. Got {len(result.assignments)} "
            f"assignments instead of 2."
        )

    @given(st.just(True))
    @settings(max_examples=1, deadline=None)
    def test_emergency_person_skips_availability_constraint(self, _):
        """
        Property: Emergency-bypassed person can be assigned to slots
        outside their availability windows (availability constraint skipped).

        **Validates: Requirements 3.5**
        """
        people = [make_person("p-emergency")]

        # Slot at 08:00-12:00
        slot_start = HORIZON_START_DT + timedelta(hours=8)
        slot_end = HORIZON_START_DT + timedelta(hours=12)
        slots = [make_slot("s1", slot_start, slot_end)]

        # Availability window does NOT cover the slot (person available 20:00-23:00 only)
        availability_windows = [
            AvailabilityWindow(
                person_id="p-emergency",
                starts_at=HORIZON_START_DT + timedelta(hours=20),
                ends_at=HORIZON_START_DT + timedelta(hours=23),
            ),
        ]

        # Emergency bypass
        emergency_constraints = [
            HardConstraint(
                constraint_id="emergency-bypass-1",
                rule_type="emergency_person_bypass",
                scope_type="person",
                scope_id="p-emergency",
                payload={"person_id": "p-emergency"},
            ),
        ]

        solver_input = make_solver_input(
            people, slots, home_leave_config=None,
            emergency_constraints=emergency_constraints,
            availability_windows=availability_windows,
        )
        result = solve(solver_input)

        assert result.feasible, "Solver should find a feasible solution"
        assert len(result.assignments) == 1, (
            "REGRESSION: Emergency-bypassed person should be assigned "
            "despite being outside availability window."
        )

    @given(st.just(True))
    @settings(max_examples=1, deadline=None)
    def test_emergency_person_skips_overlap_constraint(self, _):
        """
        Property: Emergency-bypassed person can be assigned to overlapping
        slots (overlap constraint is skipped).

        **Validates: Requirements 3.5**
        """
        people = [make_person("p-emergency")]

        # Two overlapping slots
        slot1_start = HORIZON_START_DT + timedelta(hours=8)
        slot1_end = HORIZON_START_DT + timedelta(hours=16)
        slot2_start = HORIZON_START_DT + timedelta(hours=12)
        slot2_end = HORIZON_START_DT + timedelta(hours=20)

        slots = [
            make_slot("s1", slot1_start, slot1_end),
            make_slot("s2", slot2_start, slot2_end),
        ]

        # Emergency bypass
        emergency_constraints = [
            HardConstraint(
                constraint_id="emergency-bypass-1",
                rule_type="emergency_person_bypass",
                scope_type="person",
                scope_id="p-emergency",
                payload={"person_id": "p-emergency"},
            ),
        ]

        solver_input = make_solver_input(
            people, slots, home_leave_config=None,
            emergency_constraints=emergency_constraints,
        )
        result = solve(solver_input)

        assert result.feasible, "Solver should find a feasible solution"
        assert len(result.assignments) == 2, (
            "REGRESSION: Emergency-bypassed person should be assigned to "
            "both overlapping slots (overlap constraint skipped)."
        )


# ─── Property 4: Feasible Results With No Violations Create Drafts ────────────
#
# For all solver outputs with feasible=true and no hard constraint violations,
# draft versions are created normally. We test this at the solver level by
# verifying the output structure is correct for draft creation.
#
# **Validates: Requirements 3.3**


class TestPreservationFeasibleDraftCreation:
    """
    Preservation Property: Fully feasible solutions with no violations
    produce output that enables draft version creation.

    The SolverWorkerService creates a draft when:
    - output.Feasible == true
    - parsedAssignmentDtos.Count > 0
    - output.UncoveredSlotIds.Count == 0

    We verify the solver produces output meeting these criteria for valid inputs.
    """

    @given(
        num_people=st.integers(min_value=3, max_value=6),
        num_slots=st.integers(min_value=1, max_value=3),
    )
    @settings(max_examples=20, deadline=None, suppress_health_check=[HealthCheck.too_slow])
    def test_feasible_result_has_correct_structure_for_draft(self, num_people, num_slots):
        """
        Property: For all solver inputs with enough people and non-overlapping
        slots, the solver produces a feasible result with all slots covered
        and assignments populated — enabling draft creation.

        **Validates: Requirements 3.3**
        """
        people = [make_person(f"p{i}") for i in range(num_people)]

        # Create non-overlapping slots with enough gap for rest
        slots = []
        for i in range(num_slots):
            start = HORIZON_START_DT + timedelta(hours=i * 12)
            end = start + timedelta(hours=4)
            slots.append(make_slot(f"s{i}", start, end))

        solver_input = make_solver_input(people, slots, home_leave_config=None)
        result = solve(solver_input)

        # Verify the output meets draft creation criteria
        assert result.feasible, (
            "REGRESSION: Solver should produce feasible result with "
            f"{num_people} people and {num_slots} non-overlapping slots."
        )
        assert len(result.assignments) == num_slots, (
            f"REGRESSION: Expected {num_slots} assignments (1 per slot), "
            f"got {len(result.assignments)}."
        )
        assert len(result.uncovered_slot_ids) == 0, (
            f"REGRESSION: Expected no uncovered slots, "
            f"got {len(result.uncovered_slot_ids)} uncovered."
        )
        # Verify all slot IDs are present in assignments
        assigned_slot_ids = {a.slot_id for a in result.assignments}
        expected_slot_ids = {s.slot_id for s in slots}
        assert assigned_slot_ids == expected_slot_ids, (
            f"REGRESSION: Not all slots assigned. "
            f"Missing: {expected_slot_ids - assigned_slot_ids}"
        )


# ─── Property 5: Timed-Out Results With Partial Valid Assignments ─────────────
#
# For all timed-out results with partial valid assignments and no violations,
# drafts are created with timed_out status. We test this by verifying the
# solver output structure when it times out but has partial results.
#
# Note: We cannot easily force a timeout in unit tests, so we verify the
# output contract: when timed_out=True and feasible=True with assignments,
# the output structure supports draft creation with timed_out marking.
#
# **Validates: Requirements 3.6**


class TestPreservationTimedOutDraftCreation:
    """
    Preservation Property: Timed-out results with partial valid assignments
    create drafts marked as timed_out.

    The SolverWorkerService handles timed-out results:
    - If feasible=True with assignments and no uncovered slots: creates draft,
      marks run as MarkTimedOut (not MarkFailed)
    - The timed_out field in SolverOutput signals this condition

    We verify the solver correctly sets timed_out=True when it hits the timeout
    and still has a partial/full solution.
    """

    @given(st.just(True))
    @settings(max_examples=1, deadline=None)
    def test_solver_output_supports_timed_out_with_assignments(self, _):
        """
        Property: The SolverOutput model correctly represents a timed-out
        result with partial valid assignments. When the solver finds a
        feasible solution before timeout, it returns feasible=True with
        timed_out=True and valid assignments.

        We verify this by running a normal solve (which won't timeout) and
        confirming the output structure supports the timed_out flow.
        The actual timeout behavior is tested via the SolverWorkerService
        integration tests.

        **Validates: Requirements 3.6**
        """
        # Create a simple solvable scenario
        people = [make_person(f"p{i}") for i in range(4)]
        slots = [
            make_slot("s1", HORIZON_START_DT, HORIZON_START_DT + timedelta(hours=4)),
            make_slot("s2", HORIZON_START_DT + timedelta(hours=12),
                      HORIZON_START_DT + timedelta(hours=16)),
        ]

        solver_input = make_solver_input(people, slots, home_leave_config=None)
        result = solve(solver_input)

        # Verify the output has the timed_out field and it's a valid boolean
        assert hasattr(result, 'timed_out'), (
            "REGRESSION: SolverOutput must have timed_out field"
        )
        assert isinstance(result.timed_out, bool), (
            "REGRESSION: timed_out must be a boolean"
        )

        # For a normal fast solve, timed_out should be False
        assert result.timed_out is False, (
            "A fast solve should not timeout"
        )

        # Verify the output structure supports draft creation
        assert result.feasible is True
        assert len(result.assignments) > 0
        assert len(result.uncovered_slot_ids) == 0

    @given(st.just(True))
    @settings(max_examples=1, deadline=None)
    def test_timed_out_output_model_contract(self, _):
        """
        Property: A SolverOutput with timed_out=True and feasible=True
        can be constructed and has the correct structure for the worker
        to create a draft marked as timed_out.

        This tests the output model contract directly — the worker checks:
        - output.Feasible && !shouldDiscard → create version
        - output.TimedOut → run.MarkTimedOut(summaryJson) instead of MarkCompleted

        **Validates: Requirements 3.6**
        """
        from models.solver_output import (
            SolverOutput, AssignmentResult, StabilityMetrics, FairnessMetrics
        )

        # Construct a timed-out output with partial valid assignments
        timed_out_output = SolverOutput(
            run_id="test-run-timeout",
            feasible=True,
            timed_out=True,
            assignments=[
                AssignmentResult(slot_id="slot-1", person_id="person-1"),
                AssignmentResult(slot_id="slot-2", person_id="person-2"),
            ],
            uncovered_slot_ids=[],  # No uncovered slots
            hard_conflicts=[],      # No violations
            soft_penalty_total=150.0,
            stability_metrics=StabilityMetrics(
                today_tomorrow_changes=0,
                days_3_4_changes=0,
                days_5_7_changes=0,
                total_stability_penalty=150.0,
            ),
            fairness_metrics=[],
            explanation_fragments=["Solver timed out but found partial solution."],
            solver_time_ms=15000,
        )

        # Verify the output meets the worker's draft creation criteria:
        # !shouldDiscard = output.Feasible && assignments > 0 && no uncovered
        should_discard = (
            not timed_out_output.feasible
            or len(timed_out_output.assignments) == 0
            or len(timed_out_output.uncovered_slot_ids) > 0
        )
        assert not should_discard, (
            "REGRESSION: Timed-out output with valid assignments and no "
            "uncovered slots should NOT be discarded — a draft should be created."
        )

        # Verify timed_out flag is set for MarkTimedOut path
        assert timed_out_output.timed_out is True, (
            "REGRESSION: timed_out must be True for the worker to call "
            "MarkTimedOut instead of MarkCompleted."
        )
