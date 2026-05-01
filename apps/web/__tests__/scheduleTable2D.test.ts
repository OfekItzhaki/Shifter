/**
 * Logic tests for ScheduleTable2D data transformation.
 * Tests the pure functions that derive columns, rows, and cell data from assignments.
 * Feature: schedule-table-autoschedule-role-constraints
 * Validates: Tasks 24.1, 24.2, 24.3, 24.4
 */

import * as assert from "assert";

// ── Types ─────────────────────────────────────────────────────────────────────

interface ScheduleAssignment {
  personName: string;
  taskTypeName: string;
  slotStartsAt: string;
  slotEndsAt: string;
}

// ── Pure logic extracted from ScheduleTable2D ─────────────────────────────────

function overlapsDate(startsAt: string, endsAt: string, filterDate: string): boolean {
  const dayStart = filterDate + "T00:00:00";
  const dayEnd = filterDate + "T23:59:59";
  return startsAt <= dayEnd && endsAt >= dayStart;
}

function deriveColumns(assignments: ScheduleAssignment[]): string[] {
  return [...new Set(assignments.map(a => a.taskTypeName))].sort();
}

function deriveRows(assignments: ScheduleAssignment[]): Array<{ startsAt: string; endsAt: string }> {
  const seen = new Set<string>();
  const rows: Array<{ startsAt: string; endsAt: string }> = [];
  for (const a of assignments) {
    const key = `${a.slotStartsAt}|${a.slotEndsAt}`;
    if (!seen.has(key)) {
      seen.add(key);
      rows.push({ startsAt: a.slotStartsAt, endsAt: a.slotEndsAt });
    }
  }
  return rows.sort((a, b) => a.startsAt.localeCompare(b.startsAt));
}

function buildCellMap(
  assignments: ScheduleAssignment[]
): Map<string, Map<string, string[]>> {
  const map = new Map<string, Map<string, string[]>>();
  for (const a of assignments) {
    const slotKey = `${a.slotStartsAt}|${a.slotEndsAt}`;
    if (!map.has(slotKey)) map.set(slotKey, new Map());
    const taskMap = map.get(slotKey)!;
    if (!taskMap.has(a.taskTypeName)) taskMap.set(a.taskTypeName, []);
    taskMap.get(a.taskTypeName)!.push(a.personName);
  }
  return map;
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

// ── Property 1: Column and row completeness ───────────────────────────────────
// Feature: schedule-table-autoschedule-role-constraints, Property 1: ScheduleTable2D column and row completeness

console.log("\nProperty 1: Column and row completeness");

test("unique task names become columns (sorted)", () => {
  const assignments: ScheduleAssignment[] = [
    { personName: "Alice", taskTypeName: "Guard", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
    { personName: "Bob", taskTypeName: "Kitchen", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
    { personName: "Carol", taskTypeName: "Guard", slotStartsAt: "2026-04-20T16:00:00", slotEndsAt: "2026-04-21T00:00:00" },
  ];
  const cols = deriveColumns(assignments);
  assert.deepStrictEqual(cols, ["Guard", "Kitchen"], "columns should be unique task names sorted alphabetically");
});

test("unique slot pairs become rows (sorted by start time)", () => {
  const assignments: ScheduleAssignment[] = [
    { personName: "Alice", taskTypeName: "Guard", slotStartsAt: "2026-04-20T16:00:00", slotEndsAt: "2026-04-21T00:00:00" },
    { personName: "Bob", taskTypeName: "Kitchen", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
    { personName: "Carol", taskTypeName: "Guard", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
  ];
  const rows = deriveRows(assignments);
  assert.strictEqual(rows.length, 2, "should have 2 unique slot pairs");
  assert.strictEqual(rows[0].startsAt, "2026-04-20T08:00:00", "earlier slot should come first");
  assert.strictEqual(rows[1].startsAt, "2026-04-20T16:00:00");
});

test("property: N unique tasks → N columns", () => {
  for (let n = 1; n <= 10; n++) {
    const assignments: ScheduleAssignment[] = Array.from({ length: n }, (_, i) => ({
      personName: `Person${i}`,
      taskTypeName: `Task${i}`,
      slotStartsAt: "2026-04-20T08:00:00",
      slotEndsAt: "2026-04-20T16:00:00",
    }));
    const cols = deriveColumns(assignments);
    assert.strictEqual(cols.length, n, `${n} unique tasks should produce ${n} columns`);
  }
});

// ── Property 2: Multi-person cell grouping ────────────────────────────────────
// Feature: schedule-table-autoschedule-role-constraints, Property 2: multi-person cell grouping

console.log("\nProperty 2: Multi-person cell grouping");

test("multiple people in same slot+task appear in same cell", () => {
  const assignments: ScheduleAssignment[] = [
    { personName: "Alice", taskTypeName: "Guard", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
    { personName: "Bob", taskTypeName: "Guard", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
    { personName: "Carol", taskTypeName: "Guard", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
  ];
  const cellMap = buildCellMap(assignments);
  const slotKey = "2026-04-20T08:00:00|2026-04-20T16:00:00";
  const names = cellMap.get(slotKey)?.get("Guard") ?? [];
  assert.strictEqual(names.length, 3, "all 3 people should be in the same cell");
  assert.ok(names.includes("Alice"));
  assert.ok(names.includes("Bob"));
  assert.ok(names.includes("Carol"));
});

test("property: N people in same slot → N names in cell", () => {
  for (let n = 2; n <= 5; n++) {
    const assignments: ScheduleAssignment[] = Array.from({ length: n }, (_, i) => ({
      personName: `Person${i}`,
      taskTypeName: "Guard",
      slotStartsAt: "2026-04-20T08:00:00",
      slotEndsAt: "2026-04-20T16:00:00",
    }));
    const cellMap = buildCellMap(assignments);
    const names = cellMap.get("2026-04-20T08:00:00|2026-04-20T16:00:00")?.get("Guard") ?? [];
    assert.strictEqual(names.length, n, `${n} people should produce ${n} names in cell`);
  }
});

// ── Property 3: Current user column highlight ─────────────────────────────────
// Feature: schedule-table-autoschedule-role-constraints, Property 3: current user column highlight

console.log("\nProperty 3: Current user column highlight");

function getHighlightedColumns(assignments: ScheduleAssignment[], currentUserName: string): Set<string> {
  const highlighted = new Set<string>();
  for (const a of assignments) {
    if (a.personName === currentUserName) {
      highlighted.add(a.taskTypeName);
    }
  }
  return highlighted;
}

test("current user's task column is highlighted", () => {
  const assignments: ScheduleAssignment[] = [
    { personName: "Alice", taskTypeName: "Guard", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
    { personName: "Bob", taskTypeName: "Kitchen", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
  ];
  const highlighted = getHighlightedColumns(assignments, "Alice");
  assert.ok(highlighted.has("Guard"), "Alice's task column should be highlighted");
  assert.ok(!highlighted.has("Kitchen"), "Bob's task column should not be highlighted");
});

test("property: for any user, only their tasks are highlighted", () => {
  const people = ["Alice", "Bob", "Carol"];
  const tasks = ["Guard", "Kitchen", "Patrol"];
  const assignments: ScheduleAssignment[] = people.map((p, i) => ({
    personName: p,
    taskTypeName: tasks[i],
    slotStartsAt: "2026-04-20T08:00:00",
    slotEndsAt: "2026-04-20T16:00:00",
  }));

  for (const person of people) {
    const highlighted = getHighlightedColumns(assignments, person);
    const personTask = assignments.find(a => a.personName === person)!.taskTypeName;
    assert.ok(highlighted.has(personTask), `${person}'s task should be highlighted`);
    for (const other of people.filter(p => p !== person)) {
      const otherTask = assignments.find(a => a.personName === other)!.taskTypeName;
      assert.ok(!highlighted.has(otherTask), `${other}'s task should not be highlighted for ${person}`);
    }
  }
});

// ── Property 4: Date filter correctness ──────────────────────────────────────
// Feature: schedule-table-autoschedule-role-constraints, Property 4: date filter correctness

console.log("\nProperty 4: Date filter correctness");

test("assignments on filter date are included", () => {
  const filterDate = "2026-04-20";
  const assignments: ScheduleAssignment[] = [
    { personName: "Alice", taskTypeName: "Guard", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
    { personName: "Bob", taskTypeName: "Kitchen", slotStartsAt: "2026-04-21T08:00:00", slotEndsAt: "2026-04-21T16:00:00" },
  ];
  const filtered = assignments.filter(a => overlapsDate(a.slotStartsAt, a.slotEndsAt, filterDate));
  assert.strictEqual(filtered.length, 1);
  assert.strictEqual(filtered[0].personName, "Alice");
});

test("assignments on different date are excluded", () => {
  const filterDate = "2026-04-22";
  const assignments: ScheduleAssignment[] = [
    { personName: "Alice", taskTypeName: "Guard", slotStartsAt: "2026-04-20T08:00:00", slotEndsAt: "2026-04-20T16:00:00" },
    { personName: "Bob", taskTypeName: "Kitchen", slotStartsAt: "2026-04-21T08:00:00", slotEndsAt: "2026-04-21T16:00:00" },
  ];
  const filtered = assignments.filter(a => overlapsDate(a.slotStartsAt, a.slotEndsAt, filterDate));
  assert.strictEqual(filtered.length, 0);
});

test("overnight assignment overlapping filter date is included", () => {
  // Slot starts on Apr 20 at 22:00, ends Apr 21 at 06:00
  // Filter date Apr 21 should include it
  const filterDate = "2026-04-21";
  const assignments: ScheduleAssignment[] = [
    { personName: "Alice", taskTypeName: "Night Guard", slotStartsAt: "2026-04-20T22:00:00", slotEndsAt: "2026-04-21T06:00:00" },
  ];
  const filtered = assignments.filter(a => overlapsDate(a.slotStartsAt, a.slotEndsAt, filterDate));
  assert.strictEqual(filtered.length, 1, "overnight assignment should be included for the end date");
});

test("property: filter returns exactly assignments overlapping the date", () => {
  const dates = ["2026-04-20", "2026-04-21", "2026-04-22", "2026-04-23"];
  const assignments: ScheduleAssignment[] = dates.map((d, i) => ({
    personName: `Person${i}`,
    taskTypeName: "Guard",
    slotStartsAt: `${d}T08:00:00`,
    slotEndsAt: `${d}T16:00:00`,
  }));

  for (const filterDate of dates) {
    const filtered = assignments.filter(a => overlapsDate(a.slotStartsAt, a.slotEndsAt, filterDate));
    assert.strictEqual(filtered.length, 1, `filter for ${filterDate} should return exactly 1 assignment`);
    assert.ok(filtered[0].slotStartsAt.startsWith(filterDate));
  }
});

// ── Summary ───────────────────────────────────────────────────────────────────
console.log(`\n${"─".repeat(50)}`);
console.log(`Results: ${passed} passed, ${failed} failed`);
if (failed > 0) process.exit(1);
