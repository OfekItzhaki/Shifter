"""
Soft objective functions for the CP-SAT model.

Priority order (per spec Section 18):
  1. Hard constraints (enforced elsewhere)
  2. Maximize staffing coverage (minimize uncovered slots)
  3. Minimize changes vs baseline — today+tomorrow (highest weight)
  4. Minimize changes vs baseline — days 3-7 (lower weight)
  5. Minimize soft constraint violations
  6. Improve fairness of burden distribution

All objectives are combined into a single weighted minimization.
"""
from ortools.sat.python import cp_model
from models.solver_input import SolverInput, TaskSlot
from solver.constraints import _to_timestamp
from datetime import date, datetime, timezone


def build_objective(
    model: cp_model.CpModel,
    assign: dict,
    input: SolverInput
) -> list:
    """
    Builds and returns the list of weighted penalty terms.
    Caller passes this to model.minimize(sum(penalties)).
    """
    penalties = []

    slots = input.task_slots
    people = input.people
    num_people = len(people)
    horizon_start = input.horizon_start

    baseline_set = {
        (a.slot_id, a.person_id)
        for a in input.baseline_assignments
    }

    # ── Objective 2: coverage — penalise uncovered headcount ─────────────────
    # Weight is very high so coverage is prioritised over stability
    coverage_weight = 1000  # see SchedulingConstants.CoverageWeight in the .NET layer
    for s_idx, slot in enumerate(slots):
        assigned_count = sum(assign[(s_idx, p_idx)] for p_idx in range(num_people))
        # Shortfall = required - assigned (clamped to 0)
        shortfall = model.new_int_var(0, slot.required_headcount, f"shortfall_{s_idx}")
        model.add(shortfall >= slot.required_headcount - assigned_count)
        penalties.append(coverage_weight * shortfall)

    # ── Objectives 3 & 4: stability — penalise deviations from baseline ───────
    for s_idx, slot in enumerate(slots):
        weight = _stability_weight(slot, horizon_start, input.stability_weights)
        weight_int = int(weight * 100)

        for p_idx, person in enumerate(people):
            was_assigned = (slot.slot_id, person.person_id) in baseline_set

            if was_assigned:
                # Penalise removing an existing assignment (high cost)
                removed = model.new_bool_var(f"removed_{s_idx}_{p_idx}")
                model.add(removed == (1 - assign[(s_idx, p_idx)]))
                penalties.append(weight_int * removed)
            else:
                # Penalise adding a new assignment where none existed (lower cost)
                penalties.append((weight_int // 10) * assign[(s_idx, p_idx)])

    # ── Objective 6: fairness — penalise assigning hated/disliked to burdened people ──
    burden_map = {
        "hated": 4,
        "disliked": 2,
        "neutral": 0,
        "favorable": 0
    }

    fairness_history = {
        f.person_id: f.disliked_hated_score_7d
        for f in input.fairness_counters
    }

    for s_idx, slot in enumerate(slots):
        slot_burden = burden_map.get(slot.burden_level, 0)
        if slot_burden == 0:
            continue

        for p_idx, person in enumerate(people):
            history_score = fairness_history.get(person.person_id, 0)
            # Higher history score = higher penalty for assigning another burden task
            fairness_penalty = slot_burden * max(0, history_score)
            if fairness_penalty > 0:
                penalties.append(fairness_penalty * assign[(s_idx, p_idx)])

    return penalties


def _stability_weight(slot: TaskSlot, horizon_start: date, weights) -> float:
    """Return the stability penalty weight based on the slot's time bucket."""
    slot_dt = slot.starts_at
    if isinstance(slot_dt, str):
        slot_dt = datetime.fromisoformat(slot_dt.replace("Z", "+00:00"))
    if hasattr(slot_dt, 'date'):
        slot_date = slot_dt.date()
    else:
        slot_date = slot_dt

    if isinstance(horizon_start, str):
        horizon_start = date.fromisoformat(horizon_start)

    delta = (slot_date - horizon_start).days

    if delta <= 1:
        return weights.today_tomorrow   # very high — today + tomorrow
    elif delta <= 3:
        return weights.days_3_4         # medium
    else:
        return weights.days_5_7         # low
