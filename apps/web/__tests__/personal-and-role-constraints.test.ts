/**
 * Logic tests for personal-and-role-constraints feature.
 * Pure logic tests — no React rendering needed.
 * Run with: npx ts-node --project tsconfig.tests.json __tests__/personal-and-role-constraints.test.ts
 *
 * Validates: Requirements 1.1, 1.6, 2.3, 3.3, 8.1
 */

import * as assert from "assert";

// ---------------------------------------------------------------------------
// Helper: simple test runner
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// Types mirroring the frontend DTOs
// ---------------------------------------------------------------------------

interface GroupMemberDto {
  personId: string;
  fullName: string;
  displayName: string | null;
  invitationStatus: string; // "accepted" | "pending"
}

interface GroupRoleDto {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
}

interface ConstraintDto {
  id: string;
  scopeType: string; // "Person" | "Role" | "Group"
  scopeId: string | null;
  severity: string;
  ruleType: string;
  rulePayloadJson: string;
  isActive: boolean;
  effectiveFrom: string | null;
  effectiveUntil: string | null;
}

// ---------------------------------------------------------------------------
// Pure logic functions extracted from ConstraintsTab
// ---------------------------------------------------------------------------

/** Filters the person selector to registered members only (Property 4) */
function getRegisteredMembers(members: GroupMemberDto[]): GroupMemberDto[] {
  return members.filter(m => m.invitationStatus === "accepted");
}

/** Filters the role selector to active roles only (Property 5) */
function getActiveRoles(roles: GroupRoleDto[]): GroupRoleDto[] {
  return roles.filter(r => r.isActive);
}

/** Resolves a person's display name with fullName fallback (Property 3) */
function resolvePersonName(
  scopeId: string | null,
  memberMap: Map<string, string>
): string | undefined {
  if (!scopeId) return undefined;
  return memberMap.get(scopeId);
}

/** Builds the member map used in ConstraintsTab */
function buildMemberMap(members: GroupMemberDto[]): Map<string, string> {
  return new Map(members.map(m => [m.personId, m.displayName ?? m.fullName]));
}

/** Partitions a flat constraint list into group/person/role buckets */
function partitionConstraints(
  constraints: ConstraintDto[],
  groupId: string
): { group: ConstraintDto[]; person: ConstraintDto[]; role: ConstraintDto[] } {
  return {
    group: constraints.filter(c => c.scopeType?.toLowerCase() === "group" && c.scopeId === groupId),
    person: constraints.filter(c => c.scopeType?.toLowerCase() === "person"),
    role: constraints.filter(c => c.scopeType?.toLowerCase() === "role"),
  };
}

// ---------------------------------------------------------------------------
// Property 4: Person selector contains only registered members
// Feature: personal-and-role-constraints, Property 4: person selector contains only registered members
// Validates: Requirements 2.3, 8.1
// ---------------------------------------------------------------------------

console.log("\nProperty 4: Person selector contains only registered members");

test("empty list → empty selector", () => {
  assert.strictEqual(getRegisteredMembers([]).length, 0);
});

test("all accepted → all in selector", () => {
  const members: GroupMemberDto[] = [
    { personId: "p1", fullName: "Alice", displayName: null, invitationStatus: "accepted" },
    { personId: "p2", fullName: "Bob", displayName: null, invitationStatus: "accepted" },
  ];
  const result = getRegisteredMembers(members);
  assert.strictEqual(result.length, 2);
});

test("all pending → empty selector", () => {
  const members: GroupMemberDto[] = [
    { personId: "p1", fullName: "Alice", displayName: null, invitationStatus: "pending" },
    { personId: "p2", fullName: "Bob", displayName: null, invitationStatus: "pending" },
  ];
  const result = getRegisteredMembers(members);
  assert.strictEqual(result.length, 0);
});

test("mixed → only accepted in selector", () => {
  const members: GroupMemberDto[] = [
    { personId: "p1", fullName: "Alice", displayName: null, invitationStatus: "accepted" },
    { personId: "p2", fullName: "Bob", displayName: null, invitationStatus: "pending" },
    { personId: "p3", fullName: "Carol", displayName: null, invitationStatus: "accepted" },
  ];
  const result = getRegisteredMembers(members);
  assert.strictEqual(result.length, 2);
  assert.ok(result.every(m => m.invitationStatus === "accepted"), "all results must be accepted");
  assert.ok(!result.some(m => m.personId === "p2"), "pending member must not appear");
});

test("property: for any list, selector contains exactly the accepted members", () => {
  const cases: GroupMemberDto[][] = [
    [],
    [{ personId: "a", fullName: "A", displayName: null, invitationStatus: "accepted" }],
    [{ personId: "b", fullName: "B", displayName: null, invitationStatus: "pending" }],
    [
      { personId: "c", fullName: "C", displayName: null, invitationStatus: "accepted" },
      { personId: "d", fullName: "D", displayName: null, invitationStatus: "pending" },
      { personId: "e", fullName: "E", displayName: null, invitationStatus: "accepted" },
    ],
    Array.from({ length: 10 }, (_, i) => ({
      personId: `p${i}`,
      fullName: `Person ${i}`,
      displayName: null,
      invitationStatus: i % 2 === 0 ? "accepted" : "pending",
    })),
  ];

  for (const members of cases) {
    const expected = members.filter(m => m.invitationStatus === "accepted");
    const result = getRegisteredMembers(members);
    assert.strictEqual(result.length, expected.length,
      `expected ${expected.length} registered members, got ${result.length}`);
    assert.ok(result.every(m => m.invitationStatus === "accepted"),
      "all results must have invitationStatus === 'accepted'");
    assert.ok(!result.some(m => m.invitationStatus !== "accepted"),
      "no non-accepted member should appear");
  }
});

// ---------------------------------------------------------------------------
// Property 5: Role selector contains only active roles
// Feature: personal-and-role-constraints, Property 5: role selector contains only active roles
// Validates: Requirements 3.3
// ---------------------------------------------------------------------------

console.log("\nProperty 5: Role selector contains only active roles");

test("empty list → empty selector", () => {
  assert.strictEqual(getActiveRoles([]).length, 0);
});

test("all active → all in selector", () => {
  const roles: GroupRoleDto[] = [
    { id: "r1", name: "Soldier", description: null, isActive: true },
    { id: "r2", name: "Medic", description: null, isActive: true },
  ];
  assert.strictEqual(getActiveRoles(roles).length, 2);
});

test("all inactive → empty selector", () => {
  const roles: GroupRoleDto[] = [
    { id: "r1", name: "Old Role", description: null, isActive: false },
  ];
  assert.strictEqual(getActiveRoles(roles).length, 0);
});

test("mixed → only active in selector", () => {
  const roles: GroupRoleDto[] = [
    { id: "r1", name: "Active", description: null, isActive: true },
    { id: "r2", name: "Inactive", description: null, isActive: false },
    { id: "r3", name: "Also Active", description: null, isActive: true },
  ];
  const result = getActiveRoles(roles);
  assert.strictEqual(result.length, 2);
  assert.ok(result.every(r => r.isActive), "all results must be active");
  assert.ok(!result.some(r => r.id === "r2"), "inactive role must not appear");
});

test("property: for any list, selector contains exactly the active roles", () => {
  for (let size = 0; size <= 10; size++) {
    const roles: GroupRoleDto[] = Array.from({ length: size }, (_, i) => ({
      id: `r${i}`,
      name: `Role ${i}`,
      description: null,
      isActive: i % 3 !== 0, // every 3rd role is inactive
    }));
    const expected = roles.filter(r => r.isActive);
    const result = getActiveRoles(roles);
    assert.strictEqual(result.length, expected.length,
      `size ${size}: expected ${expected.length} active roles, got ${result.length}`);
    assert.ok(result.every(r => r.isActive), "all results must be active");
  }
});

// ---------------------------------------------------------------------------
// Property 3: Person name resolution uses displayName with fullName fallback
// Feature: personal-and-role-constraints, Property 3: person name resolution uses displayName with fullName fallback
// Validates: Requirements 1.6
// ---------------------------------------------------------------------------

console.log("\nProperty 3: Person name resolution uses displayName with fullName fallback");

test("displayName non-null → returns displayName", () => {
  const members: GroupMemberDto[] = [
    { personId: "p1", fullName: "Alice Israeli", displayName: "Alice", invitationStatus: "accepted" },
  ];
  const map = buildMemberMap(members);
  assert.strictEqual(resolvePersonName("p1", map), "Alice");
});

test("displayName null → returns fullName", () => {
  const members: GroupMemberDto[] = [
    { personId: "p1", fullName: "Alice Israeli", displayName: null, invitationStatus: "accepted" },
  ];
  const map = buildMemberMap(members);
  assert.strictEqual(resolvePersonName("p1", map), "Alice Israeli");
});

test("scopeId not in map → returns undefined (fallback to scopeId in UI)", () => {
  const map = buildMemberMap([]);
  assert.strictEqual(resolvePersonName("unknown-id", map), undefined);
});

test("null scopeId → returns undefined", () => {
  const map = buildMemberMap([]);
  assert.strictEqual(resolvePersonName(null, map), undefined);
});

test("property: for any member list, resolved name equals displayName ?? fullName", () => {
  const cases: Array<{ personId: string; fullName: string; displayName: string | null }> = [
    { personId: "a", fullName: "Full A", displayName: "Nick A" },
    { personId: "b", fullName: "Full B", displayName: null },
    { personId: "c", fullName: "Full C", displayName: "" },
    { personId: "d", fullName: "Full D", displayName: "D" },
  ];

  const members: GroupMemberDto[] = cases.map(c => ({ ...c, invitationStatus: "accepted" }));
  const map = buildMemberMap(members);

  for (const c of cases) {
    const expected = c.displayName ?? c.fullName;
    const resolved = resolvePersonName(c.personId, map);
    assert.strictEqual(resolved, expected,
      `personId ${c.personId}: expected "${expected}", got "${resolved}"`);
  }
});

// ---------------------------------------------------------------------------
// Task 9.4: Constraints partitioned correctly by scopeType
// Validates: Requirements 1.1
// ---------------------------------------------------------------------------

console.log("\nTask 9.4: Constraints partitioned by scopeType");

const GROUP_ID = "group-abc";

function makeConstraint(id: string, scopeType: string, scopeId: string | null): ConstraintDto {
  return {
    id,
    scopeType,
    scopeId,
    severity: "hard",
    ruleType: "min_rest_hours",
    rulePayloadJson: "{}",
    isActive: true,
    effectiveFrom: null,
    effectiveUntil: null,
  };
}

test("empty list → all buckets empty", () => {
  const { group, person, role } = partitionConstraints([], GROUP_ID);
  assert.strictEqual(group.length, 0);
  assert.strictEqual(person.length, 0);
  assert.strictEqual(role.length, 0);
});

test("group constraint with matching scopeId → in group bucket", () => {
  const c = makeConstraint("c1", "Group", GROUP_ID);
  const { group, person, role } = partitionConstraints([c], GROUP_ID);
  assert.strictEqual(group.length, 1);
  assert.strictEqual(person.length, 0);
  assert.strictEqual(role.length, 0);
});

test("group constraint with different scopeId → NOT in group bucket", () => {
  const c = makeConstraint("c1", "Group", "other-group");
  const { group } = partitionConstraints([c], GROUP_ID);
  assert.strictEqual(group.length, 0);
});

test("person constraint → in person bucket", () => {
  const c = makeConstraint("c1", "Person", "person-id-1");
  const { group, person, role } = partitionConstraints([c], GROUP_ID);
  assert.strictEqual(group.length, 0);
  assert.strictEqual(person.length, 1);
  assert.strictEqual(role.length, 0);
});

test("role constraint → in role bucket", () => {
  const c = makeConstraint("c1", "Role", "role-id-1");
  const { group, person, role } = partitionConstraints([c], GROUP_ID);
  assert.strictEqual(group.length, 0);
  assert.strictEqual(person.length, 0);
  assert.strictEqual(role.length, 1);
});

test("mixed list → correctly partitioned", () => {
  const constraints: ConstraintDto[] = [
    makeConstraint("c1", "Group", GROUP_ID),
    makeConstraint("c2", "Person", "p1"),
    makeConstraint("c3", "Role", "r1"),
    makeConstraint("c4", "Person", "p2"),
    makeConstraint("c5", "Group", "other-group"), // different group — excluded
  ];
  const { group, person, role } = partitionConstraints(constraints, GROUP_ID);
  assert.strictEqual(group.length, 1, "only 1 group constraint for this group");
  assert.strictEqual(person.length, 2, "2 person constraints");
  assert.strictEqual(role.length, 1, "1 role constraint");
});

test("scopeType case-insensitive partitioning", () => {
  const constraints: ConstraintDto[] = [
    makeConstraint("c1", "group", GROUP_ID),
    makeConstraint("c2", "PERSON", "p1"),
    makeConstraint("c3", "Role", "r1"),
  ];
  const { group, person, role } = partitionConstraints(constraints, GROUP_ID);
  assert.strictEqual(group.length, 1);
  assert.strictEqual(person.length, 1);
  assert.strictEqual(role.length, 1);
});

// ---------------------------------------------------------------------------
// Summary
// ---------------------------------------------------------------------------
console.log(`\n${"─".repeat(50)}`);
console.log(`Results: ${passed} passed, ${failed} failed`);
if (failed > 0) {
  process.exit(1);
}
