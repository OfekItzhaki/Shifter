/**
 * Logic tests for ConstraintsTab scope filtering and role selector.
 * Feature: schedule-table-autoschedule-role-constraints
 * Validates: Tasks 25.1, 25.2
 */

import * as assert from "assert";

// ── Types ─────────────────────────────────────────────────────────────────────

interface ConstraintDto {
  id: string;
  scopeType: string;
  scopeId: string | null;
  severity: string;
  ruleType: string;
  rulePayloadJson: string;
  isActive: boolean;
  effectiveFrom: string | null;
  effectiveUntil: string | null;
}

interface GroupRoleDto {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  permissionLevel: string;
}

// ── Pure logic from ConstraintsTab ────────────────────────────────────────────

function partitionConstraints(constraints: ConstraintDto[], groupId: string) {
  return {
    group: constraints.filter(c => c.scopeType?.toLowerCase() === "group" && c.scopeId === groupId),
    role: constraints.filter(c => c.scopeType?.toLowerCase() === "role"),
    person: constraints.filter(c => c.scopeType?.toLowerCase() === "person"),
  };
}

function getActiveRoles(roles: GroupRoleDto[]): GroupRoleDto[] {
  return roles.filter(r => r.isActive);
}

// ── Test runner ───────────────────────────────────────────────────────────────

let passed = 0;
let failed = 0;

function test(name: string, fn: () => void) {
  try {
    fn();
    console.log(`  ✓ ${name}`);
    passed++;
  } catch (err: unknown) {
    console.error(`  ✗ ${name}`);
    console.error(`    ${(err as Error).message}`);
    failed++;
  }
}

const GROUP_ID = "group-abc";

function makeConstraint(id: string, scopeType: string, scopeId: string | null): ConstraintDto {
  return { id, scopeType, scopeId, severity: "hard", ruleType: "min_rest_hours", rulePayloadJson: "{}", isActive: true, effectiveFrom: null, effectiveUntil: null };
}

// ── Property 6: Constraint scope filtering in UI ──────────────────────────────
// Feature: schedule-table-autoschedule-role-constraints, Property 6: constraint scope filtering in UI

console.log("\nProperty 6: Constraint scope filtering in UI");

test("group constraints appear only in group section", () => {
  const constraints = [makeConstraint("c1", "Group", GROUP_ID)];
  const { group, role, person } = partitionConstraints(constraints, GROUP_ID);
  assert.strictEqual(group.length, 1);
  assert.strictEqual(role.length, 0);
  assert.strictEqual(person.length, 0);
});

test("role constraints appear only in role section", () => {
  const constraints = [makeConstraint("c1", "Role", "role-id-1")];
  const { group, role, person } = partitionConstraints(constraints, GROUP_ID);
  assert.strictEqual(group.length, 0);
  assert.strictEqual(role.length, 1);
  assert.strictEqual(person.length, 0);
});

test("person constraints appear only in person section", () => {
  const constraints = [makeConstraint("c1", "Person", "person-id-1")];
  const { group, role, person } = partitionConstraints(constraints, GROUP_ID);
  assert.strictEqual(group.length, 0);
  assert.strictEqual(role.length, 0);
  assert.strictEqual(person.length, 1);
});

test("no constraint appears in two sections", () => {
  const constraints = [
    makeConstraint("c1", "Group", GROUP_ID),
    makeConstraint("c2", "Role", "role-1"),
    makeConstraint("c3", "Person", "person-1"),
  ];
  const { group, role, person } = partitionConstraints(constraints, GROUP_ID);
  const allIds = [...group, ...role, ...person].map(c => c.id);
  const uniqueIds = new Set(allIds);
  assert.strictEqual(allIds.length, uniqueIds.size, "no constraint should appear in two sections");
});

test("group constraint for different group is excluded from group section", () => {
  const constraints = [makeConstraint("c1", "Group", "other-group")];
  const { group } = partitionConstraints(constraints, GROUP_ID);
  assert.strictEqual(group.length, 0, "constraint for different group should not appear");
});

test("property: mixed list is correctly partitioned", () => {
  const cases = [
    { scopeType: "Group", scopeId: GROUP_ID, expectedSection: "group" },
    { scopeType: "Role", scopeId: "r1", expectedSection: "role" },
    { scopeType: "Person", scopeId: "p1", expectedSection: "person" },
    { scopeType: "group", scopeId: GROUP_ID, expectedSection: "group" },  // case-insensitive
    { scopeType: "ROLE", scopeId: "r2", expectedSection: "role" },
    { scopeType: "PERSON", scopeId: "p2", expectedSection: "person" },
  ];

  for (const c of cases) {
    const constraints = [makeConstraint("c1", c.scopeType, c.scopeId)];
    const { group, role, person } = partitionConstraints(constraints, GROUP_ID);
    const counts = { group: group.length, role: role.length, person: person.length };
    assert.strictEqual(counts[c.expectedSection as keyof typeof counts], 1,
      `${c.scopeType} constraint should appear in ${c.expectedSection} section`);
    const otherSections = Object.entries(counts).filter(([k]) => k !== c.expectedSection);
    for (const [, count] of otherSections) {
      assert.strictEqual(count, 0, `${c.scopeType} constraint should not appear in other sections`);
    }
  }
});

// ── Property 13: Active-only role selector ────────────────────────────────────
// Feature: schedule-table-autoschedule-role-constraints, Property 13: active-only role selector

console.log("\nProperty 13: Active-only role selector");

test("inactive roles are excluded from selector", () => {
  const roles: GroupRoleDto[] = [
    { id: "r1", name: "Active Role", description: null, isActive: true, permissionLevel: "view" },
    { id: "r2", name: "Inactive Role", description: null, isActive: false, permissionLevel: "view" },
  ];
  const active = getActiveRoles(roles);
  assert.strictEqual(active.length, 1);
  assert.strictEqual(active[0].id, "r1");
});

test("all active roles appear in selector", () => {
  const roles: GroupRoleDto[] = [
    { id: "r1", name: "Role A", description: null, isActive: true, permissionLevel: "view" },
    { id: "r2", name: "Role B", description: null, isActive: true, permissionLevel: "ViewAndEdit" },
    { id: "r3", name: "Role C", description: null, isActive: true, permissionLevel: "Owner" },
  ];
  const active = getActiveRoles(roles);
  assert.strictEqual(active.length, 3);
});

test("empty role list → empty selector", () => {
  assert.strictEqual(getActiveRoles([]).length, 0);
});

test("property: for any list, selector contains exactly active roles", () => {
  for (let size = 0; size <= 8; size++) {
    const roles: GroupRoleDto[] = Array.from({ length: size }, (_, i) => ({
      id: `r${i}`,
      name: `Role ${i}`,
      description: null,
      isActive: i % 2 === 0,  // even indices are active
      permissionLevel: "view",
    }));
    const expected = roles.filter(r => r.isActive).length;
    const active = getActiveRoles(roles);
    assert.strictEqual(active.length, expected,
      `size ${size}: expected ${expected} active roles, got ${active.length}`);
    assert.ok(active.every(r => r.isActive), "all returned roles must be active");
  }
});

// ── Summary ───────────────────────────────────────────────────────────────────
console.log(`\n${"─".repeat(50)}`);
console.log(`Results: ${passed} passed, ${failed} failed`);
if (failed > 0) process.exit(1);
