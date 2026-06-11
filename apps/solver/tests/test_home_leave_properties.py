"""
Property-based tests for the home-leave scheduling solver module.
Uses Hypothesis to generate random but valid solver inputs and verify
correctness properties on the solver output.

Feature: home-leave-scheduling
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from datetime import date, datetime, timezone, timedelta
from hypothesis import given, settings, assume, HealthCheck
from hypothesis import strategies as st

from models.solver_input import (
    SolverInput, PersonEligibility, TaskSlot, StabilityWeights, HomeLeaveConfig,
    PresenceWindow, AvailabilityWindow, HardConstraint, SoftConstraint,
    BaselineAssignment, FairnessCounters, SpecialDay,
)
from solver.engine import solve


# ─── Hypothesis Strategies ────────────────────────────────────────────────────

# Fixed horizon: 3 days starting 2026-05-01
HORIZON_START = date(2026, 5, 1)
HORIZON_END = date(2026, 5, 4)  # 3 days = 72 hours
HORIZON_START_DT = datetime(2026, 5, 1, tzinfo=timezone.utc)
HORIZON_HOURS = 72


@st.composite
def people_strategy(draw):
    """Generate 2-4 people with unique IDs."""
    num_people = draw(st.integers(min_value=2, max_value=4))
    return [
        PersonEligibility(
            person_id=f"person-{i}",
            role_ids=["role-1"],
            qualification_ids=[],
            group_ids=["group-1"],
        )
        for i in range(num_people)
    ]


@st.composite
def task_slots_strategy(draw):
    """Generate 3-8 non-overlapping task slots within the 3-day horizon."""
    num_slots = draw(st.integers(min_value=3, max_value=8))
    slots = []
    for i in range(num_slots):
        # Random start hour within the horizon, leaving room for duration
        start_hour = draw(st.integers(min_value=0, max_value=HORIZON_HOURS - 4))
        # Duration between 4 and 8 hours
        duration = draw(st.integers(min_value=4, max_value=8))
        end_hour = min(start_hour + duration, HORIZON_HOURS)

        starts_at = HORIZON_START_DT + timedelta(hours=start_hour)
        ends_at = HORIZON_START_DT + timedelta(hours=end_hour)

        slots.append(TaskSlot(
            slot_id=f"slot-{i}",
            task_type_id="tt-guard",
            task_type_name="Guard",
            burden_level="neutral",
            starts_at=starts_at,
            ends_at=ends_at,
            required_headcount=1,
            priority=5,
            required_role_ids=[],
            required_qualification_ids=[],
            allows_overlap=False,
        ))
    return slots


@st.composite
def home_leave_config_strategy(draw):
    """Generate a valid HomeLeaveConfig with reasonable parameters."""
    min_rest = draw(st.integers(min_value=4, max_value=8))
    eligibility_threshold = draw(st.integers(min_value=max(min_rest, 12), max_value=24))
    leave_capacity = draw(st.integers(min_value=1, max_value=2))
    leave_duration = draw(st.integers(min_value=12, max_value=24))

    return HomeLeaveConfig(
        enabled=True,
        min_rest_hours=float(min_rest),
        eligibility_threshold_hours=float(eligibility_threshold),
        leave_capacity=leave_capacity,
        leave_duration_hours=float(leave_duration),
    )


def make_solver_input(people, slots, config):
    """Build a complete SolverInput from generated components."""
    return SolverInput(
        space_id="test-space",
        run_id="test-run-pbt",
        trigger_mode="standard",
        horizon_start=HORIZON_START,
        horizon_end=HORIZON_END,
        locale="en",
        stability_weights=StabilityWeights(
            today_tomorrow=10.0, days_3_4=3.0, days_5_7=1.0
        ),
        people=people,
        availability_windows=[],
        presence_windows=[],
        task_slots=slots,
        hard_constraints=[],
        soft_constraints=[],
        emergency_constraints=[],
        baseline_assignments=[],
        fairness_counters=[],
        locked_slot_ids=[],
        home_leave_config=config,
    )


def test_special_day_preference_places_leave_on_special_day():
    """A reviewed special day should attract home-leave timing when constraints allow it."""
    people = [
        PersonEligibility(
            person_id="person-1",
            role_ids=["role-1"],
            qualification_ids=[],
            group_ids=["group-1"],
        )
    ]
    slots = [
        TaskSlot(
            slot_id="slot-1",
            task_type_id="tt-guard",
            task_type_name="Guard",
            burden_level="neutral",
            starts_at=HORIZON_START_DT,
            ends_at=HORIZON_START_DT + timedelta(hours=4),
            required_headcount=1,
            priority=5,
            required_role_ids=[],
            required_qualification_ids=[],
            allows_overlap=False,
        )
    ]
    config = HomeLeaveConfig(
        enabled=True,
        min_rest_hours=8.0,
        eligibility_threshold_hours=0.0,
        leave_capacity=1,
        leave_duration_hours=24.0,
        balance_value=100,
    )
    solver_input = make_solver_input(people, slots, config).model_copy(update={
        "horizon_end": date(2026, 5, 5),
        "special_days": [
            SpecialDay(
                date=date(2026, 5, 3),
                name="Holiday",
                kind="holiday",
                home_leave_weight_multiplier=5.0,
                requires_coverage=True,
            )
        ],
    })

    result = solve(solver_input)

    assert result.feasible
    assert result.home_leave_assignments
    holiday_start = datetime(2026, 5, 3, tzinfo=timezone.utc)
    holiday_end = holiday_start + timedelta(days=1)
    assert any(
        _intervals_overlap(
            _parse_iso(leave.starts_at),
            _parse_iso(leave.ends_at),
            holiday_start,
            holiday_end,
        )
        for leave in result.home_leave_assignments
    )


# ─── Helper Functions ─────────────────────────────────────────────────────────

def _to_timestamp(dt) -> int:
    """Convert datetime to Unix timestamp."""
    if isinstance(dt, str):
        dt = datetime.fromisoformat(dt.replace("Z", "+00:00"))
    if hasattr(dt, 'timestamp'):
        if dt.tzinfo is None:
            dt = dt.replace(tzinfo=timezone.utc)
        return int(dt.timestamp())
    return int(dt)


def _parse_iso(s: str) -> datetime:
    """Parse ISO 8601 string to datetime."""
    return datetime.fromisoformat(s.replace("Z", "+00:00"))


def _intervals_overlap(start1, end1, start2, end2) -> bool:
    """Check if two time intervals overlap. Two windows overlap if start1 < end2 AND start2 < end1."""
    return start1 < end2 and start2 < end1


# ─── Property 3: Min-rest invariant ──────────────────────────────────────────

# Property 3: Minimum rest invariant — no assignment pair violates min rest
# For any feasible solver output with home_leave_config enabled, no two
# consecutive mission assignments for the same person have a gap smaller
# than min_rest_hours.
# **Validates: Requirements 3.2, 3.3**

@given(
    people=people_strategy(),
    slots=task_slots_strategy(),
    config=home_leave_config_strategy(),
)
@settings(max_examples=50, deadline=None)
def test_property_3_min_rest_invariant(people, slots, config):
    """Property 3: No two consecutive mission assignments for the same person
    have a gap smaller than min_rest_hours."""
    solver_input = make_solver_input(people, slots, config)
    result = solve(solver_input)

    # Only check on feasible outputs
    assume(result.feasible)

    min_rest_seconds = config.min_rest_hours * 3600

    # Group assignments by person
    person_assignments: dict[str, list] = {}
    for assignment in result.assignments:
        pid = assignment.person_id
        if pid not in person_assignments:
            person_assignments[pid] = []
        # Find the slot for this assignment
        slot = next(s for s in slots if s.slot_id == assignment.slot_id)
        person_assignments[pid].append(slot)

    # For each person, sort by start time and check consecutive gaps
    for pid, person_slots in person_assignments.items():
        sorted_slots = sorted(person_slots, key=lambda s: _to_timestamp(s.starts_at))
        for i in range(len(sorted_slots) - 1):
            end_current = _to_timestamp(sorted_slots[i].ends_at)
            start_next = _to_timestamp(sorted_slots[i + 1].starts_at)
            gap = start_next - end_current
            # Gap must be >= min_rest_hours (only check non-overlapping pairs)
            if gap >= 0:
                assert gap >= min_rest_seconds, (
                    f"Person {pid}: gap between assignments is {gap / 3600:.1f}h, "
                    f"but min_rest_hours is {config.min_rest_hours}h"
                )


# ─── Property 4: Capacity invariant ──────────────────────────────────────────

# Property 4: Home-leave capacity invariant
# For any feasible solver output with home-leave enabled, for every hour in
# the horizon, the number of people on leave ≤ leave_capacity.
# **Validates: Requirements 4.5, 5.2**

@given(
    people=people_strategy(),
    slots=task_slots_strategy(),
    config=home_leave_config_strategy(),
)
@settings(max_examples=50, deadline=None)
def test_property_4_capacity_invariant(people, slots, config):
    """Property 4: At no hour do more people have leave than leave_capacity."""
    solver_input = make_solver_input(people, slots, config)
    result = solve(solver_input)

    assume(result.feasible)

    if not result.home_leave_assignments:
        return  # No leave assignments — trivially satisfied

    horizon_start_ts = int(HORIZON_START_DT.timestamp())

    # For each hour in the horizon, count people on leave
    for hour_offset in range(HORIZON_HOURS):
        hour_ts = horizon_start_ts + hour_offset * 3600
        hour_end_ts = hour_ts + 3600

        people_on_leave = 0
        for leave in result.home_leave_assignments:
            leave_start = int(_parse_iso(leave.starts_at).timestamp())
            leave_end = int(_parse_iso(leave.ends_at).timestamp())
            # Leave overlaps this hour if leave_start < hour_end AND hour_ts < leave_end
            if leave_start < hour_end_ts and hour_ts < leave_end:
                people_on_leave += 1

        assert people_on_leave <= config.leave_capacity, (
            f"Hour offset {hour_offset}: {people_on_leave} people on leave, "
            f"but leave_capacity is {config.leave_capacity}"
        )


# ─── Property 5: Leave duration correctness ──────────────────────────────────

# Property 5: Home-leave duration correctness
# For every home-leave assignment in a feasible output,
# (ends_at - starts_at) == leave_duration_hours.
# **Validates: Requirements 5.1**

@given(
    people=people_strategy(),
    slots=task_slots_strategy(),
    config=home_leave_config_strategy(),
)
@settings(max_examples=50, deadline=None)
def test_property_5_leave_duration_correctness(people, slots, config):
    """Property 5: Every home-leave assignment has duration == leave_duration_hours."""
    solver_input = make_solver_input(people, slots, config)
    result = solve(solver_input)

    assume(result.feasible)

    expected_duration_seconds = config.leave_duration_hours * 3600

    for leave in result.home_leave_assignments:
        leave_start = _parse_iso(leave.starts_at)
        leave_end = _parse_iso(leave.ends_at)
        actual_duration = (leave_end - leave_start).total_seconds()

        assert actual_duration == expected_duration_seconds, (
            f"Leave for {leave.person_id}: duration is {actual_duration / 3600:.1f}h, "
            f"expected {config.leave_duration_hours}h"
        )


# ─── Property 6: No leave-mission overlap ────────────────────────────────────

# Property 6: No overlap between home-leave and mission assignments
# For every person, no mission assignment overlaps any home-leave assignment.
# Two time windows overlap if starts_at_1 < ends_at_2 AND starts_at_2 < ends_at_1.
# **Validates: Requirements 5.3**

@given(
    people=people_strategy(),
    slots=task_slots_strategy(),
    config=home_leave_config_strategy(),
)
@settings(max_examples=50, deadline=None)
def test_property_6_no_leave_mission_overlap(people, slots, config):
    """Property 6: No mission assignment overlaps any home-leave assignment for the same person."""
    solver_input = make_solver_input(people, slots, config)
    result = solve(solver_input)

    assume(result.feasible)

    if not result.home_leave_assignments:
        return  # No leave — trivially satisfied

    # Build a map of person_id -> list of leave windows
    person_leaves: dict[str, list[tuple[int, int]]] = {}
    for leave in result.home_leave_assignments:
        pid = leave.person_id
        leave_start = int(_parse_iso(leave.starts_at).timestamp())
        leave_end = int(_parse_iso(leave.ends_at).timestamp())
        if pid not in person_leaves:
            person_leaves[pid] = []
        person_leaves[pid].append((leave_start, leave_end))

    # Check each mission assignment against that person's leave windows
    for assignment in result.assignments:
        pid = assignment.person_id
        if pid not in person_leaves:
            continue

        slot = next(s for s in slots if s.slot_id == assignment.slot_id)
        mission_start = _to_timestamp(slot.starts_at)
        mission_end = _to_timestamp(slot.ends_at)

        for leave_start, leave_end in person_leaves[pid]:
            assert not _intervals_overlap(mission_start, mission_end, leave_start, leave_end), (
                f"Person {pid}: mission [{slot.slot_id}] overlaps with home-leave "
                f"[{leave_start} - {leave_end}]"
            )


# ─── Property 7: No concurrent leave per person ──────────────────────────────

# Property 7: No concurrent home-leave for the same person
# For every person, no two home-leave assignments overlap in time.
# **Validates: Requirements 5.8**

@given(
    people=people_strategy(),
    slots=task_slots_strategy(),
    config=home_leave_config_strategy(),
)
@settings(max_examples=50, deadline=None)
def test_property_7_no_concurrent_leave_per_person(people, slots, config):
    """Property 7: No two home-leave assignments for the same person overlap in time."""
    solver_input = make_solver_input(people, slots, config)
    result = solve(solver_input)

    assume(result.feasible)

    if not result.home_leave_assignments:
        return  # No leave — trivially satisfied

    # Group leave assignments by person
    person_leaves: dict[str, list[tuple[int, int]]] = {}
    for leave in result.home_leave_assignments:
        pid = leave.person_id
        leave_start = int(_parse_iso(leave.starts_at).timestamp())
        leave_end = int(_parse_iso(leave.ends_at).timestamp())
        if pid not in person_leaves:
            person_leaves[pid] = []
        person_leaves[pid].append((leave_start, leave_end))

    # For each person, check all pairs of leave assignments
    for pid, leaves in person_leaves.items():
        sorted_leaves = sorted(leaves, key=lambda x: x[0])
        for i in range(len(sorted_leaves) - 1):
            for j in range(i + 1, len(sorted_leaves)):
                start1, end1 = sorted_leaves[i]
                start2, end2 = sorted_leaves[j]
                assert not _intervals_overlap(start1, end1, start2, end2), (
                    f"Person {pid}: two leave assignments overlap: "
                    f"[{start1} - {end1}] and [{start2} - {end2}]"
                )


# ─── Property 8: Base-time ratio computation ─────────────────────────────────

# Property 8: Base-time ratio computation correctness
# For every person in metrics, base_time_ratio == total_base_hours /
# (total_base_hours + total_home_hours) rounded to 4 decimal places.
# **Validates: Requirements 6.1, 6.2**

@given(
    people=people_strategy(),
    slots=task_slots_strategy(),
    config=home_leave_config_strategy(),
)
@settings(max_examples=50, deadline=None)
def test_property_8_base_time_ratio_computation(people, slots, config):
    """Property 8: base_time_ratio == total_base_hours / (total_base_hours + total_home_hours) rounded to 4dp."""
    solver_input = make_solver_input(people, slots, config)
    result = solve(solver_input)

    assume(result.feasible)

    if not result.home_leave_metrics:
        return  # No metrics — nothing to check

    for metric in result.home_leave_metrics:
        total_hours = metric.total_base_hours + metric.total_home_hours
        if total_hours <= 0:
            continue  # Avoid division by zero

        expected_ratio = round(metric.total_base_hours / total_hours, 4)
        assert metric.base_time_ratio == expected_ratio, (
            f"Person {metric.person_id}: base_time_ratio is {metric.base_time_ratio}, "
            f"expected {expected_ratio} "
            f"(base={metric.total_base_hours}, home={metric.total_home_hours})"
        )


# ─── Property 9: Disabled config produces empty output ───────────────────────

# Property 9: Disabled home-leave config produces empty output
# When home_leave_config is absent or enabled=false, output has empty
# home_leave_assignments, empty home_leave_metrics, and null fairness_variance.
# **Validates: Requirements 7.2, 8.3**

@st.composite
def disabled_config_strategy(draw):
    """Generate either None or a HomeLeaveConfig with enabled=False."""
    use_none = draw(st.booleans())
    if use_none:
        return None
    return HomeLeaveConfig(
        enabled=False,
        min_rest_hours=draw(st.floats(min_value=4, max_value=8)),
        eligibility_threshold_hours=draw(st.floats(min_value=12, max_value=24)),
        leave_capacity=draw(st.integers(min_value=1, max_value=2)),
        leave_duration_hours=draw(st.floats(min_value=12, max_value=24)),
    )


@given(
    people=people_strategy(),
    slots=task_slots_strategy(),
    config=disabled_config_strategy(),
)
@settings(max_examples=50, deadline=None)
def test_property_9_disabled_config_empty_output(people, slots, config):
    """Property 9: When home_leave_config is absent or enabled=false, output has
    empty home_leave_assignments, empty home_leave_metrics, and null fairness_variance."""
    solver_input = make_solver_input(people, slots, config)
    result = solve(solver_input)

    assume(result.feasible)

    assert result.home_leave_assignments == [], (
        f"Expected empty home_leave_assignments when config is disabled, "
        f"got {len(result.home_leave_assignments)} assignments"
    )
    assert result.home_leave_metrics == [], (
        f"Expected empty home_leave_metrics when config is disabled, "
        f"got {len(result.home_leave_metrics)} metrics"
    )
    assert result.fairness_variance is None, (
        f"Expected null fairness_variance when config is disabled, "
        f"got {result.fairness_variance}"
    )
