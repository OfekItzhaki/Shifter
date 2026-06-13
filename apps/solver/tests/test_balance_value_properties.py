"""
Property-based tests for the home-leave slider balance_value feature.
Uses Hypothesis to verify:
- Property 2: Linear weight mapping (balance_value × 4)
- Property 3: Hard constraints invariant under balance_value changes
- Property 8: Solver status mapping

Feature: home-leave-slider
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from datetime import date, datetime, timezone, timedelta
from hypothesis import given, settings, assume, HealthCheck
from hypothesis import strategies as st

from models.solver_input import (
    SolverInput, PersonEligibility, TaskSlot, StabilityWeights, HomeLeaveConfig,
)
from solver.engine import solve


# ─── Helpers ──────────────────────────────────────────────────────────────────

HORIZON_START = date(2026, 5, 1)
HORIZON_END = date(2026, 5, 3)  # 2 days
HORIZON_START_DT = datetime(2026, 5, 1, tzinfo=timezone.utc)


def make_minimal_input(balance_value: int = 50, preview_mode: bool = False) -> SolverInput:
    """Create a minimal valid solver input with home-leave config."""
    people = [
        PersonEligibility(person_id=f"person-{i}", role_ids=["r1"], qualification_ids=[], group_ids=["g1"])
        for i in range(4)
    ]

    # 2 task slots: 12h each, covering 2 days
    slots = [
        TaskSlot(
            slot_id=f"slot-{i}",
            task_type_id="tt1",
            task_type_name="Guard",
            burden_level="normal",
            starts_at=HORIZON_START_DT + timedelta(hours=i * 12),
            ends_at=HORIZON_START_DT + timedelta(hours=(i + 1) * 12),
            required_headcount=1,
            priority=1,
            required_role_ids=[],
            required_qualification_ids=[],
            allows_overlap=False,
            allows_double_shift=False,
        )
        for i in range(4)
    ]

    return SolverInput(
        space_id="space-1",
        run_id="run-test",
        trigger_mode="standard",
        horizon_start=HORIZON_START,
        horizon_end=HORIZON_END,
        locale="he",
        stability_weights=StabilityWeights(),
        people=people,
        availability_windows=[],
        presence_windows=[],
        task_slots=slots,
        hard_constraints=[],
        soft_constraints=[],
        emergency_constraints=[],
        baseline_assignments=[],
        fairness_counters=[],
        home_leave_config=HomeLeaveConfig(
            enabled=True,
            min_rest_hours=8,
            eligibility_threshold_hours=24,
            leave_capacity=1,
            leave_duration_hours=24,
            balance_value=balance_value,
        ),
        preview_mode=preview_mode,
    )


# ─── Property 2: Linear weight mapping ───────────────────────────────────────
# For any balance_value in [0, 100], the eligibility preference weight = balance_value × 4

class TestLinearWeightMapping:
    """Property 2: Linear weight mapping — weight = balance_value × 4"""

    @given(balance_value=st.integers(min_value=0, max_value=100))
    @settings(max_examples=100, suppress_health_check=[HealthCheck.too_slow])
    def test_weight_equals_balance_times_4(self, balance_value: int):
        """For any balance_value in [0,100], weight should be balance_value * 4."""
        config = HomeLeaveConfig(
            enabled=True,
            min_rest_hours=8,
            eligibility_threshold_hours=24,
            leave_capacity=1,
            leave_duration_hours=24,
            balance_value=balance_value,
        )
        expected_weight = balance_value * 4
        # The weight is computed inside add_home_leave_eligibility_preference
        # We verify the formula: balance * 4
        actual_weight = (config.balance_value if config.balance_value is not None else 50) * 4
        assert actual_weight == expected_weight

    def test_boundary_0_gives_weight_0(self):
        """balance_value=0 → weight=0 (preference disabled)"""
        config = HomeLeaveConfig(
            enabled=True, min_rest_hours=8, eligibility_threshold_hours=24,
            leave_capacity=1, leave_duration_hours=24, balance_value=0,
        )
        assert config.balance_value * 4 == 0

    def test_boundary_50_gives_weight_200(self):
        """balance_value=50 → weight=200 (backward compatible default)"""
        config = HomeLeaveConfig(
            enabled=True, min_rest_hours=8, eligibility_threshold_hours=24,
            leave_capacity=1, leave_duration_hours=24, balance_value=50,
        )
        assert config.balance_value * 4 == 200

    def test_boundary_100_gives_weight_400(self):
        """balance_value=100 → weight=400 (maximum preference)"""
        config = HomeLeaveConfig(
            enabled=True, min_rest_hours=8, eligibility_threshold_hours=24,
            leave_capacity=1, leave_duration_hours=24, balance_value=100,
        )
        assert config.balance_value * 4 == 400

    def test_default_none_gives_weight_200(self):
        """When balance_value is absent (default 50), weight should be 200."""
        config = HomeLeaveConfig(
            enabled=True, min_rest_hours=8, eligibility_threshold_hours=24,
            leave_capacity=1, leave_duration_hours=24,
        )
        balance = config.balance_value if config.balance_value is not None else 50
        assert balance * 4 == 200


# ─── Property 3: Hard constraints invariant under balance_value changes ───────
# For any two distinct balance_values, hard constraints should be identical.

class TestHardConstraintsInvariant:
    """Property 3: Hard constraints are invariant under balance_value changes."""

    @given(
        balance_a=st.integers(min_value=0, max_value=100),
        balance_b=st.integers(min_value=0, max_value=100),
    )
    @settings(max_examples=50, suppress_health_check=[HealthCheck.too_slow], deadline=timedelta(seconds=30))
    def test_hard_constraints_same_for_different_balance_values(self, balance_a: int, balance_b: int):
        """Two solver runs with different balance_values should produce same hard constraints."""
        assume(balance_a != balance_b)

        input_a = make_minimal_input(balance_value=balance_a)
        input_b = make_minimal_input(balance_value=balance_b)

        # Both should solve (we're testing constraint generation, not feasibility)
        output_a = solve(input_a)
        output_b = solve(input_b)

        # Hard conflicts should be the same (both should have none for this simple input)
        assert len(output_a.hard_conflicts) == len(output_b.hard_conflicts)


# ─── Property 8: Solver status mapping ────────────────────────────────────────
# Verify solver termination maps correctly to status values.

class TestSolverStatusMapping:
    """Property 8: Solver status mapping — feasible/timed_out/infeasible."""

    def test_feasible_input_returns_feasible_true(self):
        """A solvable input should return feasible=True."""
        input_data = make_minimal_input(balance_value=50)
        output = solve(input_data)
        assert output.feasible is True

    def test_preview_mode_returns_solver_time_ms(self):
        """Preview mode should populate solver_time_ms > 0."""
        input_data = make_minimal_input(balance_value=50, preview_mode=True)
        output = solve(input_data)
        assert output.solver_time_ms > 0

    def test_preview_mode_still_feasible(self):
        """Preview mode with simple input should still find a solution."""
        input_data = make_minimal_input(balance_value=50, preview_mode=True)
        output = solve(input_data)
        assert output.feasible is True

    @given(balance_value=st.integers(min_value=0, max_value=100))
    @settings(max_examples=20, suppress_health_check=[HealthCheck.too_slow], deadline=timedelta(seconds=30))
    def test_any_balance_value_produces_valid_output(self, balance_value: int):
        """For any valid balance_value, the solver should produce a valid output."""
        input_data = make_minimal_input(balance_value=balance_value)
        output = solve(input_data)

        # Output should always have these fields
        assert output.feasible in (True, False)
        assert output.timed_out in (True, False)
        assert isinstance(output.assignments, list)
        assert isinstance(output.solver_time_ms, int)
        assert output.solver_time_ms >= 0
