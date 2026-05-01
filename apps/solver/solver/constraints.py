"""
Hard constraint implementations for the CP-SAT model.
Each function receives the model, decision variables, and relevant input data,
and adds the appropriate constraints to the model.
"""
from ortools.sat.python import cp_model
from models.solver_input import SolverInput, TaskSlot, HardConstraint, SoftConstraint
from datetime import datetime, timezone
import copy
import logging

logger = logging.getLogger(__name__)


def expand_role_constraints(
    hard_constraints: list,
    soft_constraints: list,
    emergency_constraints: list,
    people
) -> tuple:
    """
    Expand role-scoped constraints to individual person-scoped constraints.

    For each constraint with scope_type == "role":
      - Find all people whose role_ids include the role's scope_id
      - Create one person-scoped copy per matching person
      - Remove the original role-scoped constraint

    Returns the modified (hard, soft, emergency) tuple.
    """
    # Build role_id → [person_id, ...] map
    role_to_people: dict[str, list[str]] = {}
    for person in people:
        for role_id in person.role_ids:
            role_to_people.setdefault(role_id, []).append(person.person_id)

    def _expand_list(constraints: list) -> list:
        result = []
        for c in constraints:
            if c.scope_type != "role":
                result.append(c)
                continue
            role_id = c.scope_id
            members = role_to_people.get(role_id, [])
            if not members:
                logger.warning(
                    "expand_role_constraints: role %s has no members — constraint %s dropped",
                    role_id, c.constraint_id
                )
                continue
            for person_id in members:
                expanded = copy.copy(c)
                expanded = expanded.model_copy(update={"scope_type": "person", "scope_id": person_id})
                result.append(expanded)
        return result

    return (
        _expand_list(hard_constraints),
        _expand_list(soft_constraints),
        _expand_list(emergency_constraints),
    )


def expand_group_constraints(
    hard_constraints: list,
    soft_constraints: list,
    emergency_constraints: list,
    people
) -> tuple:
    """
    Expand group-scoped constraints to individual person-scoped constraints.

    For each constraint with scope_type == "group":
      - Find all people whose group_ids include the group's scope_id
      - Create one person-scoped copy per matching person
      - Remove the original group-scoped constraint

    Returns the modified (hard, soft, emergency) tuple.
    """
    # Build group_id → [person_id, ...] map
    group_to_people: dict[str, list[str]] = {}
    for person in people:
        for group_id in person.group_ids:
            group_to_people.setdefault(group_id, []).append(person.person_id)

    def _expand_list(constraints: list) -> list:
        result = []
        for c in constraints:
            if c.scope_type != "group":
                result.append(c)
                continue
            group_id = c.scope_id
            members = group_to_people.get(group_id, [])
            if not members:
                logger.warning(
                    "expand_group_constraints: group %s has no members — constraint %s dropped",
                    group_id, c.constraint_id
                )
                continue
            for person_id in members:
                expanded = c.model_copy(update={"scope_type": "person", "scope_id": person_id})
                result.append(expanded)
        return result

    return (
        _expand_list(hard_constraints),
        _expand_list(soft_constraints),
        _expand_list(emergency_constraints),
    )


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
    num_people: int,
    emergency_person_ids: set = None
):
    """
    A person cannot be assigned to two overlapping slots unless
    both task types explicitly allow overlap.
    Emergency-bypassed people skip this constraint.
    """
    emergency_person_ids = emergency_person_ids or set()
    for p_idx in range(num_people):
        for s1_idx, slot1 in enumerate(slots):
            for s2_idx, slot2 in enumerate(slots):
                if s2_idx <= s1_idx:
                    continue
                if not _slots_overlap(slot1, slot2):
                    continue
                if not (slot1.allows_overlap and slot2.allows_overlap):
                    model.add(
                        assign[(s1_idx, p_idx)] + assign[(s2_idx, p_idx)] <= 1
                    )


def add_min_rest_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    num_people: int,
    min_rest_hours: float = 8.0,
    emergency_person_ids: set = None
):
    """
    A person must have at least min_rest_hours between assignments.
    Emergency-bypassed people skip this constraint.
    """
    emergency_person_ids = emergency_person_ids or set()
    min_rest_seconds = int(min_rest_hours * 3600)

    for p_idx in range(num_people):
        for s1_idx, slot1 in enumerate(slots):
            for s2_idx, slot2 in enumerate(slots):
                if s2_idx <= s1_idx:
                    continue

                end1   = _to_timestamp(slot1.ends_at)
                start2 = _to_timestamp(slot2.starts_at)
                end2   = _to_timestamp(slot2.ends_at)
                start1 = _to_timestamp(slot1.starts_at)

                if end1 <= start2 and (start2 - end1) < min_rest_seconds:
                    model.add(assign[(s1_idx, p_idx)] + assign[(s2_idx, p_idx)] <= 1)
                if end2 <= start1 and (start1 - end2) < min_rest_seconds:
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


def add_kitchen_frequency_constraints(
    model: cp_model.CpModel,
    assign: dict,
    slots: list[TaskSlot],
    people,
    num_people: int,
    hard_constraints: list[HardConstraint],
    fairness_counters
):
    """
    Kitchen cannot exceed max assignments per rolling 7-day window.
    rule_type: max_kitchen_per_week
    payload: { "max": 2, "task_type_name": "kitchen" }
    """
    kitchen_rules = [
        c for c in hard_constraints
        if c.rule_type == "max_kitchen_per_week"
    ]

    for rule in kitchen_rules:
        max_allowed = int(rule.payload.get("max", 2))
        task_type_name = str(rule.payload.get("task_type_name", "")).lower()

        kitchen_slot_indices = [
            s_idx for s_idx, slot in enumerate(slots)
            if slot.task_type_name.lower() == task_type_name or
               slot.task_type_id == rule.payload.get("task_type_id", "")
        ]

        if not kitchen_slot_indices:
            continue

        # Build a counter map from fairness history
        kitchen_history = {
            f.person_id: f.kitchen_count_7d
            for f in fairness_counters
        }

        for p_idx, person in enumerate(people):
            already_done = kitchen_history.get(person.person_id, 0)
            remaining_allowed = max(0, max_allowed - already_done)

            model.add(
                sum(assign[(s_idx, p_idx)] for s_idx in kitchen_slot_indices)
                <= remaining_allowed
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

        for s_idx, slot in enumerate(slots):
            slot_start = _to_timestamp(slot.starts_at)
            slot_end   = _to_timestamp(slot.ends_at)

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

def _slots_overlap(slot1: TaskSlot, slot2: TaskSlot) -> bool:
    s1 = _to_timestamp(slot1.starts_at)
    e1 = _to_timestamp(slot1.ends_at)
    s2 = _to_timestamp(slot2.starts_at)
    e2 = _to_timestamp(slot2.ends_at)
    return s1 < e2 and s2 < e1


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
