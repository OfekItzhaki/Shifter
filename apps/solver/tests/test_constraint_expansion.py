"""
Tests for expand_role_constraints and expand_group_constraints.
Validates: Tasks 23.1, 23.2
Feature: schedule-table-autoschedule-role-constraints
"""
import sys, os, logging
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from models.solver_input import HardConstraint, SoftConstraint, PersonEligibility
from solver.constraints import expand_role_constraints, expand_group_constraints


def make_hard(constraint_id, scope_type, scope_id, rule_type="min_rest_hours"):
    return HardConstraint(
        constraint_id=constraint_id,
        rule_type=rule_type,
        scope_type=scope_type,
        scope_id=scope_id,
        payload={"hours": 8}
    )


def make_soft(constraint_id, scope_type, scope_id):
    return SoftConstraint(
        constraint_id=constraint_id,
        rule_type="no_consecutive_burden",
        scope_type=scope_type,
        scope_id=scope_id,
        weight=1.0,
        payload={"burden_level": "hated"}
    )


def make_person(pid, role_ids=None, group_ids=None):
    return PersonEligibility(
        person_id=pid,
        role_ids=role_ids or [],
        qualification_ids=[],
        group_ids=group_ids or []
    )


# ── expand_role_constraints ───────────────────────────────────────────────────

class TestExpandRoleConstraints:
    def test_role_with_zero_members_logs_warning_and_removes_original(self, caplog):
        """Task 23.1: role with 0 members → warning logged, 0 expanded, original removed"""
        hard = [make_hard("c1", "role", "role-empty")]
        people = [make_person("p1", role_ids=["role-other"])]

        with caplog.at_level(logging.WARNING):
            new_hard, new_soft, new_emerg = expand_role_constraints(hard, [], [], people)

        assert len(new_hard) == 0, "original role constraint should be removed"
        assert any("role-empty" in r.message for r in caplog.records), "warning should mention the role"

    def test_role_with_one_member_expands_to_one_person_constraint(self):
        """Task 23.1: role with 1 member → 1 person-scoped constraint"""
        hard = [make_hard("c1", "role", "role-a")]
        people = [make_person("p1", role_ids=["role-a"])]

        new_hard, _, _ = expand_role_constraints(hard, [], [], people)

        assert len(new_hard) == 1
        assert new_hard[0].scope_type == "person"
        assert new_hard[0].scope_id == "p1"

    def test_role_with_n_members_expands_to_n_person_constraints(self):
        """Task 23.1: role with N members → N person-scoped constraints"""
        hard = [make_hard("c1", "role", "role-b")]
        people = [
            make_person("p1", role_ids=["role-b"]),
            make_person("p2", role_ids=["role-b"]),
            make_person("p3", role_ids=["role-b"]),
        ]

        new_hard, _, _ = expand_role_constraints(hard, [], [], people)

        assert len(new_hard) == 3
        person_ids = {c.scope_id for c in new_hard}
        assert person_ids == {"p1", "p2", "p3"}

    def test_expansion_applies_to_hard_soft_and_emergency(self):
        """Task 23.1: expansion applies to hard, soft, and emergency lists"""
        hard = [make_hard("c1", "role", "role-c")]
        soft = [make_soft("c2", "role", "role-c")]
        emerg = [make_hard("c3", "role", "role-c", rule_type="emergency_person_bypass")]
        people = [make_person("p1", role_ids=["role-c"]), make_person("p2", role_ids=["role-c"])]

        new_hard, new_soft, new_emerg = expand_role_constraints(hard, soft, emerg, people)

        assert len(new_hard) == 2
        assert len(new_soft) == 2
        assert len(new_emerg) == 2

    def test_non_role_constraints_are_preserved_unchanged(self):
        """Role expansion should not touch group or person constraints"""
        hard = [
            make_hard("c1", "role", "role-d"),
            make_hard("c2", "group", "group-x"),
            make_hard("c3", "person", "person-y"),
        ]
        people = [make_person("p1", role_ids=["role-d"])]

        new_hard, _, _ = expand_role_constraints(hard, [], [], people)

        # role-d expands to 1 person constraint; group and person constraints stay
        assert len(new_hard) == 3  # 1 expanded + 1 group + 1 person
        scope_types = {c.scope_type for c in new_hard}
        assert "group" in scope_types
        assert "person" in scope_types


# ── expand_group_constraints ──────────────────────────────────────────────────

class TestExpandGroupConstraints:
    def test_group_with_zero_members_logs_warning_and_removes_original(self, caplog):
        """Task 23.2: group with 0 members → warning logged, 0 expanded, original removed"""
        hard = [make_hard("c1", "group", "group-empty")]
        people = [make_person("p1", group_ids=["group-other"])]

        with caplog.at_level(logging.WARNING):
            new_hard, _, _ = expand_group_constraints(hard, [], [], people)

        assert len(new_hard) == 0
        assert any("group-empty" in r.message for r in caplog.records)

    def test_group_with_one_member_expands_to_one_person_constraint(self):
        """Task 23.2: group with 1 member → 1 person-scoped constraint"""
        hard = [make_hard("c1", "group", "group-a")]
        people = [make_person("p1", group_ids=["group-a"])]

        new_hard, _, _ = expand_group_constraints(hard, [], [], people)

        assert len(new_hard) == 1
        assert new_hard[0].scope_type == "person"
        assert new_hard[0].scope_id == "p1"

    def test_group_with_n_members_expands_to_n_person_constraints(self):
        """Task 23.2: group with N members → N person-scoped constraints"""
        hard = [make_hard("c1", "group", "group-b")]
        people = [
            make_person("p1", group_ids=["group-b"]),
            make_person("p2", group_ids=["group-b"]),
            make_person("p3", group_ids=["group-b"]),
            make_person("p4", group_ids=["group-b"]),
        ]

        new_hard, _, _ = expand_group_constraints(hard, [], [], people)

        assert len(new_hard) == 4
        person_ids = {c.scope_id for c in new_hard}
        assert person_ids == {"p1", "p2", "p3", "p4"}

    def test_expansion_applies_to_all_three_severity_lists(self):
        """Task 23.2: expansion applies to hard, soft, and emergency lists"""
        hard = [make_hard("c1", "group", "group-c")]
        soft = [make_soft("c2", "group", "group-c")]
        emerg = [make_hard("c3", "group", "group-c")]
        people = [make_person("p1", group_ids=["group-c"])]

        new_hard, new_soft, new_emerg = expand_group_constraints(hard, soft, emerg, people)

        assert len(new_hard) == 1
        assert len(new_soft) == 1
        assert len(new_emerg) == 1
        assert all(c.scope_type == "person" for c in new_hard + new_soft + new_emerg)

    def test_person_in_multiple_groups_gets_constraint_from_each(self):
        """A person in 2 groups gets expanded constraints from both"""
        hard = [
            make_hard("c1", "group", "group-x"),
            make_hard("c2", "group", "group-y"),
        ]
        people = [make_person("p1", group_ids=["group-x", "group-y"])]

        new_hard, _, _ = expand_group_constraints(hard, [], [], people)

        assert len(new_hard) == 2
        assert all(c.scope_id == "p1" for c in new_hard)
