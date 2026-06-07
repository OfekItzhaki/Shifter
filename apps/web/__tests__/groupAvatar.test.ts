import { test } from "vitest";
import * as assert from "assert";
import { getAvatarColor, getAvatarLetter } from "../lib/utils/groupAvatar";

const COLORS = [
  "#3B82F6", "#10B981", "#F59E0B", "#EF4444",
  "#8B5CF6", "#EC4899", "#06B6D4", "#84CC16"
];

test("same name always returns same color", () => {
  const names = ["Squad A", "Squad B", "Alpha", "Beta", "הקבוצה שלי", ""];
  for (const name of names) {
    assert.strictEqual(getAvatarColor(name), getAvatarColor(name),
      `getAvatarColor("${name}") should be deterministic`);
  }
});
test("empty/null name returns fallback color", () => {
  assert.strictEqual(getAvatarColor(""), "#94A3B8");
});
test("color is always from the palette", () => {
  const testNames = ["A", "B", "Squad", "Team", "Group", "Alpha", "Beta", "Gamma", "Delta"];
  for (const name of testNames) {
    const color = getAvatarColor(name);
    assert.ok(COLORS.includes(color) || color === "#94A3B8",
      `color ${color} for "${name}" should be in palette`);
  }
});

test("returns first letter uppercase for non-empty name", () => {
  assert.strictEqual(getAvatarLetter("squad"), "S");
  assert.strictEqual(getAvatarLetter("alpha"), "A");
  assert.strictEqual(getAvatarLetter("הקבוצה"), "ה");
});
test("returns ? for empty string", () => {
  assert.strictEqual(getAvatarLetter(""), "?");
});
test("already uppercase stays uppercase", () => {
  assert.strictEqual(getAvatarLetter("Squad"), "S");
  assert.strictEqual(getAvatarLetter("ALPHA"), "A");
});
test("letter is always single character", () => {
  const names = ["Squad A", "Team B", "Alpha", "Beta"];
  for (const name of names) {
    assert.strictEqual(getAvatarLetter(name).length, 1);
  }
});
