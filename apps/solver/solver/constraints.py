"""
Hard constraint implementations for the CP-SAT model.
Each function receives the model, decision variables, and relevant input data,
and adds the appropriate constraints to the model.
"""
from ortools.sat.python import cp_model
from models.solver_input import SolverInput, TaskSlot, HardConstraint, SoftConstraint
from datetime import datetime, timezone
import logging

logger = logging.getLogger(__name__)


def add_headcount_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    num_people: int
):
    """
    Cap each slot at AT MOST required_headcount people (no over-staffing).

    We do NOT enforce a hard >= lower bound here. Shortfalls are handled
    entirely by the coverage soft objective in objectives.py (weight=1000),
    which heavily penalises under-staffing without making the model INFEASIBLE.

    A hard >= would cause INFEASIBLE whenever there are not enough eligible
    people for any slot — even if the rest of the schedule is perfectly valid.
    That produces empty drafts instead of partial results, which is worse for
    the admin than seeing a partial schedule with uncovered slots flagged.
    """
    for s_idx, slot in enumerate(slots):
        model.add(
            sum(assign[(s_idx, p_idx)] for p_idx in range(num_people))
            <= slot.required_headcount
        )


def add_no_duplicate_assignment(
    model: cp_model.CpModel,
    assign: dict,
    num_slots: int,
    num_people: int
):
    """A person cannot be assigned twice to the same slot."""
    for s_idx in range(num_slots):
        for p_idx in range(num_people):
            model.add(assign[(s_idx, p_idx)] <= 1)


def add_no_overlap_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    emergency_person_ids: set = None
):
    """
    A person cannot be assigned to two overlapping slots unless
    both task types explicitly allow overlap.
    Emergency-bypassed people skip this constraint.
    """
    emergency_person_ids = emergency_person_ids or set()

    # Pre-compute timestamps to avoid repeated ISO string parsing in O(n²) loop
    slot_times = [(_to_timestamp(s.starts_at), _to_timestamp(s.ends_at)) for s in slots]

    # Pre-compute which slot pairs overlap (O(n²) but only done once)
    overlapping_pairs = []
    for s1_idx in range(len(slots)):
        s1_start, s1_end = slot_times[s1_idx]
        for s2_idx in range(s1_idx + 1, len(slots)):
            s2_start, s2_end = slot_times[s2_idx]
            if s1_start < s2_end and s2_start < s1_end:
                # Check if both allow overlap
                if not (slots[s1_idx].allows_overlap and slots[s2_idx].allows_overlap):
                    overlapping_pairs.append((s1_idx, s2_idx))

    for p_idx in range(num_people):
        if people[p_idx].person_id in emergency_person_ids:
            continue
        for s1_idx, s2_idx in overlapping_pairs:
            model.add(assign[(s1_idx, p_idx)] + assign[(s2_idx, p_idx)] <= 1)


def add_no_consecutive_double_shift_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    emergency_person_ids: set = None
):
    """
    When a task has allows_double_shift=False, the same person cannot be assigned
    to two consecutive (adjacent) slots of that same task type.
    Two slots are considered consecutive if one ends exactly when the other starts
    (or within a 1-minute tolerance to handle rounding).
    Emergency-bypassed people skip this constraint.
    """
    emergency_person_ids = emergency_person_ids or set()

    # Pre-compute timestamps
    slot_times = [(_to_timestamp(s.starts_at), _to_timestamp(s.ends_at)) for s in slots]

    # Find consecutive slot pairs of the same task type where double shift is NOT allowed
    consecutive_pairs = []
    for s1_idx in range(len(slots)):
        if slots[s1_idx].allows_double_shift:
            continue
        s1_end = slot_times[s1_idx][1]
        task_type = slots[s1_idx].task_type_id

        for s2_idx in range(len(slots)):
            if s2_idx == s1_idx:
                continue
            if slots[s2_idx].task_type_id != task_type:
                continue
            if slots[s2_idx].allows_double_shift:
                continue
            s2_start = slot_times[s2_idx][0]
            # Consecutive: s1 ends exactly when s2 starts (within 60s tolerance)
            if 0 <= (s2_start - s1_end) <= 60:
                consecutive_pairs.append((s1_idx, s2_idx))

    if not consecutive_pairs:
        return

    for p_idx in range(num_people):
        if people[p_idx].person_id in emergency_person_ids:
            continue
        for s1_idx, s2_idx in consecutive_pairs:
            model.add(assign[(s1_idx, p_idx)] + assign[(s2_idx, p_idx)] <= 1)


def add_min_rest_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    min_rest_hours: float = 8.0,
    emergency_person_ids: set = None,
    soft_penalties: list = None
):
    """
    A person must have at least min_rest_hours between assignments.
    Emergency-bypassed people skip this constraint.
    For long shifts (>=12h), rest is a soft constraint (penalty) rather than hard,
    so the solver can still assign people to 24h tasks when resources are tight.
    """
    emergency_person_ids = emergency_person_ids or set()
    min_rest_seconds = int(min_rest_hours * 3600)
    long_shift_threshold = 24 * 3600  # 24 hours in seconds

    # Pre-compute timestamps to avoid repeated ISO string parsing in O(n²) loop
    slot_times = [(_to_timestamp(s.starts_at), _to_timestamp(s.ends_at)) for s in slots]

    # Pre-compute which slot pairs violate min rest (O(n²) but only done once)
    # Each entry: (s1_idx, s2_idx, is_long_shift)
    rest_violation_pairs = []
    for s1_idx in range(len(slots)):
        start1, end1 = slot_times[s1_idx]
        slot1_duration = end1 - start1
        for s2_idx in range(s1_idx + 1, len(slots)):
            start2, end2 = slot_times[s2_idx]
            slot2_duration = end2 - start2
            is_long_shift = slot1_duration >= long_shift_threshold or slot2_duration >= long_shift_threshold

            violates_forward = end1 <= start2 and (start2 - end1) < min_rest_seconds
            violates_backward = end2 <= start1 and (start1 - end2) < min_rest_seconds

            if violates_forward or violates_backward:
                rest_violation_pairs.append((s1_idx, s2_idx, is_long_shift, violates_forward, violates_backward))

    for p_idx in range(num_people):
        if people[p_idx].person_id in emergency_person_ids:
            continue
        for s1_idx, s2_idx, is_long_shift, viol_fwd, viol_bwd in rest_violation_pairs:
            if viol_fwd:
                if is_long_shift and soft_penalties is not None:
                    violation = model.new_bool_var(f"rest_soft_{s1_idx}_{s2_idx}_{p_idx}")
                    model.add(assign[(s1_idx, p_idx)] + assign[(s2_idx, p_idx)] <= 1 + violation)
                    soft_penalties.append(violation * 50)
                else:
                    model.add(assign[(s1_idx, p_idx)] + assign[(s2_idx, p_idx)] <= 1)

            if viol_bwd:
                if is_long_shift and soft_penalties is not None:
                    violation = model.new_bool_var(f"rest_soft_{s2_idx}_{s1_idx}_{p_idx}")
                    model.add(assign[(s1_idx, p_idx)] + assign[(s2_idx, p_idx)] <= 1 + violation)
                    soft_penalties.append(violation * 50)
                else:
                    model.add(assign[(s1_idx, p_idx)] + assign[(s2_idx, p_idx)] <= 1)


def add_qualification_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    emergency_person_ids: set = None
):
    """
    A person can only be assigned to a slot if they hold all required qualifications.
    Emergency-bypassed people skip this constraint.
    """
    emergency_person_ids = emergency_person_ids or set()
    for s_idx, slot in enumerate(slots):
        if not slot.required_qualification_ids:
            continue
        required = set(slot.required_qualification_ids)
        for p_idx, person in enumerate(people):
            if person.person_id in emergency_person_ids:
                continue
            person_quals = set(person.qualification_ids)
            if not required.issubset(person_quals):
                model.add(assign[(s_idx, p_idx)] == 0)


def add_role_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    emergency_person_ids: set = None
):
    """
    A person can only be assigned to a slot if they hold at least one required role.
    Emergency-bypassed people skip this constraint.
    """
    emergency_person_ids = emergency_person_ids or set()
    for s_idx, slot in enumerate(slots):
        if not slot.required_role_ids:
            continue
        required = set(slot.required_role_ids)
        for p_idx, person in enumerate(people):
            if person.person_id in emergency_person_ids:
                continue
            person_roles = set(person.role_ids)
            if not required.intersection(person_roles):
                model.add(assign[(s_idx, p_idx)] == 0)


def add_restriction_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    hard_constraints: list[HardConstraint]
):
    """
    Apply individual no-assignment restrictions from hard constraints.
    rule_type: no_task_type_restriction
    payload: { "person_id": "...", "task_type_id": "..." }
    """
    restrictions = [
        c for c in hard_constraints
        if c.rule_type == "no_task_type_restriction"
    ]

    for constraint in restrictions:
        person_id = constraint.payload.get("person_id") or constraint.scope_id
        task_type_id = str(constraint.payload.get("task_type_id", ""))

        if not person_id or not task_type_id:
            continue

        for p_idx, person in enumerate(people):
            if person.person_id != person_id:
                continue
            for s_idx, slot in enumerate(slots):
                if slot.task_type_id == task_type_id:
                    model.add(assign[(s_idx, p_idx)] == 0)


def add_max_task_type_per_period_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    hard_constraints: list[HardConstraint],
    fairness_counters
):
    """
    Generic: no person exceeds max assignments for a named task type within period_days.
    rule_type: max_task_type_per_period
    payload: { "task_type_name": "Kitchen", "max": 2, "period_days": 7 }
    """
    rules = [
        c for c in hard_constraints
        if c.rule_type == "max_task_type_per_period"
    ]

    for rule in rules:
        task_type_name = str(rule.payload.get("task_type_name", "")).lower()
        max_allowed = int(rule.payload.get("max", 2))
        # period_days used for historical lookup; within a single solver horizon all matching slots count

        matching_slots = [
            s_idx for s_idx, slot in enumerate(slots)
            if slot.task_type_name.lower() == task_type_name
        ]

        if not matching_slots:
            continue

        # Historical count from fairness counters
        history = {
            f.person_id: f.task_type_counts_7d.get(task_type_name, 0)
            for f in fairness_counters
        }

        for p_idx, person in enumerate(people):
            already_done = history.get(person.person_id, 0)
            remaining = max(0, max_allowed - already_done)

            model.add(
                sum(assign[(s_idx, p_idx)] for s_idx in matching_slots)
                <= remaining
            )


def add_availability_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    availability_windows,
    presence_windows,
    emergency_person_ids: set = None
):
    """
    A person cannot be assigned to a slot if:
      - They have a presence window with state 'blocked' or 'at_home' overlapping the slot.
      - The slot falls outside all their explicit availability windows (when any exist).
    Emergency-bypassed people skip this constraint entirely.

    Presence states:
      blocked    — person is completely unavailable (sick, leave, etc.) — hard block
      at_home    — person is at home / off-base — hard block
      on_mission — person is on another mission — treated as blocked
      free_in_base — person is available (no constraint added)
    """
    emergency_person_ids = emergency_person_ids or set()

    # Pre-compute slot timestamps once
    slot_times = [(_to_timestamp(s.starts_at), _to_timestamp(s.ends_at)) for s in slots]

    # Collect all blocking presence windows per person
    # States that block assignment: blocked, at_home, on_mission
    BLOCKING_STATES = {"blocked", "at_home", "on_mission"}
    blocked_windows: dict[str, list[tuple[int, int]]] = {}
    for pw in presence_windows:
        if pw.state in BLOCKING_STATES:
            blocked_windows.setdefault(pw.person_id, []).append(
                (_to_timestamp(pw.starts_at), _to_timestamp(pw.ends_at))
            )

    avail_map: dict[str, list[tuple[int, int]]] = {}
    for aw in availability_windows:
        avail_map.setdefault(aw.person_id, []).append(
            (_to_timestamp(aw.starts_at), _to_timestamp(aw.ends_at))
        )

    for p_idx, person in enumerate(people):
        pid = person.person_id
        if pid in emergency_person_ids:
            continue  # bypass all availability/presence checks

        for s_idx in range(len(slots)):
            slot_start, slot_end = slot_times[s_idx]

            # Block if any blocking presence window overlaps this slot
            if pid in blocked_windows:
                for block_start, block_end in blocked_windows[pid]:
                    if slot_start < block_end and slot_end > block_start:
                        model.add(assign[(s_idx, p_idx)] == 0)
                        break

            # Block if person has explicit availability windows but none cover this slot
            if pid in avail_map:
                covered = any(
                    a_start <= slot_start and a_end >= slot_end
                    for a_start, a_end in avail_map[pid]
                )
                if not covered:
                    model.add(assign[(s_idx, p_idx)] == 0)


# ── Helpers ───────────────────────────────────────────────────────────────────


def _to_timestamp(dt) -> int:
    """Convert datetime or ISO string to Unix timestamp (seconds)."""
    if isinstance(dt, (int, float)):
        return int(dt)
    if isinstance(dt, str):
        dt = datetime.fromisoformat(dt.replace("Z", "+00:00"))
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return int(dt.timestamp())


# ── Constraint expansion ──────────────────────────────────────────────────────

def expand_role_constraints(
    hard_constraints: list,
    soft_constraints: list,
    emergency_constraints: list,
    people: list,
) -> tuple[list, list, list]:
    """
    Expand role-scoped constraints into individual person-scoped constraints.

    For each constraint with scope_type == "role", find all people whose
    role_ids include that role's scope_id, and create one person-scoped copy
    per matching person. The original role-scoped constraint is removed.

    Returns the modified (hard, soft, emergency) tuple.
    """
    # Build role_id → [person_id, ...] map from people's role_ids
    role_to_people: dict[str, list[str]] = {}
    for person in people:
        for role_id in person.role_ids:
            role_to_people.setdefault(role_id, []).append(person.person_id)

    def _expand(constraints: list) -> list:
        result = []
        for c in constraints:
            if c.scope_type != "role":
                result.append(c)
                continue
            role_id = c.scope_id
            if not role_id:
                result.append(c)
                continue
            members = role_to_people.get(role_id, [])
            if not members:
                import logging
                logging.getLogger(__name__).warning(
                    "expand_role_constraints: role %s has no members — constraint %s skipped.",
                    role_id, c.constraint_id)
                continue
            for person_id in members:
                # Create a copy with scope_type=person and scope_id=person_id
                expanded = c.model_copy(update={"scope_type": "person", "scope_id": person_id})
                result.append(expanded)
        return result

    return _expand(hard_constraints), _expand(soft_constraints), _expand(emergency_constraints)


def expand_group_constraints(
    hard_constraints: list,
    soft_constraints: list,
    emergency_constraints: list,
    people: list,
) -> tuple[list, list, list]:
    """
    Expand group-scoped constraints into individual person-scoped constraints.

    For each constraint with scope_type == "group", find all people whose
    group_ids include that group's scope_id, and create one person-scoped copy
    per matching person. The original group-scoped constraint is removed.

    Returns the modified (hard, soft, emergency) tuple.
    """
    # Build group_id → [person_id, ...] map from people's group_ids
    group_to_people: dict[str, list[str]] = {}
    for person in people:
        for group_id in person.group_ids:
            group_to_people.setdefault(group_id, []).append(person.person_id)

    def _expand(constraints: list) -> list:
        result = []
        for c in constraints:
            if c.scope_type != "group":
                result.append(c)
                continue
            group_id = c.scope_id
            if not group_id:
                result.append(c)
                continue
            members = group_to_people.get(group_id, [])
            if not members:
                import logging
                logging.getLogger(__name__).warning(
                    "expand_group_constraints: group %s has no members — constraint %s skipped.",
                    group_id, c.constraint_id)
                continue
            for person_id in members:
                expanded = c.model_copy(update={"scope_type": "person", "scope_id": person_id})
                result.append(expanded)
        return result

    return _expand(hard_constraints), _expand(soft_constraints), _expand(emergency_constraints)


def add_locked_slot_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    locked_slot_ids: set[str],
    baseline_assignments: list,
):
    """
    For each slot that has a manual override (locked), force the solver to keep
    exactly the same person assignments from the baseline.

    This prevents the solver from reassigning manually overridden slots when
    re-running after a manual override has been applied.
    """
    if not locked_slot_ids:
        return

    # Build baseline map: slot_id → set of person_ids
    baseline_map: dict[str, set[str]] = {}
    for ba in baseline_assignments:
        baseline_map.setdefault(ba.slot_id, set()).add(ba.person_id)

    person_id_to_idx = {person.person_id: idx for idx, person in enumerate(people)}

    for s_idx, slot in enumerate(slots):
        if slot.slot_id not in locked_slot_ids:
            continue

        locked_persons = baseline_map.get(slot.slot_id, set())

        for p_idx, person in enumerate(people):
            pid = person.person_id
            if pid in locked_persons:
                # Force this person to be assigned to this slot
                model.add(assign[(s_idx, p_idx)] == 1)
            else:
                # Force all other people to NOT be assigned to this slot
                model.add(assign[(s_idx, p_idx)] == 0)


def add_composition_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    penalty_vars: list,
    emergency_person_ids: set = None
):
    """
    Enforce per-slot qualification composition requirements.

    For each slot with qualification_requirements:
      - mandatory=True, count=N: at least N people with that qualification must be assigned (soft — penalise shortfall)
      - mandatory=False, count=N: try to assign N people with that qualification (soft bonus)

    Both are implemented as soft constraints (penalise shortfall) so the solver
    never blocks a shift entirely due to missing qualified people.
    The penalty weight for mandatory seats is higher than optional seats.
    """
    MANDATORY_PENALTY = 500   # high but below coverage weight (1000)
    OPTIONAL_PENALTY  = 50    # low — nice to have

    emergency_person_ids = emergency_person_ids or set()

    for s_idx, slot in enumerate(slots):
        if not slot.qualification_requirements:
            continue

        for req in slot.qualification_requirements:
            qual_name = req.qualification_name
            required_count = req.count
            penalty_weight = MANDATORY_PENALTY if req.mandatory else OPTIONAL_PENALTY

            # Find people who hold this qualification (excluding emergency bypasses)
            qualified_p_idxs = [
                p_idx for p_idx, person in enumerate(people)
                if qual_name in person.qualification_ids
                and person.person_id not in emergency_person_ids
            ]

            if not qualified_p_idxs:
                # No one has this qualification — add max penalty if mandatory
                if req.mandatory:
                    shortfall_var = model.new_int_var(0, required_count, f"comp_shortfall_{s_idx}_{qual_name}")
                    model.add(shortfall_var == required_count)
                    penalty_vars.append((shortfall_var, penalty_weight))
                continue

            # Count how many qualified people are assigned to this slot
            assigned_qualified = sum(assign[(s_idx, p_idx)] for p_idx in qualified_p_idxs)

            # Shortfall = max(0, required_count - assigned_qualified)
            shortfall_var = model.new_int_var(0, required_count, f"comp_shortfall_{s_idx}_{qual_name}")
            model.add(shortfall_var >= required_count - assigned_qualified)
            model.add(shortfall_var >= 0)

            penalty_vars.append((shortfall_var, penalty_weight))
