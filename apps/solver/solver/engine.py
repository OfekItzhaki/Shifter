"""
CP-SAT scheduling engine.
Wires together all hard constraints and weighted soft objectives.
"""
from ortools.sat.python import cp_model
from models.solver_input import SolverInput
from models.solver_output import (
    SolverOutput, AssignmentResult, StabilityMetrics, FairnessMetrics, HardConflict
)
from solver.constraints import (
    add_headcount_constraints,
    add_no_duplicate_assignment,
    add_no_overlap_constraints,
    add_min_rest_constraints,
    add_qualification_constraints,
    add_role_constraints,
    add_restriction_constraints,
    add_kitchen_frequency_constraints,
    add_availability_constraints,
)
from solver.objectives import build_objective
import os
import logging

logger = logging.getLogger(__name__)

SOLVER_TIMEOUT = int(os.getenv("SOLVER_TIMEOUT_SECONDS", "20"))


def solve(input: SolverInput) -> SolverOutput:
    model = cp_model.CpModel()
    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = SOLVER_TIMEOUT

    people = input.people
    slots = input.task_slots
    num_people = len(people)
    num_slots = len(slots)

    if num_slots == 0 or num_people == 0:
        return _empty_result(input)

    # ── Decision variables ────────────────────────────────────────────────────
    # assign[slot_idx][person_idx] = 1 if person is assigned to slot
    assign = {
        (s_idx, p_idx): model.new_bool_var(f"a_{s_idx}_{p_idx}")
        for s_idx in range(num_slots)
        for p_idx in range(num_people)
    }

    # ── Hard constraints ──────────────────────────────────────────────────────
    add_headcount_constraints(model, assign, slots, num_people)
    add_no_duplicate_assignment(model, assign, num_slots, num_people)
    add_no_overlap_constraints(model, assign, slots, num_people)
    add_qualification_constraints(model, assign, slots, people, num_people)
    add_role_constraints(model, assign, slots, people, num_people)
    add_restriction_constraints(model, assign, slots, people, num_people, input.hard_constraints)
    add_availability_constraints(
        model, assign, slots, people, num_people,
        input.availability_windows, input.presence_windows)

    # Extract min_rest_hours from hard constraints if configured
    rest_rules = [c for c in input.hard_constraints if c.rule_type == "min_rest_hours"]
    rest_hours = float(rest_rules[0].payload.get("hours", 8)) if rest_rules else 8.0
    add_min_rest_constraints(model, assign, slots, num_people, rest_hours)

    add_kitchen_frequency_constraints(
        model, assign, slots, people, num_people,
        input.hard_constraints, input.fairness_counters)

    # ── Soft objectives ───────────────────────────────────────────────────────
    penalties = build_objective(model, assign, input)
    model.minimize(sum(penalties))

    # ── Solve ─────────────────────────────────────────────────────────────────
    status = solver.solve(model)
    timed_out = status == cp_model.UNKNOWN
    feasible = status in (cp_model.OPTIMAL, cp_model.FEASIBLE)

    # ── Extract results ───────────────────────────────────────────────────────
    assignments = []
    if feasible:
        for s_idx, slot in enumerate(slots):
            for p_idx, person in enumerate(people):
                if solver.value(assign[(s_idx, p_idx)]) == 1:
                    assignments.append(AssignmentResult(
                        slot_id=slot.slot_id,
                        person_id=person.person_id
                    ))

    uncovered = _compute_uncovered(solver, assign, slots, num_people, feasible)
    stability = _compute_stability(solver, assign, input, feasible)
    fairness  = _compute_fairness(solver, assign, input, feasible)

    fragments = _build_explanation(feasible, timed_out, uncovered, input)

    return SolverOutput(
        run_id=input.run_id,
        feasible=feasible,
        timed_out=timed_out,
        assignments=assignments,
        uncovered_slot_ids=uncovered,
        hard_conflicts=[],  # Phase 3 extension: conflict explanation
        soft_penalty_total=solver.objective_value if feasible else 0.0,
        stability_metrics=stability,
        fairness_metrics=fairness,
        explanation_fragments=fragments
    )


def _empty_result(input: SolverInput) -> SolverOutput:
    return SolverOutput(
        run_id=input.run_id,
        feasible=True,
        timed_out=False,
        assignments=[],
        uncovered_slot_ids=[],
        hard_conflicts=[],
        soft_penalty_total=0.0,
        stability_metrics=StabilityMetrics(
            today_tomorrow_changes=0,
            days_3_4_changes=0,
            days_5_7_changes=0,
            total_stability_penalty=0.0
        ),
        fairness_metrics=[],
        explanation_fragments=["No slots or people to schedule."]
    )


def _compute_uncovered(solver, assign, slots, num_people, feasible) -> list[str]:
    if not feasible:
        return [s.slot_id for s in slots]
    return [
        slot.slot_id
        for s_idx, slot in enumerate(slots)
        if sum(solver.value(assign[(s_idx, p_idx)]) for p_idx in range(num_people))
           < slot.required_headcount
    ]


def _compute_stability(solver, assign, input: SolverInput, feasible) -> StabilityMetrics:
    if not feasible:
        return StabilityMetrics(
            today_tomorrow_changes=0,
            days_3_4_changes=0,
            days_5_7_changes=0,
            total_stability_penalty=0.0
        )

    from solver.objectives import _stability_weight
    from datetime import date

    horizon_start = input.horizon_start
    if isinstance(horizon_start, str):
        from datetime import date
        horizon_start = date.fromisoformat(horizon_start)

    baseline_set = {(a.slot_id, a.person_id) for a in input.baseline_assignments}
    buckets = {0: 0, 1: 0, 2: 0}  # 0=today+tomorrow, 1=days3-4, 2=days5-7

    for s_idx, slot in enumerate(slots := input.task_slots):
        weight = _stability_weight(slot, horizon_start, input.stability_weights)
        for p_idx, person in enumerate(input.people):
            assigned = solver.value(assign[(s_idx, p_idx)]) == 1
            was_assigned = (slot.slot_id, person.person_id) in baseline_set
            if assigned != was_assigned:
                if weight >= input.stability_weights.today_tomorrow:
                    buckets[0] += 1
                elif weight >= input.stability_weights.days_3_4:
                    buckets[1] += 1
                else:
                    buckets[2] += 1

    return StabilityMetrics(
        today_tomorrow_changes=buckets[0],
        days_3_4_changes=buckets[1],
        days_5_7_changes=buckets[2],
        total_stability_penalty=solver.objective_value
    )


def _compute_fairness(solver, assign, input: SolverInput, feasible) -> list[FairnessMetrics]:
    burden_map = {"hated": True, "disliked": False}
    result = []
    for p_idx, person in enumerate(input.people):
        hated = disliked = total = 0
        for s_idx, slot in enumerate(input.task_slots):
            if feasible and solver.value(assign[(s_idx, p_idx)]) == 1:
                total += 1
                if slot.burden_level == "hated":
                    hated += 1
                elif slot.burden_level == "disliked":
                    disliked += 1
        result.append(FairnessMetrics(
            person_id=person.person_id,
            hated_tasks_assigned=hated,
            disliked_tasks_assigned=disliked,
            total_assigned=total
        ))
    return result


def _build_explanation(feasible, timed_out, uncovered, input: SolverInput) -> list[str]:
    fragments = []
    if timed_out:
        fragments.append("Solver reached time limit — returning best known result.")
    if not feasible and not timed_out:
        fragments.append("No feasible solution found under current hard constraints.")
    if uncovered:
        fragments.append(f"{len(uncovered)} slot(s) could not be fully staffed.")
    if feasible and not uncovered:
        fragments.append("All slots fully staffed.")
    return fragments
