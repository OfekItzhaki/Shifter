"""
Direct solver test — no HTTP, no database.
Verifies the CP-SAT engine works end-to-end for a minimal case:
  2 people, 1 task with 2 non-overlapping 8-hour shifts.

Run from apps/solver/:
    python test_direct.py
"""
import sys
import os

# Ensure the solver package is importable
sys.path.insert(0, os.path.dirname(__file__))

from datetime import date, datetime, timezone
from models.solver_input import (
    SolverInput, PersonEligibility, TaskSlot, StabilityWeights,
)
from solver.engine import solve


def main():
    print("=" * 60)
    print("Direct solver test: 2 people, 2 non-overlapping 8h shifts")
    print("=" * 60)

    people = [
        PersonEligibility(person_id="alice", role_ids=[], qualification_ids=[], group_ids=[]),
        PersonEligibility(person_id="bob",   role_ids=[], qualification_ids=[], group_ids=[]),
    ]

    slots = [
        TaskSlot(
            slot_id="shift-morning",
            task_type_id="task-guard",
            task_type_name="Guard",
            burden_level="neutral",
            starts_at=datetime(2026, 6, 1, 8, 0, tzinfo=timezone.utc),
            ends_at=datetime(2026, 6, 1, 16, 0, tzinfo=timezone.utc),
            required_headcount=1,
            priority=5,
            required_role_ids=[],
            required_qualification_ids=[],
            allows_overlap=False,
        ),
        TaskSlot(
            slot_id="shift-evening",
            task_type_id="task-guard",
            task_type_name="Guard",
            burden_level="neutral",
            starts_at=datetime(2026, 6, 1, 16, 0, tzinfo=timezone.utc),
            ends_at=datetime(2026, 6, 2, 0, 0, tzinfo=timezone.utc),
            required_headcount=1,
            priority=5,
            required_role_ids=[],
            required_qualification_ids=[],
            allows_overlap=False,
        ),
    ]

    solver_input = SolverInput(
        space_id="test-space",
        run_id="test-run-direct",
        trigger_mode="standard",
        horizon_start=date(2026, 6, 1),
        horizon_end=date(2026, 6, 7),
        stability_weights=StabilityWeights(today_tomorrow=10.0, days_3_4=3.0, days_5_7=1.0),
        people=people,
        availability_windows=[],
        presence_windows=[],
        task_slots=slots,
        hard_constraints=[],
        soft_constraints=[],
        baseline_assignments=[],
        fairness_counters=[],
    )

    print(f"\nInput: {len(people)} people, {len(slots)} slots")
    for p in people:
        print(f"  Person: {p.person_id}")
    for s in slots:
        print(f"  Slot:   {s.slot_id}  {s.starts_at.strftime('%H:%M')} – {s.ends_at.strftime('%H:%M')}")

    print("\nRunning solver...")
    result = solve(solver_input)

    print("\nResult:")
    print(f"  feasible    = {result.feasible}")
    print(f"  timed_out   = {result.timed_out}")
    print(f"  assignments = {len(result.assignments)}")
    print(f"  uncovered   = {result.uncovered_slot_ids}")

    if result.assignments:
        print("\nAssignments:")
        for a in result.assignments:
            print(f"  {a.slot_id} → {a.person_id}")
    else:
        print("\nNo assignments produced!")

    if result.hard_conflicts:
        print("\nHard conflicts:")
        for c in result.hard_conflicts:
            print(f"  [{c.rule_type}] {c.description}")

    print("\nExplanation:")
    for f in result.explanation_fragments:
        print(f"  {f}")

    # Assertions
    assert result.feasible, "FAIL: expected feasible=True, got feasible=False"
    assert len(result.assignments) == 2, \
        f"FAIL: expected 2 assignments, got {len(result.assignments)}"
    assert len(result.uncovered_slot_ids) == 0, \
        f"FAIL: expected 0 uncovered slots, got {result.uncovered_slot_ids}"

    assigned_slots = {a.slot_id for a in result.assignments}
    assert "shift-morning" in assigned_slots, "FAIL: shift-morning not assigned"
    assert "shift-evening" in assigned_slots, "FAIL: shift-evening not assigned"

    assigned_people = {a.person_id for a in result.assignments}
    assert len(assigned_people) == 2, \
        f"FAIL: expected 2 distinct people assigned, got {assigned_people}"

    print("\n✓ All assertions passed — solver is working correctly!")
    return 0


if __name__ == "__main__":
    sys.exit(main())
