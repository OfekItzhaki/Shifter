"""
Home-leave constraint and preference functions for the CP-SAT model.

This module handles closed-base group scheduling where personnel live on-base
and rotate between missions and home-leave. It provides:
  - Hard constraints: capacity, no-overlap with missions, min-rest gate, one-at-a-time
  - Soft preferences: eligibility threshold triggers preference to send people home
  - Fairness objective: minimize max deviation of base_time_ratio (in separate function)
"""
from ortools.sat.python import cp_model
from models.solver_input import (
    SolverInput, TaskSlot, HomeLeaveConfig, PersonEligibility, PresenceWindow
)
from datetime import datetime, timezone, timedelta
import logging

logger = logging.getLogger(__name__)


def _to_timestamp(dt) -> int:
    """Convert datetime or ISO string to Unix timestamp (seconds)."""
    if isinstance(dt, (int, float)):
        return int(dt)
    if isinstance(dt, str):
        dt = datetime.fromisoformat(dt.replace("Z", "+00:00"))
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return int(dt.timestamp())


def add_home_leave_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people: list[PersonEligibility],
    config: HomeLeaveConfig,
    horizon_start_ts: int,
    horizon_end_ts: int,
    presence_windows: list[PresenceWindow],
    emergency_person_ids: set[str] | None = None,
) -> dict[tuple[int, int], "cp_model.IntVar"]:
    """
    Creates home-leave decision variables and adds hard constraints.

    Returns:
        home_leave_vars: dict mapping (person_idx, slot_start_hour) to BoolVar.
            slot_start_hour is the hour offset from horizon_start.
    """
    emergency_person_ids = emergency_person_ids or set()
    num_people = len(people)
    leave_duration_seconds = int(config.leave_duration_hours * 3600)
    min_rest_seconds = int(config.min_rest_hours * 3600)
    horizon_duration_seconds = horizon_end_ts - horizon_start_ts
    horizon_duration_hours = horizon_duration_seconds // 3600

    # ── Generate possible leave start hours ───────────────────────────────────
    # A leave slot starting at hour h occupies [h, h + leave_duration_hours).
    # The slot must fit within the horizon.
    leave_duration_hours_int = int(config.leave_duration_hours)
    max_start_hour = horizon_duration_hours - leave_duration_hours_int

    if max_start_hour < 0:
        logger.warning(
            "Home-leave duration (%dh) exceeds horizon (%dh) — no leave slots generated.",
            leave_duration_hours_int, horizon_duration_hours
        )
        return {}

    possible_start_hours = list(range(0, max_start_hour + 1))

    # ── Create boolean decision variables ─────────────────────────────────────
    # home_leave_vars[(p_idx, start_hour)] = 1 if person p_idx goes on leave
    # starting at that hour offset from horizon_start.
    home_leave_vars: dict[tuple[int, int], cp_model.IntVar] = {}
    for p_idx in range(num_people):
        if people[p_idx].person_id in emergency_person_ids:
            continue  # emergency-bypassed people don't get leave slots
        for h in possible_start_hours:
            var = model.new_bool_var(f"hl_{p_idx}_{h}")
            home_leave_vars[(p_idx, h)] = var

    if not home_leave_vars:
        return home_leave_vars

    # ── Constraint 1: Capacity — at most leave_capacity people on leave per hour ──
    for hour in range(horizon_duration_hours):
        # Find all leave vars whose window covers this hour
        # A leave starting at start_hour covers [start_hour, start_hour + leave_duration_hours)
        active_vars = []
        for p_idx in range(num_people):
            if people[p_idx].person_id in emergency_person_ids:
                continue
            for start_h in possible_start_hours:
                if start_h <= hour < start_h + leave_duration_hours_int:
                    if (p_idx, start_h) in home_leave_vars:
                        active_vars.append(home_leave_vars[(p_idx, start_h)])
        if active_vars:
            model.add(sum(active_vars) <= config.leave_capacity)

    # ── Constraint 2: No-overlap with mission assignments ─────────────────────
    # If a person is on leave during a time window, they cannot be assigned to
    # any mission slot overlapping that window.
    for p_idx in range(num_people):
        if people[p_idx].person_id in emergency_person_ids:
            continue
        for start_h in possible_start_hours:
            if (p_idx, start_h) not in home_leave_vars:
                continue
            leave_var = home_leave_vars[(p_idx, start_h)]
            leave_start_ts = horizon_start_ts + start_h * 3600
            leave_end_ts = leave_start_ts + leave_duration_seconds

            for s_idx, slot in enumerate(slots):
                slot_start = _to_timestamp(slot.starts_at)
                slot_end = _to_timestamp(slot.ends_at)
                # Check overlap: slot_start < leave_end AND leave_start < slot_end
                if slot_start < leave_end_ts and leave_start_ts < slot_end:
                    # If leave is active, mission must be 0
                    model.add(
                        assign[(s_idx, p_idx)] + leave_var <= 1
                    )

    # ── Constraint 3: Min-rest gate before leave starts ───────────────────────
    # A person must have at least min_rest_hours of free time before leave starts.
    # This means no mission can end less than min_rest_hours before leave start.
    for p_idx in range(num_people):
        if people[p_idx].person_id in emergency_person_ids:
            continue
        for start_h in possible_start_hours:
            if (p_idx, start_h) not in home_leave_vars:
                continue
            leave_var = home_leave_vars[(p_idx, start_h)]
            leave_start_ts = horizon_start_ts + start_h * 3600

            for s_idx, slot in enumerate(slots):
                slot_end = _to_timestamp(slot.ends_at)
                # If mission ends within min_rest window before leave start,
                # both cannot be active simultaneously
                if slot_end <= leave_start_ts and (leave_start_ts - slot_end) < min_rest_seconds:
                    model.add(
                        assign[(s_idx, p_idx)] + leave_var <= 1
                    )

    # ── Constraint 4: One-at-a-time per person ────────────────────────────────
    # A person cannot have two overlapping leave slots.
    for p_idx in range(num_people):
        if people[p_idx].person_id in emergency_person_ids:
            continue
        person_start_hours = [
            h for h in possible_start_hours if (p_idx, h) in home_leave_vars
        ]
        for i, h1 in enumerate(person_start_hours):
            for h2 in person_start_hours[i + 1:]:
                # Two leave slots overlap if h1 < h2 + duration AND h2 < h1 + duration
                if h1 < h2 + leave_duration_hours_int and h2 < h1 + leave_duration_hours_int:
                    model.add(
                        home_leave_vars[(p_idx, h1)] + home_leave_vars[(p_idx, h2)] <= 1
                    )

    return home_leave_vars


def add_home_leave_fairness_objective(
    model: cp_model.CpModel,
    home_leave_vars: dict[tuple[int, int], "cp_model.IntVar"],
    assign: dict,
    slots: list[TaskSlot],
    people: list[PersonEligibility],
    config: HomeLeaveConfig,
    horizon_start_ts: int,
    horizon_end_ts: int,
) -> list:
    """
    Fairness objective: minimizes the maximum deviation of each person's
    base_time_ratio from the group mean.

    base_time_ratio ≈ total_base_hours / available_hours
    where total_base_hours = available_hours - total_home_hours
    and total_home_hours = sum of leave_duration_hours for each active leave var.

    Uses minimax formulation: introduces a variable `max_dev` that is >= each
    person's deviation from the mean, then minimizes max_dev * weight.

    Weight = 500 (below coverage at 1000, above burden at ≤99).

    Returns penalty terms to add to the objective (minimization).
    Skips fairness if fewer than 2 people have leave vars.

    Validates: Requirements 6.3, 6.4, 6.6
    """
    FAIRNESS_WEIGHT = 500

    if not home_leave_vars:
        return []

    # Determine which people have leave vars
    people_with_vars: set[int] = set()
    for (p_idx, _) in home_leave_vars:
        people_with_vars.add(p_idx)

    # Skip fairness if fewer than 2 eligible members (Requirement 6.6)
    if len(people_with_vars) < 2:
        return []

    eligible_indices = sorted(people_with_vars)
    num_eligible = len(eligible_indices)
    leave_duration_hours = config.leave_duration_hours
    horizon_duration_hours = (horizon_end_ts - horizon_start_ts) / 3600.0

    # For each eligible person, compute available_hours (use full horizon as proxy)
    # and total_home_hours as a linear expression of their leave vars.
    # base_time_ratio = (available_hours - total_home_hours) / available_hours
    #                 = 1 - total_home_hours / available_hours

    # To work in integer arithmetic for CP-SAT, we scale ratios by a factor.
    # Let's work in "leave units" — each active leave var contributes
    # leave_duration_hours to total_home_hours.
    #
    # We want to minimize max |ratio_i - mean_ratio|.
    # ratio_i = 1 - (leave_count_i * leave_duration_hours) / available_hours
    # mean_ratio = 1 - (total_leave_count * leave_duration_hours) / (num_eligible * available_hours)
    #
    # deviation_i = ratio_i - mean_ratio
    #             = (total_leave_count * leave_duration_hours) / (num_eligible * available_hours)
    #               - (leave_count_i * leave_duration_hours) / available_hours
    #             = leave_duration_hours / available_hours * (total_leave_count / num_eligible - leave_count_i)
    #
    # Since leave_duration_hours / available_hours is a constant, minimizing
    # max |deviation_i| is equivalent to minimizing max |total_leave_count / num_eligible - leave_count_i|.
    #
    # To avoid fractions, multiply through by num_eligible:
    # We minimize max |total_leave_count - num_eligible * leave_count_i|
    # (scaled by a constant factor that we absorb into the weight).

    # Compute leave_count for each eligible person as a linear expression
    leave_counts: dict[int, list] = {}
    for p_idx in eligible_indices:
        person_vars = [
            home_leave_vars[(p_idx, h)]
            for h in range(0, int(horizon_duration_hours) - int(leave_duration_hours) + 1)
            if (p_idx, h) in home_leave_vars
        ]
        leave_counts[p_idx] = person_vars

    # total_leave_count = sum of all leave vars across eligible people
    all_leave_vars = []
    for p_idx in eligible_indices:
        all_leave_vars.extend(leave_counts[p_idx])

    # Upper bound on any person's leave count
    max_possible_leaves_per_person = max(len(leave_counts[p_idx]) for p_idx in eligible_indices)
    # Upper bound on total leaves
    max_possible_total = sum(len(leave_counts[p_idx]) for p_idx in eligible_indices)

    # The deviation for person i (scaled by num_eligible) is:
    # dev_i = total_leave_count - num_eligible * leave_count_i
    # We need |dev_i|, so we introduce abs_dev_i >= dev_i and abs_dev_i >= -dev_i.
    # Then max_dev >= abs_dev_i for all i.

    # Upper bound for max_dev: num_eligible * max_possible_leaves_per_person + max_possible_total
    max_dev_bound = num_eligible * max_possible_leaves_per_person + max_possible_total

    max_dev = model.new_int_var(0, max_dev_bound, "hl_fairness_max_dev")

    for p_idx in eligible_indices:
        person_leave_vars = leave_counts[p_idx]

        # dev_i = sum(all_leave_vars) - num_eligible * sum(person_leave_vars)
        # abs_dev_i >= dev_i  AND  abs_dev_i >= -dev_i
        # max_dev >= abs_dev_i

        abs_dev = model.new_int_var(0, max_dev_bound, f"hl_abs_dev_{p_idx}")

        # dev_i = total - num_eligible * person_count
        # abs_dev >= total - num_eligible * person_count
        # abs_dev >= -(total - num_eligible * person_count) = num_eligible * person_count - total

        # Constraint: abs_dev >= sum(all_leave_vars) - num_eligible * sum(person_leave_vars)
        model.add(
            abs_dev >= sum(all_leave_vars) - num_eligible * sum(person_leave_vars)
        )
        # Constraint: abs_dev >= num_eligible * sum(person_leave_vars) - sum(all_leave_vars)
        model.add(
            abs_dev >= num_eligible * sum(person_leave_vars) - sum(all_leave_vars)
        )
        # Constraint: max_dev >= abs_dev
        model.add(max_dev >= abs_dev)

    # The penalty is max_dev * FAIRNESS_WEIGHT.
    # We scale by leave_duration_hours / (horizon_duration_hours * num_eligible)
    # to make the weight meaningful in ratio-space, but since the weight is
    # already tuned (500), we just use max_dev directly.
    return [max_dev * FAIRNESS_WEIGHT]


def add_home_leave_eligibility_preference(
    model: cp_model.CpModel,
    home_leave_vars: dict[tuple[int, int], "cp_model.IntVar"],
    assign: dict,
    slots: list[TaskSlot],
    people: list[PersonEligibility],
    config: HomeLeaveConfig,
    horizon_start_ts: int,
    horizon_end_ts: int,
    presence_windows: list[PresenceWindow],
) -> list:
    """
    Soft preference: once a person exceeds eligibility_threshold_hours of
    continuous free_in_base time, prefer sending them home.

    Returns a list of penalty terms to add to the objective (minimization).
    A penalty is incurred when a person is eligible but NOT on leave.
    """
    if not home_leave_vars:
        return []

    penalties = []
    num_people = len(people)
    threshold_seconds = int(config.eligibility_threshold_hours * 3600)
    leave_duration_hours_int = int(config.leave_duration_hours)
    horizon_duration_seconds = horizon_end_ts - horizon_start_ts
    horizon_duration_hours = horizon_duration_seconds // 3600
    max_start_hour = horizon_duration_hours - leave_duration_hours_int

    if max_start_hour < 0:
        return []

    # Weight for the eligibility preference — derived from balance_value (0–100).
    # Formula: weight = balance_value × 4, giving range [0, 400].
    # Default balance_value is 50 → weight 200 (backward compatible with previous hardcoded value).
    # When balance_value is 0, weight is 0 (preference disabled).
    # When balance_value is 100, weight is 400 (maximum preference).
    balance = config.balance_value if config.balance_value is not None else 50
    ELIGIBILITY_WEIGHT = balance * 4

    # For each person, determine the earliest hour at which they become eligible
    # based on their last mission end (from presence windows or slot assignments).
    # Since we can't know the actual assignment at model-build time, we use a
    # simplified approach: for each person, find the latest mission end from
    # presence_windows that are on_mission, and compute when they become eligible.
    # Then penalize if no leave var is active after that point.

    for p_idx, person in enumerate(people):
        pid = person.person_id
        person_start_hours = [
            h for h in range(0, max_start_hour + 1)
            if (p_idx, h) in home_leave_vars
        ]
        if not person_start_hours:
            continue

        # Find the latest on_mission end for this person before/within the horizon
        latest_mission_end_ts = horizon_start_ts  # default: free since horizon start
        for pw in presence_windows:
            if pw.person_id == pid and pw.state == "on_mission":
                pw_end = _to_timestamp(pw.ends_at)
                if pw_end > latest_mission_end_ts and pw_end <= horizon_end_ts:
                    latest_mission_end_ts = pw_end

        # The person becomes eligible at latest_mission_end + threshold
        eligible_ts = latest_mission_end_ts + threshold_seconds
        eligible_hour = max(0, (eligible_ts - horizon_start_ts) // 3600)

        if eligible_hour >= horizon_duration_hours:
            # Person doesn't become eligible within the horizon
            continue

        # Find leave start hours that are at or after the eligibility point
        eligible_start_hours = [
            h for h in person_start_hours if h >= eligible_hour
        ]

        if not eligible_start_hours:
            continue

        # Soft preference: at least one leave slot should be active after eligibility
        # Penalty = ELIGIBILITY_WEIGHT if NO leave slot is chosen after eligibility
        any_leave_after_eligible = model.new_bool_var(f"hl_elig_{p_idx}")
        model.add_max_equality(
            any_leave_after_eligible,
            [home_leave_vars[(p_idx, h)] for h in eligible_start_hours]
        )

        # Penalty when no leave is taken after becoming eligible
        not_on_leave = model.new_bool_var(f"hl_not_leave_{p_idx}")
        model.add(not_on_leave == 1 - any_leave_after_eligible)
        penalties.append(not_on_leave * ELIGIBILITY_WEIGHT)

    return penalties
