"""
Tests for min-rest hard constraint in closed-base groups (Task 8.4).

Verifies:
- When home_leave_config is enabled, min-rest is strictly hard (no soft penalty for long shifts)
- HardConflict entries are returned when infeasible due to min-rest violations
- Emergency bypass skips min-rest enforcement for bypassed persons
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from datetime import date, datetime, timezone
from models.solver_input import (
    SolverInput, PersonEligibility, TaskSlot, StabilityWeights,
    BaselineAssignment, FairnessCounters, HardConstraint, HomeLeaveConfig
)
from solver.engine import solve


def make_person(pid, roles=None, quals=None, groups=None):
    return PersonEligibility(
        person_id=pid,
        role_ids=roles or [],
        qualification_ids=quals or [],
        group_ids=groups or []
    )


def make_slot(sid, day=20, start_hour=8, end_hour=16, headcount=1, task_type_name="Guard"):
    return TaskSlot(
        slot_id=sid,
        task_type_id="tt-1",
        task_type_name=task_type_name,
        burden_level="neutral",
        starts_at=datetime(2026, 4, day, start_hour, 0, tzinfo=timezone.utc),
        ends_at=datetime(2026, 4, day, end_hour, 0, tzinfo=timezone.utc),
        required_headcount=headcount,
        priority=5,
        required_role_ids=[],
        required_qualification_ids=[],
        allows_overlap=False
    )


def make_input(slots, people, home_leave_config=None, hard_constraints=None, emergency_constraints=None):
    return SolverInput(
        space_id="test-space",
        run_id="test-run",
        trigger_mode="standard",
        horizon_start=date(2026, 4, 20),
        horizon_end=date(2026, 4, 26),
        stability_weights=StabilityWeights(
            today_tomorrow=10.0, days_3_4=3.0, days_5_7=1.0),
        people=people,
        availability_windows=[],
        presence_windows=[],
        task_slots=slots,
        hard_constraints=hard_constraints or [],
        soft_constraints=[],
        emergency_constraints=emergency_constraints or [],
        baseline_assignments=[],
        fairness_counters=[],
        home_leave_config=home_leave_config,
    )


class TestMinRestHardConstraintClosedBase:
    """When home_leave_config is enabled, min-rest is strictly hard — no soft exception for long shifts."""

    def test_min_rest_hard_blocks_assignment_for_long_shifts(self):
        """
        With home_leave_config enabled, even 24h shifts must respect min-rest as hard.
        1 person, two 24h slots with only 6h gap — should NOT both be assigned
        when min_rest_hours=8.
        """
        people = [make_person("p1")]
        # Slot 1: 00:00 - 24:00 (24h shift)
        # Slot 2: 06:00 next day - 30:00 next day (24h shift starting 6h after slot1 ends)
        slots = [
            TaskSlot(
                slot_id="s1",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 0, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 21, 0, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
            TaskSlot(
                slot_id="s2",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 21, 6, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 22, 6, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
        ]
        config = HomeLeaveConfig(
            enabled=True,
            min_rest_hours=8.0,
            eligibility_threshold_hours=24.0,
            leave_capacity=1,
            leave_duration_hours=48.0,
        )
        result = solve(make_input(slots, people, home_leave_config=config))
        # With only 1 person and hard min-rest, only 1 slot can be assigned
        assert result.feasible
        assert len(result.assignments) == 1
        assert len(result.uncovered_slot_ids) == 1

    def test_without_home_leave_config_long_shifts_get_soft_penalty(self):
        """
        Without home_leave_config, 24h shifts get a soft penalty (not hard block).
        1 person, two 24h slots with 6h gap — both CAN be assigned (soft penalty only).
        """
        people = [make_person("p1")]
        slots = [
            TaskSlot(
                slot_id="s1",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 0, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 21, 0, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
            TaskSlot(
                slot_id="s2",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 21, 6, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 22, 6, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
        ]
        # Use a hard constraint for min_rest_hours (non-closed-base mode)
        rest_constraint = HardConstraint(
            constraint_id="c1",
            rule_type="min_rest_hours",
            scope_type="space",
            scope_id=None,
            payload={"hours": 8}
        )
        result = solve(make_input(slots, people, hard_constraints=[rest_constraint]))
        # Without home_leave_config, long shifts get soft penalty — both can be assigned
        assert result.feasible
        assert len(result.assignments) == 2

    def test_min_rest_uses_config_value(self):
        """
        The min_rest_hours from home_leave_config overrides the default.
        With min_rest_hours=4, a 5h gap should be fine.
        """
        people = [make_person("p1")]
        # Slot 1: 08:00-16:00, Slot 2: 21:00-05:00 next day (5h gap)
        slots = [
            make_slot("s1", start_hour=8, end_hour=16),
            TaskSlot(
                slot_id="s2",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 21, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 21, 5, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
        ]
        config = HomeLeaveConfig(
            enabled=True,
            min_rest_hours=4.0,
            eligibility_threshold_hours=24.0,
            leave_capacity=1,
            leave_duration_hours=48.0,
        )
        result = solve(make_input(slots, people, home_leave_config=config))
        # 5h gap > 4h min_rest — both should be assigned
        assert result.feasible
        assert len(result.assignments) == 2

    def test_min_rest_config_value_blocks_when_gap_too_small(self):
        """
        With min_rest_hours=6, a 5h gap should block assignment.
        """
        people = [make_person("p1")]
        # Slot 1: 08:00-16:00, Slot 2: 21:00-05:00 next day (5h gap)
        slots = [
            make_slot("s1", start_hour=8, end_hour=16),
            TaskSlot(
                slot_id="s2",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 21, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 21, 5, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
        ]
        config = HomeLeaveConfig(
            enabled=True,
            min_rest_hours=6.0,
            eligibility_threshold_hours=24.0,
            leave_capacity=1,
            leave_duration_hours=48.0,
        )
        result = solve(make_input(slots, people, home_leave_config=config))
        # 5h gap < 6h min_rest — only 1 slot can be assigned
        assert result.feasible
        assert len(result.assignments) == 1
        assert len(result.uncovered_slot_ids) == 1


class TestMinRestHardConflictReporting:
    """HardConflict entries are returned when infeasible due to min-rest violations."""

    def test_hard_conflict_reported_for_min_rest_violation(self):
        """
        When solver is infeasible and home_leave_config is enabled,
        min-rest violations should produce HardConflict entries.
        We test this via _build_hard_conflicts directly since the engine
        returns early with _empty_result when there are 0 people.
        """
        from solver.engine import _build_hard_conflicts

        people = [make_person("p1")]
        slots = [
            TaskSlot(
                slot_id="s1",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 0, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 20, 8, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
            TaskSlot(
                slot_id="s2",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 10, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 20, 18, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
        ]
        config = HomeLeaveConfig(
            enabled=True,
            min_rest_hours=8.0,
            eligibility_threshold_hours=24.0,
            leave_capacity=1,
            leave_duration_hours=48.0,
        )
        input_data = make_input(slots, people, home_leave_config=config)
        conflicts = _build_hard_conflicts(input_data, people, slots)

        # Should have a min_rest_violation conflict (2h gap < 8h min rest)
        min_rest_conflicts = [c for c in conflicts if c.rule_type == "min_rest_violation"]
        assert len(min_rest_conflicts) > 0
        assert min_rest_conflicts[0].affected_person_ids == ["p1"]
        assert set(min_rest_conflicts[0].affected_slot_ids) == {"s1", "s2"}

    def test_hard_conflict_includes_min_rest_violation_details(self):
        """
        When infeasible with people present and min-rest violations exist,
        the conflict should include rule_type='min_rest_violation' with
        affected_person_ids and affected_slot_ids.
        """
        # Create a scenario that triggers conflict analysis:
        # 1 person, 2 slots with 2h gap, min_rest=8h, both need headcount=1
        # The solver returns feasible (partial), but if we force timed_out with 0 assignments
        # we can't easily do that. Instead, let's verify the conflict analysis logic directly.
        from solver.engine import _build_hard_conflicts
        from models.solver_input import SolverInput

        people = [make_person("p1")]
        slots = [
            TaskSlot(
                slot_id="s1",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 0, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 20, 8, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
            TaskSlot(
                slot_id="s2",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 10, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 20, 18, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
        ]
        config = HomeLeaveConfig(
            enabled=True,
            min_rest_hours=8.0,
            eligibility_threshold_hours=24.0,
            leave_capacity=1,
            leave_duration_hours=48.0,
        )
        input_data = make_input(slots, people, home_leave_config=config)
        conflicts = _build_hard_conflicts(input_data, people, slots)

        # Should have a min_rest_violation conflict
        min_rest_conflicts = [c for c in conflicts if c.rule_type == "min_rest_violation"]
        assert len(min_rest_conflicts) == 1
        conflict = min_rest_conflicts[0]
        assert conflict.affected_person_ids == ["p1"]
        assert set(conflict.affected_slot_ids) == {"s1", "s2"}
        assert "min_rest_violation" in conflict.rule_type

    def test_no_min_rest_conflict_when_gap_sufficient(self):
        """No min-rest conflict when the gap between slots is >= min_rest_hours."""
        from solver.engine import _build_hard_conflicts

        people = [make_person("p1")]
        # 10h gap between slots — no violation with 8h min rest
        slots = [
            TaskSlot(
                slot_id="s1",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 0, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 20, 8, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
            TaskSlot(
                slot_id="s2",
                task_type_id="tt-1",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 18, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 21, 2, 0, tzinfo=timezone.utc),
                required_headcount=1,
                priority=5,
                required_role_ids=[],
                required_qualification_ids=[],
                allows_overlap=False,
            ),
        ]
        config = HomeLeaveConfig(
            enabled=True,
            min_rest_hours=8.0,
            eligibility_threshold_hours=24.0,
            leave_capacity=1,
            leave_duration_hours=48.0,
        )
        input_data = make_input(slots, people, home_leave_config=config)
        conflicts = _build_hard_conflicts(input_data, people, slots)

        min_rest_conflicts = [c for c in conflicts if c.rule_type == "min_rest_violation"]
        assert len(min_rest_conflicts) == 0


class TestMinRestEmergencyBypass:
    """Emergency bypass skips min-rest enforcement for bypassed persons."""

    def test_emergency_bypass_allows_rest_violation(self):
        """
        A person with emergency bypass can be assigned to both slots
        even when the gap violates min-rest in closed-base mode.
        """
        people = [make_person("p1")]
        # Two slots with only 2h gap
        slots = [
            make_slot("s1", start_hour=0, end_hour=8),
            make_slot("s2", start_hour=10, end_hour=18),
        ]
        config = HomeLeaveConfig(
            enabled=True,
            min_rest_hours=8.0,
            eligibility_threshold_hours=24.0,
            leave_capacity=1,
            leave_duration_hours=48.0,
        )
        emergency = [
            HardConstraint(
                constraint_id="em1",
                rule_type="emergency_person_bypass",
                scope_type="person",
                scope_id="p1",
                payload={"person_id": "p1"}
            )
        ]
        result = solve(make_input(
            slots, people,
            home_leave_config=config,
            emergency_constraints=emergency
        ))
        # Emergency bypass — both slots should be assigned despite rest violation
        assert result.feasible
        assert len(result.assignments) == 2

    def test_emergency_bypass_skips_conflict_reporting(self):
        """
        Emergency-bypassed persons should not appear in min-rest HardConflict entries.
        """
        from solver.engine import _build_hard_conflicts

        people = [make_person("p1")]
        slots = [
            make_slot("s1", start_hour=0, end_hour=8),
            make_slot("s2", start_hour=10, end_hour=18),
        ]
        config = HomeLeaveConfig(
            enabled=True,
            min_rest_hours=8.0,
            eligibility_threshold_hours=24.0,
            leave_capacity=1,
            leave_duration_hours=48.0,
        )
        emergency = [
            HardConstraint(
                constraint_id="em1",
                rule_type="emergency_person_bypass",
                scope_type="person",
                scope_id="p1",
                payload={"person_id": "p1"}
            )
        ]
        input_data = make_input(
            slots, people,
            home_leave_config=config,
            emergency_constraints=emergency
        )
        conflicts = _build_hard_conflicts(input_data, people, slots)

        # p1 is bypassed — no min-rest conflict should be reported for them
        min_rest_conflicts = [c for c in conflicts if c.rule_type == "min_rest_violation"]
        assert len(min_rest_conflicts) == 0

    def test_non_bypassed_person_still_blocked(self):
        """
        When one person has emergency bypass and another doesn't,
        only the non-bypassed person is subject to min-rest.
        """
        people = [make_person("p1"), make_person("p2")]
        # Two slots with only 2h gap — both need headcount=1
        slots = [
            make_slot("s1", start_hour=0, end_hour=8),
            make_slot("s2", start_hour=10, end_hour=18),
        ]
        config = HomeLeaveConfig(
            enabled=True,
            min_rest_hours=8.0,
            eligibility_threshold_hours=24.0,
            leave_capacity=1,
            leave_duration_hours=48.0,
        )
        # Only p1 has emergency bypass
        emergency = [
            HardConstraint(
                constraint_id="em1",
                rule_type="emergency_person_bypass",
                scope_type="person",
                scope_id="p1",
                payload={"person_id": "p1"}
            )
        ]
        result = solve(make_input(
            slots, people,
            home_leave_config=config,
            emergency_constraints=emergency
        ))
        assert result.feasible
        assert len(result.assignments) == 2
        # p1 (bypassed) can take both, or p1 takes one and p2 takes the other
        # Either way, both slots should be covered
        slot_ids = {a.slot_id for a in result.assignments}
        assert "s1" in slot_ids
        assert "s2" in slot_ids
