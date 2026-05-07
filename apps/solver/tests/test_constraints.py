"""
Tests for individual hard constraint functions.
"""
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from datetime import datetime, timezone
from ortools.sat.python import cp_model
from models.solver_input import TaskSlot, HardConstraint, PersonEligibility
from solver.constraints import (
    add_no_overlap_constraints,
    add_min_rest_constraints,
    add_qualification_constraints,
    add_role_constraints,
    add_kitchen_frequency_constraints,
)


def make_slot(sid, start_hour, end_hour, allows_overlap=False, task_type_name="Guard"):
    return TaskSlot(
        slot_id=sid, task_type_id="tt1", task_type_name=task_type_name,
        burden_level="neutral",
        starts_at=datetime(2026, 4, 20, start_hour, 0, tzinfo=timezone.utc),
        ends_at=datetime(2026, 4, 20, end_hour, 0, tzinfo=timezone.utc),
        required_headcount=1, priority=5,
        required_role_ids=[], required_qualification_ids=[],
        allows_overlap=allows_overlap
    )


def make_person(pid, roles=None, quals=None):
    return PersonEligibility(
        person_id=pid, role_ids=roles or [],
        qualification_ids=quals or [], group_ids=[]
    )


def solve_simple(model, assign, num_slots, num_people):
    """Solve a model and return True if feasible."""
    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = 5
    status = solver.solve(model)
    return status in (cp_model.OPTIMAL, cp_model.FEASIBLE), solver, assign


class TestNoOverlapConstraint:
    def test_overlapping_slots_blocked_for_same_person(self):
        model = cp_model.CpModel()
        slots = [make_slot("s1", 8, 16), make_slot("s2", 10, 18)]
        people = [make_person("p1")]
        assign = {(s, p): model.new_bool_var(f"a_{s}_{p}")
                  for s in range(2) for p in range(1)}

        # Force both slots assigned to p1
        model.add(assign[(0, 0)] == 1)
        model.add(assign[(1, 0)] == 1)
        add_no_overlap_constraints(model, assign, slots, people, 1)

        feasible, _, _ = solve_simple(model, assign, 2, 1)
        assert not feasible  # should be infeasible

    def test_non_overlapping_slots_allowed(self):
        model = cp_model.CpModel()
        slots = [make_slot("s1", 8, 12), make_slot("s2", 14, 18)]
        people = [make_person("p1")]
        assign = {(s, p): model.new_bool_var(f"a_{s}_{p}")
                  for s in range(2) for p in range(1)}

        model.add(assign[(0, 0)] == 1)
        model.add(assign[(1, 0)] == 1)
        add_no_overlap_constraints(model, assign, slots, people, 1)

        feasible, _, _ = solve_simple(model, assign, 2, 1)
        assert feasible  # should be feasible


class TestMinRestConstraint:
    def test_insufficient_rest_blocked(self):
        model = cp_model.CpModel()
        # slot1 ends at 16:00, slot2 starts at 20:00 — only 4h gap, need 8h
        slots = [make_slot("s1", 8, 16), make_slot("s2", 20, 23)]
        people = [make_person("p1")]
        assign = {(s, p): model.new_bool_var(f"a_{s}_{p}")
                  for s in range(2) for p in range(1)}

        model.add(assign[(0, 0)] == 1)
        model.add(assign[(1, 0)] == 1)
        add_min_rest_constraints(model, assign, slots, people, 1, min_rest_hours=8.0)

        feasible, _, _ = solve_simple(model, assign, 2, 1)
        assert not feasible

    def test_sufficient_rest_allowed(self):
        model = cp_model.CpModel()
        # slot1 ends at 08:00, slot2 starts at 20:00 — 12h gap
        slots = [make_slot("s1", 0, 8), make_slot("s2", 20, 23)]
        people = [make_person("p1")]
        assign = {(s, p): model.new_bool_var(f"a_{s}_{p}")
                  for s in range(2) for p in range(1)}

        model.add(assign[(0, 0)] == 1)
        model.add(assign[(1, 0)] == 1)
        add_min_rest_constraints(model, assign, slots, people, 1, min_rest_hours=8.0)

        feasible, _, _ = solve_simple(model, assign, 2, 1)
        assert feasible


class TestQualificationConstraint:
    def test_person_without_qualification_blocked(self):
        model = cp_model.CpModel()
        slot = TaskSlot(
            slot_id="s1", task_type_id="tt1", task_type_name="Medical",
            burden_level="neutral",
            starts_at=datetime(2026, 4, 20, 8, 0, tzinfo=timezone.utc),
            ends_at=datetime(2026, 4, 20, 16, 0, tzinfo=timezone.utc),
            required_headcount=1, priority=5,
            required_role_ids=[], required_qualification_ids=["medic_cert"],
            allows_overlap=False
        )
        person = make_person("p1", quals=[])  # no qualifications
        assign = {(0, 0): model.new_bool_var("a_0_0")}
        model.add(assign[(0, 0)] == 1)

        add_qualification_constraints(model, assign, [slot], [person], 1)

        feasible, _, _ = solve_simple(model, assign, 1, 1)
        assert not feasible

    def test_person_with_qualification_allowed(self):
        model = cp_model.CpModel()
        slot = TaskSlot(
            slot_id="s1", task_type_id="tt1", task_type_name="Medical",
            burden_level="neutral",
            starts_at=datetime(2026, 4, 20, 8, 0, tzinfo=timezone.utc),
            ends_at=datetime(2026, 4, 20, 16, 0, tzinfo=timezone.utc),
            required_headcount=1, priority=5,
            required_role_ids=[], required_qualification_ids=["medic_cert"],
            allows_overlap=False
        )
        person = make_person("p1", quals=["medic_cert"])
        assign = {(0, 0): model.new_bool_var("a_0_0")}
        model.add(assign[(0, 0)] == 1)

        add_qualification_constraints(model, assign, [slot], [person], 1)

        feasible, _, _ = solve_simple(model, assign, 1, 1)
        assert feasible
