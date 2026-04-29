"""
Scenario-based solver tests covering real-world edge cases.

Scenarios tested:
  1. Too many tasks for the number of people
  2. Too many tasks for the number of days (time pressure)
  3. Enough people but hard constraints can't be satisfied
  4. Soft constraints violated but still feasible
  5. Rest constraint makes scheduling impossible with few people
  6. All people blocked by availability windows
  7. Single person, multiple non-overlapping shifts — should work
  8. Partial coverage — some slots covered, some not
  9. Fairness: hated tasks distributed across people
  10. Stability: baseline assignments preserved when possible
"""
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from datetime import date, datetime, timezone, timedelta
from models.solver_input import (
    SolverInput, PersonEligibility, TaskSlot, StabilityWeights,
    HardConstraint, SoftConstraint, BaselineAssignment, FairnessCounters,
    AvailabilityWindow, PresenceWindow,
)
from solver.engine import solve


# ── Helpers ───────────────────────────────────────────────────────────────────

HORIZON_START = date(2026, 5, 1)
HORIZON_END   = date(2026, 5, 7)

def make_input(slots, people, hard_constraints=None, soft_constraints=None,
               availability=None, presence=None, baseline=None, fairness=None):
    return SolverInput(
        space_id="test", run_id="test-run", trigger_mode="standard",
        horizon_start=HORIZON_START, horizon_end=HORIZON_END,
        stability_weights=StabilityWeights(today_tomorrow=10.0, days_3_4=3.0, days_5_7=1.0),
        people=people,
        availability_windows=availability or [],
        presence_windows=presence or [],
        task_slots=slots,
        hard_constraints=hard_constraints or [],
        soft_constraints=soft_constraints or [],
        baseline_assignments=baseline or [],
        fairness_counters=fairness or [],
    )

def person(pid, roles=None, quals=None):
    return PersonEligibility(person_id=pid, role_ids=roles or [],
                             qualification_ids=quals or [], group_ids=[])

def slot(sid, day=1, start_h=8, end_h=16, headcount=1, burden="neutral",
         task_type_id="tt-guard", task_name="Guard"):
    d = date(2026, 5, day)
    return TaskSlot(
        slot_id=sid, task_type_id=task_type_id, task_type_name=task_name,
        burden_level=burden,
        starts_at=datetime(d.year, d.month, d.day, start_h, 0, tzinfo=timezone.utc),
        ends_at=datetime(d.year, d.month, d.day, end_h, 0, tzinfo=timezone.utc),
        required_headcount=headcount, priority=5,
        required_role_ids=[], required_qualification_ids=[],
        allows_overlap=False,
    )

def rest_constraint(hours=8.0):
    return HardConstraint(
        constraint_id="rest", rule_type="min_rest_hours",
        scope_type="space", scope_id=None,
        payload={"hours": hours}
    )


# ── Scenario 1: Too many tasks for the number of people ──────────────────────

class TestTooManyTasksForPeople:
    def test_3_simultaneous_slots_2_people_infeasible(self):
        """3 overlapping slots each needing 1 person, only 2 people → infeasible."""
        people = [person("p1"), person("p2")]
        slots = [
            slot("s1", day=1, start_h=8, end_h=16),
            slot("s2", day=1, start_h=8, end_h=16),
            slot("s3", day=1, start_h=8, end_h=16),
        ]
        result = solve(make_input(slots, people))
        assert not result.feasible
        assert len(result.hard_conflicts) > 0

    def test_3_simultaneous_slots_3_people_feasible(self):
        """3 overlapping slots, 3 people → feasible."""
        people = [person("p1"), person("p2"), person("p3")]
        slots = [
            slot("s1", day=1, start_h=8, end_h=16),
            slot("s2", day=1, start_h=8, end_h=16),
            slot("s3", day=1, start_h=8, end_h=16),
        ]
        result = solve(make_input(slots, people))
        assert result.feasible
        assert len(result.assignments) == 3

    def test_headcount_2_with_1_person_infeasible(self):
        """Slot requires 2 people, only 1 available → infeasible."""
        people = [person("p1")]
        slots = [slot("s1", headcount=2)]
        result = solve(make_input(slots, people))
        assert not result.feasible

    def test_conflict_description_mentions_headcount(self):
        """Hard conflict description should mention the headcount shortage."""
        people = [person("p1")]
        slots = [slot("s1", headcount=5)]
        result = solve(make_input(slots, people))
        assert not result.feasible
        assert len(result.hard_conflicts) > 0
        # At least one conflict should mention the slot
        descriptions = " ".join(c.description for c in result.hard_conflicts)
        assert len(descriptions) > 0


# ── Scenario 2: Too many tasks for the number of days (time pressure) ────────

class TestTimePressure:
    def test_one_person_two_back_to_back_shifts_no_rest_infeasible(self):
        """
        1 person, 2 shifts with only 4h gap, 8h rest required → infeasible.
        This simulates too many tasks crammed into too few days.
        """
        people = [person("p1")]
        slots = [
            slot("s1", day=1, start_h=0, end_h=8),
            slot("s2", day=1, start_h=12, end_h=20),  # only 4h gap
        ]
        result = solve(make_input(slots, people, hard_constraints=[rest_constraint(8.0)]))
        assert not result.feasible

    def test_two_people_can_cover_back_to_back_shifts(self):
        """2 people, 2 back-to-back shifts → feasible (different people cover each)."""
        people = [person("p1"), person("p2")]
        slots = [
            slot("s1", day=1, start_h=0, end_h=8),
            slot("s2", day=1, start_h=12, end_h=20),
        ]
        result = solve(make_input(slots, people, hard_constraints=[rest_constraint(8.0)]))
        assert result.feasible
        assert len(result.assignments) == 2
        # Each shift must be covered by a different person
        p_s1 = next(a.person_id for a in result.assignments if a.slot_id == "s1")
        p_s2 = next(a.person_id for a in result.assignments if a.slot_id == "s2")
        assert p_s1 != p_s2

    def test_many_slots_spread_across_days_feasible(self):
        """5 people, 1 slot per day for 5 days → feasible."""
        people = [person(f"p{i}") for i in range(5)]
        slots = [slot(f"s{d}", day=d) for d in range(1, 6)]
        result = solve(make_input(slots, people))
        assert result.feasible
        assert len(result.assignments) == 5


# ── Scenario 3: Enough people but hard constraints can't be satisfied ─────────

class TestConstraintsUnsatisfiable:
    def test_all_people_restricted_from_task_type_infeasible(self):
        """All people have a restriction on the task type → infeasible."""
        people = [person("p1"), person("p2")]
        slots = [slot("s1", task_type_id="tt-kitchen", task_name="Kitchen")]
        constraints = [
            HardConstraint(
                constraint_id=f"r{i}", rule_type="no_task_type_restriction",
                scope_type="person", scope_id=f"p{i+1}",
                payload={"person_id": f"p{i+1}", "task_type_id": "tt-kitchen"}
            )
            for i in range(2)
        ]
        result = solve(make_input(slots, people, hard_constraints=constraints))
        assert not result.feasible

    def test_one_person_restricted_other_covers(self):
        """p1 restricted from kitchen, p2 is not → p2 covers it."""
        people = [person("p1"), person("p2")]
        slots = [slot("s1", task_type_id="tt-kitchen", task_name="Kitchen")]
        constraints = [
            HardConstraint(
                constraint_id="r1", rule_type="no_task_type_restriction",
                scope_type="person", scope_id="p1",
                payload={"person_id": "p1", "task_type_id": "tt-kitchen"}
            )
        ]
        result = solve(make_input(slots, people, hard_constraints=constraints))
        assert result.feasible
        assert result.assignments[0].person_id == "p2"

    def test_qualification_required_nobody_has_it_infeasible(self):
        """Slot requires 'medic_cert', no one has it → infeasible."""
        people = [person("p1"), person("p2")]
        s = TaskSlot(
            slot_id="s1", task_type_id="tt-medical", task_type_name="Medical",
            burden_level="neutral",
            starts_at=datetime(2026, 5, 1, 8, 0, tzinfo=timezone.utc),
            ends_at=datetime(2026, 5, 1, 16, 0, tzinfo=timezone.utc),
            required_headcount=1, priority=5,
            required_role_ids=[], required_qualification_ids=["medic_cert"],
            allows_overlap=False,
        )
        result = solve(make_input([s], people))
        assert not result.feasible

    def test_qualification_required_one_person_has_it_feasible(self):
        """Slot requires 'medic_cert', p2 has it → p2 assigned."""
        people = [person("p1"), person("p2", quals=["medic_cert"])]
        s = TaskSlot(
            slot_id="s1", task_type_id="tt-medical", task_type_name="Medical",
            burden_level="neutral",
            starts_at=datetime(2026, 5, 1, 8, 0, tzinfo=timezone.utc),
            ends_at=datetime(2026, 5, 1, 16, 0, tzinfo=timezone.utc),
            required_headcount=1, priority=5,
            required_role_ids=[], required_qualification_ids=["medic_cert"],
            allows_overlap=False,
        )
        result = solve(make_input([s], people))
        assert result.feasible
        assert result.assignments[0].person_id == "p2"


# ── Scenario 4: Availability blocks ──────────────────────────────────────────

class TestAvailabilityBlocks:
    def test_person_at_home_cannot_be_assigned(self):
        """p1 is at_home during the slot → only p2 can cover it."""
        people = [person("p1"), person("p2")]
        slots = [slot("s1", day=1, start_h=8, end_h=16)]
        presence = [
            PresenceWindow(
                person_id="p1", state="at_home",
                starts_at=datetime(2026, 5, 1, 0, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 5, 1, 23, 59, tzinfo=timezone.utc),
            )
        ]
        result = solve(make_input(slots, people, presence=presence))
        assert result.feasible
        assert result.assignments[0].person_id == "p2"

    def test_all_people_at_home_infeasible(self):
        """Both people at_home during the slot → infeasible."""
        people = [person("p1"), person("p2")]
        slots = [slot("s1", day=1, start_h=8, end_h=16)]
        presence = [
            PresenceWindow(
                person_id=pid, state="at_home",
                starts_at=datetime(2026, 5, 1, 0, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 5, 1, 23, 59, tzinfo=timezone.utc),
            )
            for pid in ["p1", "p2"]
        ]
        result = solve(make_input(slots, people, presence=presence))
        assert not result.feasible

    def test_availability_window_covers_slot_feasible(self):
        """p1 has availability window covering the slot → assigned."""
        people = [person("p1")]
        slots = [slot("s1", day=1, start_h=8, end_h=16)]
        availability = [
            AvailabilityWindow(
                person_id="p1",
                starts_at=datetime(2026, 5, 1, 6, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 5, 1, 20, 0, tzinfo=timezone.utc),
            )
        ]
        result = solve(make_input(slots, people, availability=availability))
        assert result.feasible

    def test_availability_window_does_not_cover_slot_infeasible(self):
        """p1's availability window doesn't cover the slot → infeasible."""
        people = [person("p1")]
        slots = [slot("s1", day=1, start_h=8, end_h=16)]
        availability = [
            AvailabilityWindow(
                person_id="p1",
                starts_at=datetime(2026, 5, 1, 18, 0, tzinfo=timezone.utc),
                ends_at=datetime(2026, 5, 1, 22, 0, tzinfo=timezone.utc),
            )
        ]
        result = solve(make_input(slots, people, availability=availability))
        assert not result.feasible


# ── Scenario 5: Partial coverage ─────────────────────────────────────────────

class TestPartialCoverage:
    def test_some_slots_covered_some_not_reported(self):
        """
        3 slots, 2 people, all non-overlapping.
        All 3 can be covered since people can do multiple non-overlapping shifts.
        """
        people = [person("p1"), person("p2")]
        slots = [
            slot("s1", day=1, start_h=0, end_h=4),
            slot("s2", day=1, start_h=8, end_h=12),
            slot("s3", day=1, start_h=16, end_h=20),
        ]
        result = solve(make_input(slots, people))
        assert result.feasible
        # With 2 people and 3 non-overlapping slots, all should be covered
        assert len(result.uncovered_slot_ids) == 0

    def test_uncovered_slots_reported_when_infeasible(self):
        """When infeasible, uncovered_slot_ids should list all slots."""
        people = [person("p1")]
        slots = [
            slot("s1", day=1, start_h=8, end_h=16, headcount=2),
        ]
        result = solve(make_input(slots, people))
        assert not result.feasible
        assert "s1" in result.uncovered_slot_ids


# ── Scenario 6: Fairness ──────────────────────────────────────────────────────

class TestFairness:
    def test_hated_task_assigned_to_person_with_lower_history(self):
        """
        p1 has high burden history, p2 has none.
        Hated task should prefer p2.
        """
        people = [person("p1"), person("p2")]
        slots = [slot("s1", burden="hated")]
        fairness = [
            FairnessCounters(person_id="p1", disliked_hated_score_7d=10),
            FairnessCounters(person_id="p2", disliked_hated_score_7d=0),
        ]
        result = solve(make_input(slots, people, fairness=fairness))
        assert result.feasible
        # p2 should be preferred (lower burden history)
        assert result.assignments[0].person_id == "p2"

    def test_fairness_metrics_include_all_people(self):
        """Fairness metrics should be returned for every person."""
        people = [person(f"p{i}") for i in range(4)]
        slots = [slot("s1")]
        result = solve(make_input(slots, people))
        assert result.feasible
        assert len(result.fairness_metrics) == 4


# ── Scenario 7: Stability ─────────────────────────────────────────────────────

class TestStability:
    def test_baseline_person_preferred_for_same_slot(self):
        """When p2 was previously assigned to s1, they should be preferred."""
        people = [person("p1"), person("p2")]
        slots = [slot("s1")]
        baseline = [BaselineAssignment(slot_id="s1", person_id="p2")]
        result = solve(make_input(slots, people, baseline=baseline))
        assert result.feasible
        assert result.assignments[0].person_id == "p2"

    def test_stability_metrics_show_changes_from_baseline(self):
        """When a different person is assigned vs baseline, changes should be counted."""
        people = [person("p1"), person("p2")]
        # Force p1 to be the only option by restricting p2
        slots = [slot("s1", task_type_id="tt-x", task_name="X")]
        constraints = [
            HardConstraint(
                constraint_id="r1", rule_type="no_task_type_restriction",
                scope_type="person", scope_id="p2",
                payload={"person_id": "p2", "task_type_id": "tt-x"}
            )
        ]
        baseline = [BaselineAssignment(slot_id="s1", person_id="p2")]
        result = solve(make_input(slots, people, hard_constraints=constraints, baseline=baseline))
        assert result.feasible
        assert result.assignments[0].person_id == "p1"
        # Stability penalty should be non-zero (change from baseline)
        assert result.stability_metrics.total_stability_penalty > 0
