"""
CP-SAT scheduling engine.
Wires together all hard constraints and weighted soft objectives.
"""
from ortools.sat.python import cp_model
from models.solver_input import SolverInput
from models.solver_output import (
    SolverOutput, AssignmentResult, StabilityMetrics, FairnessMetrics, HardConflict,
    HomeLeaveAssignment, HomeLeaveMetric,
)
from solver.constraints import (
    add_headcount_constraints,
    add_no_duplicate_assignment,
    add_no_overlap_constraints,
    add_no_consecutive_double_shift_constraints,
    add_min_rest_constraints,
    add_qualification_constraints,
    add_role_constraints,
    add_restriction_constraints,
    add_max_task_type_per_period_constraints,
    add_availability_constraints,
    expand_role_constraints,
    expand_group_constraints,
    add_locked_slot_constraints,
    add_composition_constraints,
)
from solver.home_leave import (
    add_home_leave_constraints,
    add_home_leave_eligibility_preference,
    add_home_leave_fairness_objective,
    add_home_leave_special_day_preference,
)
from solver.objectives import build_objective
from solver.i18n import t
from datetime import datetime, timezone
import os
import time
import logging

logger = logging.getLogger(__name__)

SOLVER_TIMEOUT = int(os.getenv("SOLVER_TIMEOUT_SECONDS", "15"))

# Absolute minimum rest hours for closed-base groups — prevents misconfigured
# hard constraints from allowing unsafe rest gaps.
_CLOSED_BASE_MIN_REST_FLOOR = 8.0


def _resolve_min_rest_hours_closed_base(
    config_value: float,
    hard_constraint_value: float | None,
    run_id: str,
) -> float:
    """
    Resolve min_rest_hours for a closed-base group.

    config_value is the admin's configured setting from the group/home-leave payload:
      - 0   = not configured; fall back to the hard-constraint/default chain
      - > 0 = admin-configured rest requirement, clamped to the closed-base floor

    Closed-base scheduling never treats rest as disabled. This keeps long shifts
    and home-leave scheduling under the same hard safety floor.

    Returns the resolved min_rest_hours (>= 0).
    """
    if config_value > 0:
        resolved = max(config_value, _CLOSED_BASE_MIN_REST_FLOOR)
        logger.info(
            "[run=%s] min_rest_hours resolved to %.1fh (source: group config %.1fh).",
            run_id, resolved, config_value,
        )
        return resolved

    # config_value <= 0 means not set; fall back with the 8h floor.
    if hard_constraint_value is not None:
        resolved = max(hard_constraint_value, _CLOSED_BASE_MIN_REST_FLOOR)
        logger.warning(
            "[run=%s] min_rest_hours: no explicit config — "
            "using hard constraint rule value (%.1fh), clamped to floor %.1fh.",
            run_id, hard_constraint_value, resolved,
        )
        return resolved

    logger.warning(
        "[run=%s] min_rest_hours: no config, no constraint — using default %.1fh.",
        run_id, _CLOSED_BASE_MIN_REST_FLOOR,
    )
    return _CLOSED_BASE_MIN_REST_FLOOR


def solve(input: SolverInput) -> SolverOutput:
    t0 = time.time()
    model = cp_model.CpModel()
    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = SOLVER_TIMEOUT

    people = input.people
    slots = input.task_slots
    num_people = len(people)
    num_slots = len(slots)

    if num_slots == 0 or num_people == 0:
        return _empty_result(input)

    # ── Expand role/group constraints to person-scoped constraints ────────────
    # Role and group constraints are fan-out shortcuts: a single constraint
    # targeting a role or group is expanded to one person-scoped copy per member.
    # This happens before the CP-SAT model is built so all constraint functions
    # only need to handle person-scoped constraints.
    hard_constraints, soft_constraints, emergency_constraints = expand_role_constraints(
        list(input.hard_constraints),
        list(input.soft_constraints),
        list(input.emergency_constraints),
        input.people,
    )
    hard_constraints, soft_constraints, emergency_constraints = expand_group_constraints(
        hard_constraints, soft_constraints, emergency_constraints, input.people
    )
    # Rebuild a modified input with expanded constraints for downstream functions
    input = input.model_copy(update={
        "hard_constraints": hard_constraints,
        "soft_constraints": soft_constraints,
        "emergency_constraints": emergency_constraints,
    })

    # ── Decision variables ────────────────────────────────────────────────────
    # assign[slot_idx][person_idx] = 1 if person is assigned to slot
    assign = {
        (s_idx, p_idx): model.new_bool_var(f"a_{s_idx}_{p_idx}")
        for s_idx in range(num_slots)
        for p_idx in range(num_people)
    }

    # ── Hard constraints ──────────────────────────────────────────────────────
    # Build emergency bypass sets first — people/slots covered by emergency
    # constraints skip availability, rest, and overlap enforcement.
    emergency_person_ids, emergency_slot_ids = _build_emergency_bypass(input)

    add_headcount_constraints(model, assign, slots, num_people)
    add_no_duplicate_assignment(model, assign, num_slots, num_people)
    add_no_overlap_constraints(model, assign, slots, people, num_people, emergency_person_ids)
    add_no_consecutive_double_shift_constraints(model, assign, slots, people, num_people, emergency_person_ids)
    add_qualification_constraints(model, assign, slots, people, num_people, emergency_person_ids)
    add_role_constraints(model, assign, slots, people, num_people, emergency_person_ids)
    add_restriction_constraints(model, assign, slots, people, num_people, input.hard_constraints)
    add_availability_constraints(
        model, assign, slots, people, num_people,
        input.availability_windows, input.presence_windows, emergency_person_ids)

    # Parent schedule cascading: treat parent assignments as blocked presence windows
    if input.parent_schedule:
        _add_parent_schedule_constraints(model, assign, slots, people, num_people, input.parent_schedule)

    # Extract min_rest_hours from hard constraints if configured
    rest_rules = [c for c in input.hard_constraints if c.rule_type == "min_rest_hours"]
    hard_constraint_rest_hours = float(rest_rules[0].payload.get("hours", 8)) if rest_rules else None

    # ── Min-rest resolution ───────────────────────────────────────────────────
    # For closed-base groups (home_leave_config enabled), use explicit fallback chain:
    #   config value > hard constraint rule > 8.0 default
    # For non-closed-base groups, use existing logic unchanged.
    home_leave_cfg = input.home_leave_config
    if home_leave_cfg and home_leave_cfg.enabled:
        rest_hours = _resolve_min_rest_hours_closed_base(
            config_value=home_leave_cfg.min_rest_hours,
            hard_constraint_value=hard_constraint_rest_hours,
            run_id=input.run_id,
        )
        rest_soft_penalties = None  # None disables soft penalty path — all rest is hard
    else:
        # Non-closed-base: use hard constraint rule value or 8.0 default (unchanged)
        rest_hours = hard_constraint_rest_hours if hard_constraint_rest_hours is not None else 8.0
        rest_soft_penalties = []

    add_min_rest_constraints(model, assign, slots, people, num_people, rest_hours, emergency_person_ids, rest_soft_penalties)

    add_max_task_type_per_period_constraints(
        model, assign, slots, people, num_people,
        input.hard_constraints, input.fairness_counters)

    # ── Locked slots (manual overrides) ──────────────────────────────────────
    # Force the solver to keep manually overridden assignments unchanged.
    locked_slot_ids = set(getattr(input, "locked_slot_ids", []) or [])
    if locked_slot_ids:
        add_locked_slot_constraints(
            model, assign, slots, people, num_people,
            locked_slot_ids, input.baseline_assignments
        )

    # ── Soft objectives ───────────────────────────────────────────────────────
    penalties = build_objective(model, assign, input)
    # Add soft rest penalties for 24h shifts (only when not in closed-base hard mode)
    if rest_soft_penalties:
        penalties.extend(rest_soft_penalties)

    # ── Composition constraints (mandatory/optional qualification seats per slot) ──
    # Collected as (var, weight) tuples and added to the minimisation objective.
    composition_penalty_vars: list[tuple] = []
    add_composition_constraints(
        model, assign, slots, people, num_people,
        composition_penalty_vars, emergency_person_ids)
    for penalty_var, weight in composition_penalty_vars:
        penalties.append(penalty_var * weight)

    # ── Home-leave constraints and objectives ─────────────────────────────────
    home_leave_vars: dict = {}
    if home_leave_cfg and home_leave_cfg.enabled:
        # Compute horizon timestamps from input dates
        horizon_start_dt = input.horizon_start
        if isinstance(horizon_start_dt, str):
            from datetime import date as _date
            horizon_start_dt = _date.fromisoformat(horizon_start_dt)
        horizon_end_dt = input.horizon_end
        if isinstance(horizon_end_dt, str):
            from datetime import date as _date
            horizon_end_dt = _date.fromisoformat(horizon_end_dt)

        horizon_start_ts = int(datetime(
            horizon_start_dt.year, horizon_start_dt.month, horizon_start_dt.day,
            tzinfo=timezone.utc
        ).timestamp())
        horizon_end_ts = int(datetime(
            horizon_end_dt.year, horizon_end_dt.month, horizon_end_dt.day,
            tzinfo=timezone.utc
        ).timestamp())

        # Generate home-leave decision variables and add hard constraints
        home_leave_vars = add_home_leave_constraints(
            model=model,
            assign=assign,
            slots=slots,
            people=people,
            config=home_leave_cfg,
            horizon_start_ts=horizon_start_ts,
            horizon_end_ts=horizon_end_ts,
            presence_windows=input.presence_windows,
            emergency_person_ids=emergency_person_ids,
            cumulative_tracking=input.cumulative_tracking,
        )

        # Add eligibility preference (soft)
        eligibility_penalties = add_home_leave_eligibility_preference(
            model=model,
            home_leave_vars=home_leave_vars,
            assign=assign,
            slots=slots,
            people=people,
            config=home_leave_cfg,
            horizon_start_ts=horizon_start_ts,
            horizon_end_ts=horizon_end_ts,
            presence_windows=input.presence_windows,
            cumulative_tracking=input.cumulative_tracking or None,
        )
        penalties.extend(eligibility_penalties)

        # Add fairness objective (soft)
        fairness_penalties = add_home_leave_fairness_objective(
            model=model,
            home_leave_vars=home_leave_vars,
            assign=assign,
            slots=slots,
            people=people,
            config=home_leave_cfg,
            horizon_start_ts=horizon_start_ts,
            horizon_end_ts=horizon_end_ts,
        )
        penalties.extend(fairness_penalties)

        special_day_penalties = add_home_leave_special_day_preference(
            home_leave_vars=home_leave_vars,
            config=home_leave_cfg,
            horizon_start_ts=horizon_start_ts,
            special_days=input.special_days,
        )
        penalties.extend(special_day_penalties)

    model.minimize(sum(penalties))

    # ── Preview mode configuration ───────────────────────────────────────────
    if input.preview_mode:
        solver.parameters.max_time_in_seconds = 3.0
        solver.parameters.num_workers = 1
        solver.parameters.log_search_progress = False

    # ── Solve ─────────────────────────────────────────────────────────────────
    constraint_build_ms = int((time.time() - t0) * 1000)
    logger.info("Constraint building took %dms. Starting CP-SAT solve (timeout=%ds)...",
                constraint_build_ms, solver.parameters.max_time_in_seconds)
    start_time = time.time()
    status = solver.solve(model)
    solver_time_ms = int((time.time() - start_time) * 1000)

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

    # NOTE: We do NOT flip feasible=False when all slots are uncovered.
    # With >= headcount constraints, a feasible-but-partial solution is valid —
    # the coverage objective already penalises shortfalls heavily (weight=1000).
    # Flipping to infeasible here would discard a real (partial) solve result.
    stability = _compute_stability(solver, assign, input, feasible)
    fairness  = _compute_fairness(solver, assign, input, feasible)

    # ── Extract home-leave results ────────────────────────────────────────────
    home_leave_assignments: list[HomeLeaveAssignment] = []
    home_leave_metrics: list[HomeLeaveMetric] = []
    fairness_variance: float | None = None

    if home_leave_cfg and home_leave_cfg.enabled and feasible and home_leave_vars:
        leave_duration_hours = home_leave_cfg.leave_duration_hours
        horizon_duration_hours = (horizon_end_ts - horizon_start_ts) / 3600.0

        # Count active leave slots per person
        person_leave_counts: dict[int, int] = {}
        for (p_idx, start_h), var in home_leave_vars.items():
            if solver.value(var) == 1:
                person_leave_counts[p_idx] = person_leave_counts.get(p_idx, 0) + 1
                # Build HomeLeaveAssignment
                leave_start_ts = horizon_start_ts + start_h * 3600
                leave_end_ts = leave_start_ts + int(leave_duration_hours * 3600)
                starts_at_iso = datetime.fromtimestamp(leave_start_ts, tz=timezone.utc).isoformat().replace("+00:00", "Z")
                ends_at_iso = datetime.fromtimestamp(leave_end_ts, tz=timezone.utc).isoformat().replace("+00:00", "Z")
                home_leave_assignments.append(HomeLeaveAssignment(
                    person_id=people[p_idx].person_id,
                    starts_at=starts_at_iso,
                    ends_at=ends_at_iso,
                ))

        # Build HomeLeaveMetric for each person
        base_time_ratios: list[float] = []
        for p_idx, person in enumerate(people):
            if person.person_id in emergency_person_ids:
                continue
            leave_slot_count = person_leave_counts.get(p_idx, 0)
            total_home_hours = leave_slot_count * leave_duration_hours
            available_hours = horizon_duration_hours
            total_base_hours = available_hours - total_home_hours
            base_time_ratio = round(total_base_hours / available_hours, 4) if available_hours > 0 else 1.0

            home_leave_metrics.append(HomeLeaveMetric(
                person_id=person.person_id,
                total_base_hours=total_base_hours,
                total_home_hours=total_home_hours,
                base_time_ratio=base_time_ratio,
                leave_slot_count=leave_slot_count,
            ))
            base_time_ratios.append(base_time_ratio)

        # Compute fairness_variance as variance of base_time_ratios
        if len(base_time_ratios) >= 2:
            mean_ratio = sum(base_time_ratios) / len(base_time_ratios)
            fairness_variance = round(
                sum((r - mean_ratio) ** 2 for r in base_time_ratios) / len(base_time_ratios),
                6
            )
        elif len(base_time_ratios) == 1:
            fairness_variance = 0.0

    fragments = _build_explanation(feasible, timed_out, uncovered, input)

    # Build hard conflicts:
    # - If truly infeasible (solver proved it): run full conflict analysis
    # - If timed out with 0 assignments: also run conflict analysis — the model
    #   is almost certainly infeasible (not enough people / rest constraints),
    #   and we want to give the admin a clear reason rather than a generic timeout.
    hard_conflicts = []
    if not feasible or (timed_out and len(assignments) == 0):
        hard_conflicts = _build_hard_conflicts(input, people, slots)

    return SolverOutput(
        run_id=input.run_id,
        feasible=feasible,
        timed_out=timed_out,
        assignments=assignments,
        uncovered_slot_ids=uncovered,
        hard_conflicts=hard_conflicts,
        soft_penalty_total=solver.objective_value if feasible else 0.0,
        stability_metrics=stability,
        fairness_metrics=fairness,
        explanation_fragments=fragments,
        home_leave_assignments=home_leave_assignments,
        home_leave_metrics=home_leave_metrics,
        fairness_variance=fairness_variance,
        solver_time_ms=solver_time_ms,
    )


def _add_parent_schedule_constraints(model, assign, slots, people, num_people, parent_schedule):
    """
    Block assignments that conflict with the parent group's published schedule.
    If a person is assigned in the parent schedule during a time window,
    they cannot be assigned to overlapping slots in the child group.
    """
    from solver.constraints import _to_timestamp

    if not parent_schedule:
        return

    # Build a map of person_id -> list of (start_ts, end_ts) from parent assignments
    parent_blocks: dict[str, list[tuple[int, int]]] = {}
    for pa in parent_schedule:
        parent_blocks.setdefault(pa.person_id, []).append(
            (_to_timestamp(pa.starts_at), _to_timestamp(pa.ends_at))
        )

    # Pre-compute slot timestamps
    slot_times = [(_to_timestamp(s.starts_at), _to_timestamp(s.ends_at)) for s in slots]

    for p_idx, person in enumerate(people):
        blocks = parent_blocks.get(person.person_id)
        if not blocks:
            continue
        for s_idx in range(len(slots)):
            slot_start, slot_end = slot_times[s_idx]
            # Check if any parent assignment overlaps this slot
            for block_start, block_end in blocks:
                if slot_start < block_end and slot_end > block_start:
                    model.Add(assign[(s_idx, p_idx)] == 0)
                    break


def _empty_result(input: SolverInput) -> SolverOutput:
    return SolverOutput(
        run_id=input.run_id,
        feasible=False,
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

    for s_idx, slot in enumerate(input.task_slots):
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
    locale = getattr(input, "locale", "en")
    fragments = []
    if timed_out:
        fragments.append(t(locale, "solver_timeout"))
    if not feasible and not timed_out:
        fragments.append(t(locale, "no_feasible"))
    if uncovered:
        fragments.append(t(locale, "uncovered_slots", n=len(uncovered)))
    if feasible and not uncovered:
        fragments.append(t(locale, "all_staffed"))
    return fragments


def _build_emergency_bypass(input: SolverInput) -> tuple[set[str], set[str]]:
    """
    Parse emergency constraints and return two sets:
    - emergency_person_ids: people who bypass availability/rest/overlap/qualification checks
    - emergency_slot_ids:   slots that bypass headcount and eligibility checks

    Supported emergency rule_types:
      emergency_person_bypass  — payload: { "person_id": "..." }
        Person can be assigned to any slot regardless of availability, rest, or qualifications.
      emergency_slot_bypass    — payload: { "slot_id": "..." }
        Slot can be filled by anyone regardless of qualifications or availability.
      emergency_space_bypass   — scope_type: "space", no payload needed
        ALL people and slots bypass all constraints (full emergency mode).
    """
    person_ids: set[str] = set()
    slot_ids:   set[str] = set()

    for c in input.emergency_constraints:
        if c.rule_type == "emergency_space_bypass":
            # Full bypass — add everyone and every slot
            person_ids.update(p.person_id for p in input.people)
            slot_ids.update(s.slot_id for s in input.task_slots)
        elif c.rule_type == "emergency_person_bypass":
            pid = c.payload.get("person_id") or c.scope_id
            if pid:
                person_ids.add(str(pid))
        elif c.rule_type == "emergency_slot_bypass":
            sid = c.payload.get("slot_id") or c.scope_id
            if sid:
                slot_ids.add(str(sid))

    return person_ids, slot_ids


def _build_hard_conflicts(input: SolverInput, people, slots) -> list[HardConflict]:
    """
    Analyse the input to produce locale-aware conflict explanations when
    the solver returns INFEASIBLE.
    """
    from solver.constraints import _is_allowed_double_shift_pair, _to_timestamp

    locale = getattr(input, "locale", "en")
    conflicts: list[HardConflict] = []

    # ── 0. Min-rest violations for closed-base groups ─────────────────────────
    # When home_leave_config is enabled, min-rest is strictly hard. Detect pairs
    # of slots that cannot both be assigned to the same person without violating
    # the rest window.
    home_leave_cfg = input.home_leave_config
    if home_leave_cfg and home_leave_cfg.enabled:
        emergency_person_ids, _ = _build_emergency_bypass(input)
        # Use the same fallback chain as the constraint builder
        rest_rules = [c for c in input.hard_constraints if c.rule_type == "min_rest_hours"]
        hard_constraint_rest_hours = float(rest_rules[0].payload.get("hours", 8)) if rest_rules else None
        resolved_rest_hours = _resolve_min_rest_hours_closed_base(
            config_value=home_leave_cfg.min_rest_hours,
            hard_constraint_value=hard_constraint_rest_hours,
            run_id=input.run_id,
        )
        min_rest_seconds = int(resolved_rest_hours * 3600)

        for p_idx, person in enumerate(people):
            if person.person_id in emergency_person_ids:
                continue  # emergency bypass — skip
            for s1_idx, slot1 in enumerate(slots):
                for s2_idx, slot2 in enumerate(slots):
                    if s2_idx <= s1_idx:
                        continue
                    end1 = _to_timestamp(slot1.ends_at)
                    start2 = _to_timestamp(slot2.starts_at)
                    end2 = _to_timestamp(slot2.ends_at)
                    start1 = _to_timestamp(slot1.starts_at)

                    # Check if the gap between the two slots is less than min_rest
                    violates = False
                    if (
                        end1 <= start2
                        and (start2 - end1) < min_rest_seconds
                        and not _is_allowed_double_shift_pair(slot1, slot2, end1, start2)
                    ):
                        violates = True
                    elif (
                        end2 <= start1
                        and (start1 - end2) < min_rest_seconds
                        and not _is_allowed_double_shift_pair(slot2, slot1, end2, start1)
                    ):
                        violates = True

                    if violates:
                        conflicts.append(HardConflict(
                            constraint_id=f"min_rest_{person.person_id}_{slot1.slot_id}_{slot2.slot_id}",
                            rule_type="min_rest_violation",
                            description=t(locale, "min_rest_violation",
                                          person=person.person_id,
                                          min_rest=home_leave_cfg.min_rest_hours,
                                          slot1=slot1.task_type_name,
                                          slot2=slot2.task_type_name),
                            affected_slot_ids=[slot1.slot_id, slot2.slot_id],
                            affected_person_ids=[person.person_id],
                        ))

    # ── 1. Global headcount check ─────────────────────────────────────────────
    total_required = sum(s.required_headcount for s in slots)
    if len(people) < total_required:
        conflicts.append(HardConflict(
            constraint_id="headcount_global",
            rule_type="min_headcount",
            description=t(locale, "headcount_global",
                          required=total_required, available=len(people)),
            affected_slot_ids=[s.slot_id for s in slots],
            affected_person_ids=[p.person_id for p in people],
        ))

    # ── 2. Per-slot eligibility check ─────────────────────────────────────────
    BLOCKING_STATES = {"blocked", "at_home", "on_mission"}
    blocked_windows: dict[str, list[tuple[int, int]]] = {}
    for pw in input.presence_windows:
        if pw.state in BLOCKING_STATES:
            blocked_windows.setdefault(pw.person_id, []).append(
                (_to_timestamp(pw.starts_at), _to_timestamp(pw.ends_at))
            )

    avail_map: dict[str, list[tuple[int, int]]] = {}
    for aw in input.availability_windows:
        avail_map.setdefault(aw.person_id, []).append(
            (_to_timestamp(aw.starts_at), _to_timestamp(aw.ends_at))
        )

    restriction_map: dict[str, set[str]] = {}
    for c in input.hard_constraints:
        if c.rule_type == "no_task_type_restriction":
            pid = c.payload.get("person_id") or c.scope_id
            tid = str(c.payload.get("task_type_id", ""))
            if pid and tid:
                restriction_map.setdefault(pid, set()).add(tid)

    for slot in slots:
        slot_start = _to_timestamp(slot.starts_at)
        slot_end = _to_timestamp(slot.ends_at)
        eligible: list[str] = []

        for person in people:
            pid = person.person_id

            blocked = any(
                slot_start < block_end and slot_end > block_start
                for block_start, block_end in blocked_windows.get(pid, [])
            )
            if blocked:
                continue

            if pid in avail_map:
                covered = any(
                    a_start <= slot_start and a_end >= slot_end
                    for a_start, a_end in avail_map[pid]
                )
                if not covered:
                    continue

            if slot.required_qualification_ids:
                if not set(slot.required_qualification_ids).issubset(set(person.qualification_ids)):
                    continue

            if slot.required_role_ids:
                if not set(slot.required_role_ids).intersection(set(person.role_ids)):
                    continue

            if slot.task_type_id in restriction_map.get(pid, set()):
                continue

            eligible.append(pid)

        if len(eligible) < slot.required_headcount:
            reason_parts = []
            if slot.required_qualification_ids:
                reason_parts.append(t(locale, "reason_qualification"))
            if slot.required_role_ids:
                reason_parts.append(t(locale, "reason_role"))
            if blocked_windows or avail_map:
                reason_parts.append(t(locale, "reason_availability"))
            reason_str = ", ".join(reason_parts) if reason_parts else t(locale, "reason_other")

            # Format starts_at as a readable date/time string
            starts_str = str(slot.starts_at)[:16].replace("T", " ")

            conflicts.append(HardConflict(
                constraint_id=f"slot_{slot.slot_id}_eligibility",
                rule_type="slot_eligibility",
                description=t(locale, "slot_eligibility",
                               task=slot.task_type_name,
                               starts_at=starts_str,
                               required=slot.required_headcount,
                               eligible=len(eligible),
                               reasons=reason_str),
                affected_slot_ids=[slot.slot_id],
                affected_person_ids=[p.person_id for p in people if p.person_id not in eligible],
            ))

    return conflicts
