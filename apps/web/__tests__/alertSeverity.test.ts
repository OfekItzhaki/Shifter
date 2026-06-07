// Feature: group-alerts-and-phone
// Property 10: Severity badge color is correct for all severity values

import { test } from "vitest";
import * as assert from "assert";
import { SEVERITY_BADGE, getSeverityBadge } from "../lib/utils/alertSeverity";

test("info severity returns blue classes", () => {
  const badge = getSeverityBadge("info");
  assert.ok(badge.bg.includes("blue"), `info bg should contain 'blue', got: ${badge.bg}`);
  assert.ok(badge.text.includes("blue"), `info text should contain 'blue', got: ${badge.text}`);
  assert.strictEqual(badge.labelKey, "info");
});

test("warning severity returns amber classes", () => {
  const badge = getSeverityBadge("warning");
  assert.ok(badge.bg.includes("amber"), `warning bg should contain 'amber', got: ${badge.bg}`);
  assert.ok(badge.text.includes("amber"), `warning text should contain 'amber', got: ${badge.text}`);
  assert.strictEqual(badge.labelKey, "warning");
});

test("critical severity returns red classes", () => {
  const badge = getSeverityBadge("critical");
  assert.ok(badge.bg.includes("red"), `critical bg should contain 'red', got: ${badge.bg}`);
  assert.ok(badge.text.includes("red"), `critical text should contain 'red', got: ${badge.text}`);
  assert.strictEqual(badge.labelKey, "critical");
});

test("case-insensitive: INFO, WARNING, CRITICAL all resolve correctly", () => {
  assert.ok(getSeverityBadge("INFO").bg.includes("blue"));
  assert.ok(getSeverityBadge("WARNING").bg.includes("amber"));
  assert.ok(getSeverityBadge("CRITICAL").bg.includes("red"));
});

test("unknown severity falls back to info (blue)", () => {
  const badge = getSeverityBadge("unknown");
  assert.ok(badge.bg.includes("blue"), `unknown should fall back to blue, got: ${badge.bg}`);
});

test("all three severities have distinct bg colors", () => {
  const infoBg = getSeverityBadge("info").bg;
  const warningBg = getSeverityBadge("warning").bg;
  const criticalBg = getSeverityBadge("critical").bg;
  assert.notStrictEqual(infoBg, warningBg, "info and warning should have different bg");
  assert.notStrictEqual(infoBg, criticalBg, "info and critical should have different bg");
  assert.notStrictEqual(warningBg, criticalBg, "warning and critical should have different bg");
});

test("SEVERITY_BADGE map contains exactly info, warning, critical", () => {
  const keys = Object.keys(SEVERITY_BADGE);
  assert.ok(keys.includes("info"), "SEVERITY_BADGE must have 'info'");
  assert.ok(keys.includes("warning"), "SEVERITY_BADGE must have 'warning'");
  assert.ok(keys.includes("critical"), "SEVERITY_BADGE must have 'critical'");
});

test("100 iterations — all valid severities return correct color family", () => {
  const cases = [
    { severity: "info", expectedColor: "blue" },
    { severity: "warning", expectedColor: "amber" },
    { severity: "critical", expectedColor: "red" },
  ];
  for (let i = 0; i < 100; i++) {
    const { severity, expectedColor } = cases[i % cases.length];
    const badge = getSeverityBadge(severity);
    assert.ok(badge.bg.includes(expectedColor),
      `Iteration ${i}: ${severity} bg should contain '${expectedColor}', got: ${badge.bg}`);
    assert.ok(badge.text.includes(expectedColor),
      `Iteration ${i}: ${severity} text should contain '${expectedColor}', got: ${badge.text}`);
  }
});
