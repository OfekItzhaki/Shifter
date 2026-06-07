/**
 * Logic tests for GroupDetailPage tab visibility and group lookup.
 * Feature: group-detail-page
 * Validates: Tasks 2.2, 3.2, 3.3, 3.6, 5.6, 6.3, 6.4
 */

import { test } from "vitest";
import * as assert from "assert";

// ── Types ─────────────────────────────────────────────────────────────────────

interface GroupWithMemberCountDto {
  id: string;
  name: string;
  memberCount: number;
  solverHorizonDays: number;
  ownerPersonId: string | null;
}

interface GroupMemberDto {
  personId: string;
  fullName: string;
  displayName: string | null;
  isOwner: boolean;
  phoneNumber: string | null;
  invitationStatus: string;
  profileImageUrl: string | null;
  birthday: string | null;
  linkedUserId: string | null;
}

// ── Pure logic functions ──────────────────────────────────────────────────────

function findGroup(groups: GroupWithMemberCountDto[], groupId: string): GroupWithMemberCountDto | null {
  return groups.find(g => g.id === groupId) ?? null;
}

function getDisplayName(m: GroupMemberDto): string {
  return m.displayName ?? m.fullName;
}

function isAdminTabVisible(adminGroupId: string | null, groupId: string): boolean {
  return adminGroupId === groupId;
}

function getSolverHorizonWarning(horizon: number): boolean {
  return horizon > 30;
}

function getTransferDropdownMembers(members: GroupMemberDto[]): GroupMemberDto[] {
  return members.filter(m => !m.isOwner);
}

// ── Test runner ───────────────────────────────────────────────────────────────

// ── Property 1: Group lookup correctness ─────────────────────────────────────
// Feature: group-detail-page, Property 1: group lookup correctness

test("finds group by ID", () => {
  const groups: GroupWithMemberCountDto[] = [
    { id: "g1", name: "Alpha", memberCount: 3, solverHorizonDays: 7, ownerPersonId: "p1" },
    { id: "g2", name: "Beta", memberCount: 5, solverHorizonDays: 14, ownerPersonId: "p2" },
  ];
  const result = findGroup(groups, "g1");
  assert.strictEqual(result?.name, "Alpha");
});

test("returns null for unknown group ID", () => {
  const groups: GroupWithMemberCountDto[] = [
    { id: "g1", name: "Alpha", memberCount: 3, solverHorizonDays: 7, ownerPersonId: "p1" },
  ];
  assert.strictEqual(findGroup(groups, "unknown"), null);
});

test("returns null for empty list", () => {
  assert.strictEqual(findGroup([], "g1"), null);
});

test("property: for any list and any ID, result is the matching group or null", () => {
  const groups: GroupWithMemberCountDto[] = Array.from({ length: 5 }, (_, i) => ({
    id: `g${i}`, name: `Group ${i}`, memberCount: i, solverHorizonDays: 7, ownerPersonId: null
  }));
  for (const g of groups) {
    const found = findGroup(groups, g.id);
    assert.strictEqual(found?.id, g.id, `should find group ${g.id}`);
  }
  assert.strictEqual(findGroup(groups, "nonexistent"), null);
});

// ── Property 2: Base tabs always present ─────────────────────────────────────
// Feature: group-detail-page, Property 2: base tabs always present

const BASE_TABS = ["schedule", "live-status", "members"];
const ADMIN_TABS = ["tasks", "constraints", "settings", "stats"];

function getVisibleTabs(adminGroupId: string | null, groupId: string): string[] {
  const tabs = [...BASE_TABS];
  if (adminGroupId === groupId) tabs.push(...ADMIN_TABS);
  return tabs;
}

test("base tabs always visible regardless of admin state", () => {
  const groupId = "g1";
  for (const adminGroupId of [null, "g1", "g2", "other"]) {
    const tabs = getVisibleTabs(adminGroupId, groupId);
    for (const baseTab of BASE_TABS) {
      assert.ok(tabs.includes(baseTab), `${baseTab} should always be visible (adminGroupId=${adminGroupId})`);
    }
  }
});

// ── Property 4: Admin tabs conditional on adminGroupId ───────────────────────
// Feature: group-detail-page, Property 4: admin tabs appear exactly when adminGroupId matches

test("admin tabs visible when adminGroupId matches groupId", () => {
  const tabs = getVisibleTabs("g1", "g1");
  for (const adminTab of ADMIN_TABS) {
    assert.ok(tabs.includes(adminTab), `${adminTab} should be visible when admin`);
  }
});

test("admin tabs hidden when adminGroupId is null", () => {
  const tabs = getVisibleTabs(null, "g1");
  for (const adminTab of ADMIN_TABS) {
    assert.ok(!tabs.includes(adminTab), `${adminTab} should be hidden when not admin`);
  }
});

test("admin tabs hidden when adminGroupId is different group", () => {
  const tabs = getVisibleTabs("g2", "g1");
  for (const adminTab of ADMIN_TABS) {
    assert.ok(!tabs.includes(adminTab), `${adminTab} should be hidden for different group`);
  }
});

test("property: admin tabs present iff adminGroupId === groupId", () => {
  const groupId = "target-group";
  const cases = [null, "target-group", "other-group", ""];
  for (const adminGroupId of cases) {
    const tabs = getVisibleTabs(adminGroupId, groupId);
    const shouldHaveAdmin = adminGroupId === groupId;
    for (const adminTab of ADMIN_TABS) {
      if (shouldHaveAdmin) {
        assert.ok(tabs.includes(adminTab), `${adminTab} should be visible when adminGroupId matches`);
      } else {
        assert.ok(!tabs.includes(adminTab), `${adminTab} should be hidden when adminGroupId doesn't match`);
      }
    }
  }
});

// ── Property 3: DisplayName fallback ─────────────────────────────────────────
// Feature: group-detail-page, Property 3: displayName fallback

test("returns displayName when non-null", () => {
  const m: GroupMemberDto = { personId: "p1", fullName: "Alice Israeli", displayName: "Alice", isOwner: false, phoneNumber: null, invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: null };
  assert.strictEqual(getDisplayName(m), "Alice");
});

test("returns fullName when displayName is null", () => {
  const m: GroupMemberDto = { personId: "p1", fullName: "Alice Israeli", displayName: null, isOwner: false, phoneNumber: null, invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: null };
  assert.strictEqual(getDisplayName(m), "Alice Israeli");
});

test("property: for any member, result is displayName ?? fullName", () => {
  const cases = [
    { displayName: "Nick", fullName: "Full Name", expected: "Nick" },
    { displayName: null, fullName: "Full Name", expected: "Full Name" },
    { displayName: "", fullName: "Full Name", expected: "" },
  ];
  for (const c of cases) {
    const m: GroupMemberDto = { personId: "p", fullName: c.fullName, displayName: c.displayName, isOwner: false, phoneNumber: null, invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: null };
    assert.strictEqual(getDisplayName(m), c.expected);
  }
});

// ── Property 6: Solver horizon warning threshold ──────────────────────────────
// Feature: group-detail-page, Property 6: solver horizon warning threshold

test("warning shown when horizon > 30", () => {
  assert.strictEqual(getSolverHorizonWarning(31), true);
  assert.strictEqual(getSolverHorizonWarning(90), true);
});

test("no warning when horizon <= 30", () => {
  assert.strictEqual(getSolverHorizonWarning(30), false);
  assert.strictEqual(getSolverHorizonWarning(1), false);
  assert.strictEqual(getSolverHorizonWarning(14), false);
});

test("property: warning iff horizon > 30", () => {
  for (let v = 1; v <= 90; v++) {
    assert.strictEqual(getSolverHorizonWarning(v), v > 30, `horizon=${v}: warning should be ${v > 30}`);
  }
});

// ── Property 16: Transfer dropdown excludes owner ────────────────────────────
// Feature: group-ownership, Property 16: transfer dropdown excludes owner

test("owner excluded from transfer dropdown", () => {
  const members: GroupMemberDto[] = [
    { personId: "p1", fullName: "Owner", displayName: null, isOwner: true, phoneNumber: null, invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: "u1" },
    { personId: "p2", fullName: "Member A", displayName: null, isOwner: false, phoneNumber: null, invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: "u2" },
    { personId: "p3", fullName: "Member B", displayName: null, isOwner: false, phoneNumber: null, invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: "u3" },
  ];
  const dropdown = getTransferDropdownMembers(members);
  assert.strictEqual(dropdown.length, 2);
  assert.ok(!dropdown.some(m => m.isOwner), "owner must not appear in dropdown");
});

test("property: for any member list with one owner, dropdown excludes that owner", () => {
  for (let size = 1; size <= 8; size++) {
    const members: GroupMemberDto[] = Array.from({ length: size }, (_, i) => ({
      personId: `p${i}`, fullName: `Person ${i}`, displayName: null,
      isOwner: i === 0, // first person is owner
      phoneNumber: null, invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: `u${i}`
    }));
    const dropdown = getTransferDropdownMembers(members);
    assert.strictEqual(dropdown.length, size - 1, `size ${size}: dropdown should have ${size - 1} members`);
    assert.ok(!dropdown.some(m => m.isOwner), "owner must not appear in dropdown");
  }
});
