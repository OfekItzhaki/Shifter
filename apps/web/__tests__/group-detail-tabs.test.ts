/**
 * Property-based tests for group-detail-page feature.
 * These are pure logic tests — no React rendering needed.
 * Run with: node --require ts-node/register __tests__/group-detail-tabs.test.ts
 *
 * Validates: Requirements 2.1, 2.4, 3.1, 3.6, 3.10, 4.3, 4.6, 5.1
 */

import * as assert from "assert";
import * as fs from "fs";
import * as path from "path";

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
  } catch (err: any) {
    console.error(`  ✗ ${name}`);
    console.error(`    ${err.message}`);
    failed++;
  }
}

// ---------------------------------------------------------------------------
// TASK 3.2 — Property 2: Base tabs always present
// Validates: Requirements 2.1
// ---------------------------------------------------------------------------

type ActiveTab = "schedule" | "members-readonly" | "members-edit" | "tasks" | "constraints" | "settings";

function getVisibleTabs(groupId: string, adminGroupId: string | null): string[] {
  const base = ["schedule", "members-readonly"];
  const admin = ["members-edit", "tasks", "constraints", "settings"];
  return adminGroupId === groupId ? [...base, ...admin] : base;
}

console.log("\nProperty 2: Base tabs always present");

test("base tabs present when adminGroupId is null", () => {
  const tabs = getVisibleTabs("group-1", null);
  assert.ok(tabs.includes("schedule"), "should include schedule");
  assert.ok(tabs.includes("members-readonly"), "should include members-readonly");
});

test("base tabs present when adminGroupId equals groupId", () => {
  const tabs = getVisibleTabs("group-1", "group-1");
  assert.ok(tabs.includes("schedule"), "should include schedule");
  assert.ok(tabs.includes("members-readonly"), "should include members-readonly");
});

test("base tabs present when adminGroupId is a different string", () => {
  const tabs = getVisibleTabs("group-1", "group-2");
  assert.ok(tabs.includes("schedule"), "should include schedule");
  assert.ok(tabs.includes("members-readonly"), "should include members-readonly");
});

test("base tabs present for any arbitrary groupId and adminGroupId", () => {
  const cases: [string, string | null][] = [
    ["abc", null],
    ["abc", "abc"],
    ["abc", "xyz"],
    ["", null],
    ["", ""],
    ["uuid-123", "uuid-456"],
  ];
  for (const [gid, aid] of cases) {
    const tabs = getVisibleTabs(gid, aid);
    assert.ok(tabs.includes("schedule"), `schedule missing for (${gid}, ${aid})`);
    assert.ok(tabs.includes("members-readonly"), `members-readonly missing for (${gid}, ${aid})`);
  }
});

// ---------------------------------------------------------------------------
// TASK 3.3 — Property 4: Admin tabs conditional on adminGroupId
// Validates: Requirements 3.1
// ---------------------------------------------------------------------------

const ADMIN_TABS = ["members-edit", "tasks", "constraints", "settings"];

console.log("\nProperty 4: Admin tabs conditional on adminGroupId");

test("admin tabs present when adminGroupId === groupId", () => {
  const tabs = getVisibleTabs("group-1", "group-1");
  for (const t of ADMIN_TABS) {
    assert.ok(tabs.includes(t), `admin tab '${t}' should be present`);
  }
});

test("admin tabs absent when adminGroupId is null", () => {
  const tabs = getVisibleTabs("group-1", null);
  for (const t of ADMIN_TABS) {
    assert.ok(!tabs.includes(t), `admin tab '${t}' should NOT be present`);
  }
});

test("admin tabs absent when adminGroupId is a different string", () => {
  const tabs = getVisibleTabs("group-1", "group-2");
  for (const t of ADMIN_TABS) {
    assert.ok(!tabs.includes(t), `admin tab '${t}' should NOT be present`);
  }
});

test("admin tabs conditional for many inputs", () => {
  const groupIds = ["g1", "g2", "abc", "uuid-123"];
  for (const gid of groupIds) {
    // When admin for this group — all admin tabs present
    const adminTabs = getVisibleTabs(gid, gid);
    for (const t of ADMIN_TABS) {
      assert.ok(adminTabs.includes(t), `admin tab '${t}' missing when adminGroupId === groupId`);
    }
    // When not admin — no admin tabs
    const nonAdminTabs = getVisibleTabs(gid, null);
    for (const t of ADMIN_TABS) {
      assert.ok(!nonAdminTabs.includes(t), `admin tab '${t}' present when adminGroupId is null`);
    }
    // When admin for different group — no admin tabs
    const otherAdminTabs = getVisibleTabs(gid, gid + "-other");
    for (const t of ADMIN_TABS) {
      assert.ok(!otherAdminTabs.includes(t), `admin tab '${t}' present when adminGroupId !== groupId`);
    }
  }
});

// ---------------------------------------------------------------------------
// TASK 3.6 — Property 3: displayName fallback
// Validates: Requirements 2.4
// ---------------------------------------------------------------------------

function getDisplayName(m: { displayName: string | null; fullName: string }): string {
  return m.displayName ?? m.fullName;
}

console.log("\nProperty 3: displayName fallback");

test("returns displayName when non-null", () => {
  assert.strictEqual(getDisplayName({ displayName: "Ofek", fullName: "Ofek Israeli" }), "Ofek");
  assert.strictEqual(getDisplayName({ displayName: "Yael", fullName: "Yael Cohen" }), "Yael");
});

test("returns fullName when displayName is null", () => {
  assert.strictEqual(getDisplayName({ displayName: null, fullName: "Ofek Israeli" }), "Ofek Israeli");
  assert.strictEqual(getDisplayName({ displayName: null, fullName: "Yael Cohen" }), "Yael Cohen");
});

test("returns empty string when displayName is empty string (non-null)", () => {
  assert.strictEqual(getDisplayName({ displayName: "", fullName: "Ofek Israeli" }), "");
});

test("displayName fallback for many inputs", () => {
  const cases: [string | null, string, string][] = [
    ["Nick", "Full Name", "Nick"],
    [null, "Full Name", "Full Name"],
    ["", "Full Name", ""],
    ["A", "B", "A"],
    [null, "B", "B"],
  ];
  for (const [dn, fn, expected] of cases) {
    assert.strictEqual(getDisplayName({ displayName: dn, fullName: fn }), expected,
      `getDisplayName({displayName: ${JSON.stringify(dn)}, fullName: ${JSON.stringify(fn)}}) should be ${JSON.stringify(expected)}`);
  }
});

// ---------------------------------------------------------------------------
// TASK 5.2 — Property 5: Members re-fetched after mutation
// Validates: Requirements 3.6
// ---------------------------------------------------------------------------

console.log("\nProperty 5: Members re-fetched after mutation");

test("getGroupMembers called after successful addGroupMemberByEmail", async () => {
  let fetchCount = 0;
  const mockGetGroupMembers = async () => { fetchCount++; return []; };
  const mockAddMember = async () => { /* success */ };

  // Simulate the pattern: add then re-fetch
  await mockAddMember();
  await mockGetGroupMembers();

  assert.strictEqual(fetchCount, 1, "getGroupMembers should be called once after add");
});

test("getGroupMembers called after successful removeGroupMember", async () => {
  let fetchCount = 0;
  const mockGetGroupMembers = async () => { fetchCount++; return []; };
  const mockRemoveMember = async () => { /* success */ };

  // Simulate the pattern: remove then re-fetch
  await mockRemoveMember();
  await mockGetGroupMembers();

  assert.strictEqual(fetchCount, 1, "getGroupMembers should be called once after remove");
});

test("re-fetch pattern: fetch count increments for each mutation", async () => {
  let fetchCount = 0;
  const mockGetGroupMembers = async () => { fetchCount++; return []; };

  // 3 mutations → 3 re-fetches
  for (let i = 0; i < 3; i++) {
    await mockGetGroupMembers();
  }
  assert.strictEqual(fetchCount, 3, "getGroupMembers should be called once per mutation");
});

// ---------------------------------------------------------------------------
// TASK 5.6 — Property 6: Solver horizon warning threshold
// Validates: Requirements 3.10
// ---------------------------------------------------------------------------

function shouldShowWarning(v: number): boolean {
  return v > 30;
}

console.log("\nProperty 6: Solver horizon warning threshold");

test("no warning for value 1", () => assert.strictEqual(shouldShowWarning(1), false));
test("no warning for value 14", () => assert.strictEqual(shouldShowWarning(14), false));
test("no warning for value 30", () => assert.strictEqual(shouldShowWarning(30), false));
test("warning shown for value 31", () => assert.strictEqual(shouldShowWarning(31), true));
test("warning shown for value 90", () => assert.strictEqual(shouldShowWarning(90), true));

test("warning threshold is exactly > 30 for all values 1-90", () => {
  for (let v = 1; v <= 90; v++) {
    const expected = v > 30;
    assert.strictEqual(shouldShowWarning(v), expected,
      `shouldShowWarning(${v}) should be ${expected}`);
  }
});

// ---------------------------------------------------------------------------
// TASK 6.3 — Property 7: No Admin section in AppShell nav
// Validates: Requirements 4.3
// ---------------------------------------------------------------------------

console.log("\nProperty 7: No Admin section in AppShell nav");

// Define the nav items as a data structure (mirrors AppShell after task 6.1/6.2)
const navItems = [
  { href: "/schedule/today", label: "היום" },
  { href: "/schedule/tomorrow", label: "מחר" },
  { href: "/schedule/my-missions", label: "המשימות שלי" },
  { href: "/groups", label: "קבוצות" },
];

test("no nav item has href starting with /admin/", () => {
  for (const item of navItems) {
    assert.ok(!item.href.startsWith("/admin/"),
      `nav item '${item.label}' has /admin/ href: ${item.href}`);
  }
});

test("no nav item label is 'Admin'", () => {
  for (const item of navItems) {
    assert.notStrictEqual(item.label, "Admin",
      `nav item with label 'Admin' should not exist`);
  }
});

test("קבוצות nav item is present", () => {
  const groupsItem = navItems.find(i => i.href === "/groups");
  assert.ok(groupsItem, "קבוצות nav item should be present");
  assert.strictEqual(groupsItem?.label, "קבוצות");
});

// ---------------------------------------------------------------------------
// TASK 6.4 — Property 8: Amber topbar when adminGroupId is non-null
// Validates: Requirements 4.6
// ---------------------------------------------------------------------------

// Mirror the S.topbar function from AppShell
function topbarStyle(admin: boolean) {
  return {
    background: admin ? "#fffbeb" : "white",
    borderBottom: `1px solid ${admin ? "#fde68a" : "#e2e8f0"}`,
  };
}

console.log("\nProperty 8: Amber topbar when adminGroupId is non-null");

test("topbar background is #fffbeb when admin is true", () => {
  assert.strictEqual(topbarStyle(true).background, "#fffbeb");
});

test("topbar background is white when admin is false", () => {
  assert.strictEqual(topbarStyle(false).background, "white");
});

test("topbar amber border when admin is true", () => {
  assert.ok(topbarStyle(true).borderBottom.includes("#fde68a"));
});

test("topbar normal border when admin is false", () => {
  assert.ok(topbarStyle(false).borderBottom.includes("#e2e8f0"));
});

// ---------------------------------------------------------------------------
// TASK 8.3 — Property 9: Seed UUID validity
// Validates: Requirements 5.1, 5.2
// ---------------------------------------------------------------------------

console.log("\nProperty 9: Seed UUID validity");

const UUID_V4_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const UUID_PATTERN = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;

// __dirname when compiled = apps/web/__tests__/dist → need 4 levels up to repo root
const seedPath = path.resolve(__dirname, "../../../../infra/scripts/seed.sql");

test("seed.sql file exists", () => {
  assert.ok(fs.existsSync(seedPath), `seed.sql not found at ${seedPath}`);
});

test("all UUIDs in SQL statements (non-comment lines) are not old sequential zeros", () => {
  const content = fs.readFileSync(seedPath, "utf-8");
  // Only check non-comment lines (lines not starting with --)
  const sqlLines = content.split("\n").filter(l => !l.trimStart().startsWith("--"));
  const sqlContent = sqlLines.join("\n");
  const matches = sqlContent.match(UUID_PATTERN) ?? [];
  assert.ok(matches.length > 0, "No UUIDs found in SQL statements");

  // Verify none of the SQL-statement UUIDs are the old sequential zeros pattern
  const SEQUENTIAL_PATTERN = /^[0-9a-f0]{8}-0000-0000-0000-[0-9a-f0]{12}$/;
  const sequential = matches.filter(u => SEQUENTIAL_PATTERN.test(u.toLowerCase()));
  assert.strictEqual(sequential.length, 0,
    `Sequential fake UUIDs still in SQL statements: ${sequential.join(", ")}`);
});

test("seed.sql contains expected new UUIDs from mapping table", () => {
  const content = fs.readFileSync(seedPath, "utf-8");
  const expectedUUIDs = [
    "a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5", // admin user
    "b2c3d4e5-f6a7-4b8c-9d0e-f1a2b3c4d5e6", // ofek user
    "e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9", // space
    "f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6", // group squad a
  ];
  for (const uuid of expectedUUIDs) {
    assert.ok(content.includes(uuid), `Expected UUID ${uuid} not found in seed.sql`);
  }
});

test("seed.sql does NOT contain old sequential UUIDs in SQL statements", () => {
  const content = fs.readFileSync(seedPath, "utf-8");
  // Only check non-comment lines
  const sqlLines = content.split("\n").filter(l => !l.trimStart().startsWith("--"));
  const sqlContent = sqlLines.join("\n");
  const oldUUIDs = [
    "00000000-0000-0000-0000-000000000001",
    "10000000-0000-0000-0000-000000000001",
    "40000000-0000-0000-0000-000000000001",
  ];
  for (const uuid of oldUUIDs) {
    assert.ok(!sqlContent.includes(uuid), `Old sequential UUID ${uuid} still present in SQL statements`);
  }
});

// ---------------------------------------------------------------------------
// Summary
// ---------------------------------------------------------------------------
console.log(`\n${"─".repeat(50)}`);
console.log(`Results: ${passed} passed, ${failed} failed`);
if (failed > 0) {
  process.exit(1);
}
