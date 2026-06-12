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
    SolverInput, TaskSlot, HomeLeaveConfig, PersonEligibility, PresenceWindow, CumulativeTracking,
    SpecialDay,
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


def _compute_max_concurrent_mission_headcount(
    slots: list[TaskSlot],
    horizon_start_ts: int,
    horizon_duration_hours: int,
) -> int:
    """
    Compute the maximum total mission headcount needed at any single hour
    across the scheduling horizon.

    For each hour in the horizon, sums the required_headcount of all slots
    that overlap that hour. Returns the maximum across all hours.
    """
    if not slots:
        return 0

    max_headcount = 0
    for hour in range(horizon_duration_hours):
        hour_start_ts = horizon_start_ts + hour * 3600
        hour_end_ts = hour_start_ts + 3600
        headcount_at_hour = 0
        for slot in slots:
            slot_start = _to_timestamp(slot.starts_at)
            slot_end = _to_timestamp(slot.ends_at)
            # Slot overlaps this hour if slot_start < hour_end AND hour_start < slot_end
            if slot_start < hour_end_ts and hour_start_ts < slot_end:
                headcount_at_hour += slot.required_headcount
        if headcount_at_hour > max_headcount:
            max_headcount = headcount_at_hour

    return max_headcount


def _add_dynamic_concurrent_leave_cap(
    model: cp_model.CpModel,
    home_leave_vars: dict[tuple[int, int], "cp_model.IntVar"],
    slots: list[TaskSlot],
    people: list,
    num_people: int,
    horizon_start_ts: int,
    horizon_duration_hours: int,
    leave_duration_hours_int: int,
    possible_start_hours: list[int],
    emergency_person_ids: set[str],
    presence_windows: list[PresenceWindow],
) -> None:
    """
    Add a dynamic concurrent-leave cap constraint to the model.

    The cap ensures that at any hour, the number of people on home-leave
    does not exceed: len(people) - max_concurrent_mission_headcount.

    This guarantees enough people are always available for missions regardless
    of the leave_capacity setting.

    If max_concurrent_leave <= 0, it is set to 1 (at least 1 person can be on leave).

    People already at home (presence_window.state = "at_home") are counted
    against the cap but cannot be recalled — they retain priority.
    """
    max_mission_headcount = _compute_max_concurrent_mission_headcount(
        slots, horizon_start_ts, horizon_duration_hours
    )
    max_concurrent_leave = num_people - max_mission_headcount
    if max_concurrent_leave <= 0:
        max_concurrent_leave = 1

    logger.info(
        "Dynamic concurrent-leave cap: %d (people=%d, max_mission_headcount=%d)",
        max_concurrent_leave, num_people, max_mission_headcount
    )

    # Count people already at home per hour — they occupy cap slots but
    # cannot be recalled, so the effective cap for NEW leave vars is reduced.
    already_at_home_per_hour: dict[int, int] = {}
    for hour in range(horizon_duration_hours):
        hour_start_ts = horizon_start_ts + hour * 3600
        hour_end_ts = hour_start_ts + 3600
        count = 0
        for pw in presence_windows:
            if pw.state != "at_home":
                continue
            pw_start = _to_timestamp(pw.starts_at)
            pw_end = _to_timestamp(pw.ends_at)
            # Presence window overlaps this hour
            if pw_start < hour_end_ts and hour_start_ts < pw_end:
                count += 1
        already_at_home_per_hour[hour] = count

    # Apply the cap per hour: active_leave_vars + already_at_home <= max_concurrent_leave
    for hour in range(horizon_duration_hours):
        active_vars = []
        for p_idx in range(num_people):
            if people[p_idx].person_id in emergency_person_ids:
                continue
            for start_h in possible_start_hours:
                if start_h <= hour < start_h + leave_duration_hours_int:
                    if (p_idx, start_h) in home_leave_vars:
                        active_vars.append(home_leave_vars[(p_idx, start_h)])
        if active_vars:
            at_home_count = already_at_home_per_hour.get(hour, 0)
            effective_cap = max_concurrent_leave - at_home_count
            if effective_cap < 0:
                effective_cap = 0
            model.add(sum(active_vars) <= effective_cap)


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
    cumulative_tracking: list[CumulativeTracking] | None = None,
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

    possible_start_hours = list(range(0, max_start_hour + 1, 4))  # every 4 hours to reduce search space

    # ── Pre-filter: only create leave vars for people who could be eligible ───
    # A person is potentially eligible if their cumulative hours + horizon time >= threshold.
    # This dramatically reduces the search space for groups where most people aren't eligible yet.
    threshold_seconds = int(config.eligibility_threshold_hours * 3600)
    
    # Build cumulative hours lookup
    cumulative_seconds_lookup: dict[str, int] = {}
    if cumulative_tracking:
        for ct in cumulative_tracking:
            cumulative_seconds_lookup[ct.person_id] = int(ct.consecutive_hours_at_base * 3600)

    eligible_person_indices: set[int] = set()
    for p_idx, person in enumerate(people):
        if person.person_id in emergency_person_ids:
            continue
        # Check if this person could become eligible within the horizon
        cumulative_secs = cumulative_seconds_lookup.get(person.person_id, 0)
        # Even without cumulative data, if threshold is low enough they might qualify
        # from presence windows alone (horizon provides up to horizon_duration_seconds of base time)
        max_possible_base_time = cumulative_secs + horizon_duration_seconds
        if max_possible_base_time >= threshold_seconds:
            eligible_person_indices.add(p_idx)

    if not eligible_person_indices:
        logger.info("No people are potentially eligible for home-leave within this horizon.")
        return {}

    logger.info(
        "Home-leave pre-filter: %d/%d people potentially eligible (threshold=%dh)",
        len(eligible_person_indices), num_people, int(config.eligibility_threshold_hours))

    # ── Create boolean decision variables ─────────────────────────────────────
    # home_leave_vars[(p_idx, start_hour)] = 1 if person p_idx goes on leave
    # starting at that hour offset from horizon_start.
    home_leave_vars: dict[tuple[int, int], cp_model.IntVar] = {}
    for p_idx in eligible_person_indices:
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

    # ── Constraint 1b: Dynamic concurrent-leave cap ───────────────────────────
    # Ensures enough people are always available for missions regardless of
    # the leave_capacity setting. The cap is:
    #   max_concurrent_leave = len(people) - max_concurrent_mission_headcount
    # This is ADDITIVE to the per-hour leave_capacity constraint above.
    # People already at home (presence_window.state = "at_home") are counted
    # against the cap but cannot be recalled (they retain priority).
    _add_dynamic_concurrent_leave_cap(
        model=model,
        home_leave_vars=home_leave_vars,
        slots=slots,
        people=people,
        num_people=num_people,
        horizon_start_ts=horizon_start_ts,
        horizon_duration_hours=horizon_duration_hours,
        leave_duration_hours_int=leave_duration_hours_int,
        possible_start_hours=possible_start_hours,
        emergency_person_ids=emergency_person_ids,
        presence_windows=presence_windows,
    )

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

    # ── Constraint 4: At most one leave per person per horizon ───────────────
    # A person can only go on leave ONCE per solver run. This ensures they
    # must accumulate enough base time again before the next leave.
    for p_idx in range(num_people):
        if people[p_idx].person_id in emergency_person_ids:
            continue
        person_vars = [
            home_leave_vars[(p_idx, h)]
            for h in possible_start_hours
            if (p_idx, h) in home_leave_vars
        ]
        if len(person_vars) > 1:
            model.add(sum(person_vars) <= 1)

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


def add_home_leave_special_day_preference(
    home_leave_vars: dict[tuple[int, int], "cp_model.IntVar"],
    config: HomeLeaveConfig,
    horizon_start_ts: int,
    special_days: list[SpecialDay] | None = None,
) -> list:
    """
    Prefer placing home-leave windows on reviewed special days.

    This is a timing preference only. It does not create extra leave, relax
    coverage, bypass min-rest, or override capacity. A higher multiplier gives
    Shifter a stronger reason to choose a leave window that overlaps that date.
    """
    if not home_leave_vars or not special_days:
        return []

    balance = config.balance_value if config.balance_value is not None else 50
    base_weight = min(balance * 4, 400)
    if base_weight <= 0:
        return []

    leave_duration_seconds = int(config.leave_duration_hours * 3600)
    rewards = []

    for (p_idx, start_h), leave_var in home_leave_vars.items():
        leave_start_ts = horizon_start_ts + start_h * 3600
        leave_end_ts = leave_start_ts + leave_duration_seconds
        slot_reward = 0

        for special_day in special_days:
            day_start = datetime(
                special_day.date.year,
                special_day.date.month,
                special_day.date.day,
                tzinfo=timezone.utc,
            )
            day_start_ts = int(day_start.timestamp())
            day_end_ts = day_start_ts + 24 * 3600
            if leave_start_ts < day_end_ts and day_start_ts < leave_end_ts:
                multiplier = max(1.0, float(special_day.home_leave_weight_multiplier or 1.0))
                slot_reward += int(base_weight * (multiplier - 1.0))

        if slot_reward > 0:
            rewards.append(leave_var * -slot_reward)

    return rewards


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
    cumulative_tracking: list[CumulativeTracking] | None = None,
) -> list:
    """
    Soft preference: once a person exceeds eligibility_threshold_hours of
    continuous free_in_base time, prefer sending them home.

    When cumulative_tracking is provided, each person's consecutive_hours_at_base
    is added to the hours computed within the current horizon before comparing
    against the eligibility threshold.

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
    # Formula: weight = min(balance_value × 10, 999), giving range [0, 999].
    # Default balance_value is 50 → weight 500.
    # Capped at coverage_weight - 1 (999) to ensure mission coverage (weight 1000)
    # ALWAYS takes priority over sending additional people home.
    # When balance_value is 0, weight is 0 (preference disabled).
    # When balance_value is 100, weight is 999 (max, still below coverage).
    # People already at home (presence_window.state = "at_home") retain highest
    # priority via the existing availability constraint — they are not affected.
    balance = config.balance_value if config.balance_value is not None else 50
    ELIGIBILITY_WEIGHT = min(balance * 10, 999)

    # Build cumulative hours lookup: person_id → consecutive_hours_at_base (in seconds)
    cumulative_seconds_lookup: dict[str, int] = {}
    if cumulative_tracking:
        for ct in cumulative_tracking:
            cumulative_seconds_lookup[ct.person_id] = int(ct.consecutive_hours_at_base * 3600)

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

        # Incorporate cumulative hours: reduce the remaining threshold by hours already at base
        # effective_threshold = max(0, threshold - cumulative_hours_already_at_base)
        cumulative_secs = cumulative_seconds_lookup.get(pid, 0)
        effective_threshold_seconds = max(0, threshold_seconds - cumulative_secs)

        # The person becomes eligible at latest_mission_end + effective_threshold
        eligible_ts = latest_mission_end_ts + effective_threshold_seconds
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
        # Penalty = ELIGIBILITY_WEIGHT * person's priority if NO leave slot is chosen after eligibility
        # Higher priority = higher penalty for NOT sending them home = solver tries harder to send them home
        any_leave_after_eligible = model.new_bool_var(f"hl_elig_{p_idx}")
        model.add_max_equality(
            any_leave_after_eligible,
            [home_leave_vars[(p_idx, h)] for h in eligible_start_hours]
        )

        # Penalty when no leave is taken after becoming eligible
        # Multiply by person's home_leave_priority (default 1.0, parents/students get 1.5-3.0)
        person_priority = people[p_idx].home_leave_priority if hasattr(people[p_idx], 'home_leave_priority') else 1.0
        person_weight = int(ELIGIBILITY_WEIGHT * person_priority)
        not_on_leave = model.new_bool_var(f"hl_not_leave_{p_idx}")
        model.add(not_on_leave == 1 - any_leave_after_eligible)
        penalties.append(not_on_leave * person_weight)

    return penalties
