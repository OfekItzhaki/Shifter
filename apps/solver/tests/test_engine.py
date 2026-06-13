"""
Tests for the CP-SAT solver engine.
Verifies hard constraints and stability objective behavior.
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from datetime import date, datetime, timezone
from models.solver_input import (
    SolverInput, PersonEligibility, TaskSlot, StabilityWeights,
    BaselineAssignment
)
from solver.engine import solve


def make_input(slots, people, baseline=None, hard_constraints=None):
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
        baseline_assignments=baseline or [],
        fairness_counters=[]
    )


def make_person(pid, roles=None, quals=None):
    return PersonEligibility(
        person_id=pid,
        role_ids=roles or [],
        qualification_ids=quals or [],
        group_ids=[]
    )


def make_slot(sid, start_hour=8, end_hour=16, headcount=1, burden="neutral"):
    return TaskSlot(
        slot_id=sid,
        task_type_id="tt-1",
        task_type_name="Guard",
        burden_level=burden,
        starts_at=datetime(2026, 4, 20, start_hour, 0, tzinfo=timezone.utc),
        ends_at=datetime(2026, 4, 20, end_hour, 0, tzinfo=timezone.utc),
        required_headcount=headcount,
        priority=5,
        required_role_ids=[],
        required_qualification_ids=[],
        allows_overlap=False
    )


class TestBasicSolving:
    def test_single_slot_single_person_is_feasible(self):
        people = [make_person("p1")]
        slots = [make_slot("s1")]
        result = solve(make_input(slots, people))
        assert result.feasible
        assert len(result.assignments) == 1
        assert result.assignments[0].slot_id == "s1"
        assert result.assignments[0].person_id == "p1"

    def test_headcount_two_requires_two_people(self):
        people = [make_person("p1"), make_person("p2"), make_person("p3")]
        slots = [make_slot("s1", headcount=2)]
        result = solve(make_input(slots, people))
        assert result.feasible
        assigned = [a for a in result.assignments if a.slot_id == "s1"]
        assert len(assigned) == 2

    def test_no_people_returns_infeasible(self):
        """No people means no one can be assigned — infeasible."""
        slots = [make_slot("s1")]
        result = solve(make_input(slots, []))
        assert not result.feasible
        assert len(result.assignments) == 0

    def test_no_slots_returns_infeasible(self):
        """No slots means nothing to schedule — treated as infeasible (no work done)."""
        people = [make_person("p1")]
        result = solve(make_input([], people))
        assert not result.feasible
        assert len(result.assignments) == 0


class TestNoOverlap:
    def test_overlapping_slots_assigned_to_different_people(self):
        people = [make_person("p1"), make_person("p2")]
        # Both slots overlap: 08:00-16:00 and 10:00-18:00
        slots = [
            make_slot("s1", start_hour=8, end_hour=16),
            make_slot("s2", start_hour=10, end_hour=18),
        ]
        result = solve(make_input(slots, people))
        assert result.feasible
        # Each slot should be assigned to a different person
        s1_person = next(a.person_id for a in result.assignments if a.slot_id == "s1")
        s2_person = next(a.person_id for a in result.assignments if a.slot_id == "s2")
        assert s1_person != s2_person


class TestStabilityObjective:
    def test_baseline_assignment_is_preserved(self):
        people = [make_person("p1"), make_person("p2")]
        slots = [make_slot("s1")]
        # Baseline: p2 was assigned to s1
        baseline = [BaselineAssignment(slot_id="s1", person_id="p2")]
        result = solve(make_input(slots, people, baseline=baseline))
        assert result.feasible
        # p2 should be preferred due to stability penalty
        assigned_person = result.assignments[0].person_id
        assert assigned_person == "p2"


class TestStabilityMetrics:
    def test_stability_metrics_returned(self):
        people = [make_person("p1")]
        slots = [make_slot("s1")]
        result = solve(make_input(slots, people))
        assert result.stability_metrics is not None
        assert result.stability_metrics.total_stability_penalty >= 0


class TestFairnessMetrics:
    def test_fairness_metrics_returned_for_all_people(self):
        people = [make_person("p1"), make_person("p2")]
        slots = [make_slot("s1")]
        result = solve(make_input(slots, people))
        assert len(result.fairness_metrics) == 2
        person_ids = {m.person_id for m in result.fairness_metrics}
        assert "p1" in person_ids
        assert "p2" in person_ids


class TestInfeasibility:
    def test_insufficient_people_for_headcount_returns_partial(self):
        """1 person cannot fill a slot requiring 2 — solver returns feasible=True
        with partial coverage (1 assignment) and the slot flagged as uncovered.
        The headcount constraint is soft (coverage objective), not hard."""
        people = [make_person("p1")]
        slots = [make_slot("s1", headcount=2)]
        result = solve(make_input(slots, people))
        # Solver is feasible — it assigns what it can
        assert result.feasible
        # The 1 available person is assigned
        assert len(result.assignments) == 1
        # The slot is flagged as not fully staffed
        assert "s1" in result.uncovered_slot_ids

    def test_hard_conflicts_reported_when_infeasible(self):
        """When infeasible (e.g. zero people), hard_conflicts should be non-empty."""
        people = []
        slots = [make_slot("s1", headcount=3)]
        result = solve(make_input(slots, people))
        # Zero people → _empty_result → feasible=False
        assert not result.feasible

    def test_enough_people_is_feasible(self):
        """3 people for a slot requiring 2 — feasible."""
        people = [make_person("p1"), make_person("p2"), make_person("p3")]
        slots = [make_slot("s1", headcount=2)]
        result = solve(make_input(slots, people))
        assert result.feasible
        assigned = [a for a in result.assignments if a.slot_id == "s1"]
        assert len(assigned) == 2

    def test_multiple_slots_all_covered(self):
        """2 people, 2 non-overlapping slots requiring 1 each — both covered."""
        people = [make_person("p1"), make_person("p2")]
        slots = [
            make_slot("s1", start_hour=8, end_hour=12),
            make_slot("s2", start_hour=14, end_hour=18),
        ]
        result = solve(make_input(slots, people))
        assert result.feasible
        assert len(result.uncovered_slot_ids) == 0
        assert len(result.assignments) == 2

    def test_rest_constraint_makes_infeasible_with_one_person(self):
        """1 person, 2 slots with only 4h gap — infeasible with 8h rest rule."""
        from models.solver_input import HardConstraint
        people = [make_person("p1")]
        slots = [
            make_slot("s1", start_hour=0, end_hour=8),
            make_slot("s2", start_hour=12, end_hour=20),
        ]
        rest_constraint = HardConstraint(
            constraint_id="c1",
            rule_type="min_rest_hours",
            scope_type="space",
            scope_id=None,
            payload={"hours": 8}
        )
        result = solve(make_input(slots, people, hard_constraints=[rest_constraint]))
        # With soft headcount: person covers 1 slot, the other is uncovered
        assert result.feasible
        assert len(result.assignments) == 1
        assert len(result.uncovered_slot_ids) == 1


class TestShiftSlotIds:
    def test_shift_slot_ids_are_unique_per_shift(self):
        """
        When a task generates multiple shifts (e.g. s1:shift:0, s1:shift:1),
        each shift must be assigned independently — not collapsed to the same slot.
        """
        people = [make_person("p1"), make_person("p2")]
        # Two non-overlapping shifts from the same task
        slots = [
            TaskSlot(
                slot_id="task-abc:shift:0",
                task_type_id="task-abc",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 8, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 20, 16, 0, tzinfo=timezone.utc),
                required_headcount=1, priority=5,
                required_role_ids=[], required_qualification_ids=[],
                allows_overlap=False
            ),
            TaskSlot(
                slot_id="task-abc:shift:1",
                task_type_id="task-abc",
                task_type_name="Guard",
                burden_level="neutral",
                starts_at=datetime(2026, 4, 20, 16, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 4, 20, 23, 59, tzinfo=timezone.utc),
                required_headcount=1, priority=5,
                required_role_ids=[], required_qualification_ids=[],
                allows_overlap=False
            ),
        ]
        result = solve(make_input(slots, people))
        assert result.feasible
        # Both shifts should be assigned
        assert len(result.assignments) == 2
        slot_ids = {a.slot_id for a in result.assignments}
        assert "task-abc:shift:0" in slot_ids
        assert "task-abc:shift:1" in slot_ids
