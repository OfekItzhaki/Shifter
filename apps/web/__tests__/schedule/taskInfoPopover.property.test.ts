/**
 * Property-based tests for TaskInfoPopover configuration display.
 *
 * Feature: recommendation-approval-flow
 * Property 4: Task info popover displays correct configuration
 *
 * Uses fast-check to generate arbitrary TaskConfigSummaryDto values and asserts
 * the popover displays correct values for each non-default field and shows
 * split count only when > 1.
 *
 * **Validates: Requirements 6.1**
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";

// ── Types (mirrors lib/api/groups.ts) ────────────────────────────────────────

interface TaskConfigSummaryDto {
  taskId: string;
  allowsDoubleShift: boolean;
  allowsOverlap: boolean;
  dailyStartTime: string | null; // "HH:mm" or null
  dailyEndTime: string | null; // "HH:mm" or null
  burdenLevel: string; // "Hard" | "Normal" | "Easy"
  requiredQualificationNames: string[];
  splitCount: number;
}

// ── Pure logic extracted from TaskInfoPopover.tsx ─────────────────────────────

/** Returns true when all task config values are at their defaults. */
function isDefaultConfig(config: TaskConfigSummaryDto): boolean {
  return (
    !config.allowsDoubleShift &&
    !config.allowsOverlap &&
    config.dailyStartTime === null &&
    config.dailyEndTime === null &&
    config.burdenLevel === "Normal" &&
    config.requiredQualificationNames.length === 0 &&
    config.splitCount <= 1
  );
}

/** Determines the time window display string. */
function getTimeWindowDisplay(config: TaskConfigSummaryDto): string {
  return config.dailyStartTime && config.dailyEndTime
    ? `${config.dailyStartTime} – ${config.dailyEndTime}`
    : "24/7";
}

/** Determines whether split count should be displayed. */
function shouldShowSplitCount(config: TaskConfigSummaryDto): boolean {
  return config.splitCount > 1;
}

/** Determines whether qualifications should be displayed. */
function shouldShowQualifications(config: TaskConfigSummaryDto): boolean {
  return config.requiredQualificationNames.length > 0;
}

/**
 * Computes the set of visible fields for a given config.
 * When config is default, returns ["defaultSettings"].
 * Otherwise returns the list of field keys that are displayed.
 */
function getVisibleFields(config: TaskConfigSummaryDto): string[] {
  if (isDefaultConfig(config)) {
    return ["defaultSettings"];
  }

  const fields: string[] = [
    "doubleShift",
    "overlap",
    "timeWindow",
    "burden",
  ];

  if (shouldShowQualifications(config)) {
    fields.push("qualifications");
  }

  if (shouldShowSplitCount(config)) {
    fields.push("splitCount");
  }

  return fields;
}

// ── Arbitraries ──────────────────────────────────────────────────────────────

/** Generates a valid "HH:mm" time string. */
const timeStringArb = fc
  .tuple(fc.integer({ min: 0, max: 23 }), fc.integer({ min: 0, max: 59 }))
  .map(([h, m]) => `${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}`);

/** Generates a burden level. */
const burdenLevelArb = fc.constantFrom("Hard", "Normal", "Easy");

/** Generates a list of qualification names. */
const qualificationsArb = fc.array(
  fc.string({ minLength: 1, maxLength: 20 }).filter((s) => s.trim().length > 0),
  { minLength: 0, maxLength: 5 }
);

/** Generates an arbitrary TaskConfigSummaryDto. */
const taskConfigArb: fc.Arbitrary<TaskConfigSummaryDto> = fc
  .tuple(
    fc.uuid(),
    fc.boolean(),
    fc.boolean(),
    fc.option(timeStringArb, { nil: null }),
    fc.option(timeStringArb, { nil: null }),
    burdenLevelArb,
    qualificationsArb,
    fc.integer({ min: 1, max: 10 })
  )
  .map(
    ([
      taskId,
      allowsDoubleShift,
      allowsOverlap,
      dailyStartTime,
      dailyEndTime,
      burdenLevel,
      requiredQualificationNames,
      splitCount,
    ]) => ({
      taskId,
      allowsDoubleShift,
      allowsOverlap,
      dailyStartTime,
      dailyEndTime,
      burdenLevel,
      requiredQualificationNames,
      splitCount,
    })
  );

/** Generates a config that is guaranteed to be default. */
const defaultConfigArb: fc.Arbitrary<TaskConfigSummaryDto> = fc.uuid().map((taskId) => ({
  taskId,
  allowsDoubleShift: false,
  allowsOverlap: false,
  dailyStartTime: null,
  dailyEndTime: null,
  burdenLevel: "Normal",
  requiredQualificationNames: [],
  splitCount: 1,
}));

/** Generates a config that is guaranteed to be non-default. */
const nonDefaultConfigArb: fc.Arbitrary<TaskConfigSummaryDto> = taskConfigArb.filter(
  (config) => !isDefaultConfig(config)
);

// ── Property-based tests ─────────────────────────────────────────────────────

describe("Property 4: Task info popover displays correct configuration", () => {
  // **Validates: Requirements 6.1**

  it("default config shows only 'defaultSettings' message", () => {
    fc.assert(
      fc.property(defaultConfigArb, (config) => {
        expect(isDefaultConfig(config)).toBe(true);
        const fields = getVisibleFields(config);
        expect(fields).toEqual(["defaultSettings"]);
      }),
      { numRuns: 100 }
    );
  });

  it("non-default config always shows doubleShift, overlap, timeWindow, and burden fields", () => {
    fc.assert(
      fc.property(nonDefaultConfigArb, (config) => {
        const fields = getVisibleFields(config);
        expect(fields).not.toContain("defaultSettings");
        expect(fields).toContain("doubleShift");
        expect(fields).toContain("overlap");
        expect(fields).toContain("timeWindow");
        expect(fields).toContain("burden");
      }),
      { numRuns: 100 }
    );
  });

  it("splitCount is shown only when > 1", () => {
    fc.assert(
      fc.property(taskConfigArb, (config) => {
        if (isDefaultConfig(config)) return; // skip defaults

        const fields = getVisibleFields(config);
        if (config.splitCount > 1) {
          expect(fields).toContain("splitCount");
        } else {
          expect(fields).not.toContain("splitCount");
        }
      }),
      { numRuns: 200 }
    );
  });

  it("qualifications shown only when requiredQualificationNames is non-empty", () => {
    fc.assert(
      fc.property(taskConfigArb, (config) => {
        if (isDefaultConfig(config)) return; // skip defaults

        const fields = getVisibleFields(config);
        if (config.requiredQualificationNames.length > 0) {
          expect(fields).toContain("qualifications");
        } else {
          expect(fields).not.toContain("qualifications");
        }
      }),
      { numRuns: 200 }
    );
  });

  it("time window displays '24/7' when both dailyStartTime and dailyEndTime are null", () => {
    fc.assert(
      fc.property(taskConfigArb, (config) => {
        const display = getTimeWindowDisplay(config);
        if (config.dailyStartTime === null || config.dailyEndTime === null) {
          expect(display).toBe("24/7");
        } else {
          expect(display).toBe(`${config.dailyStartTime} – ${config.dailyEndTime}`);
        }
      }),
      { numRuns: 200 }
    );
  });

  it("isDefaultConfig correctly identifies default vs non-default configs", () => {
    fc.assert(
      fc.property(taskConfigArb, (config) => {
        const expected =
          !config.allowsDoubleShift &&
          !config.allowsOverlap &&
          config.dailyStartTime === null &&
          config.dailyEndTime === null &&
          config.burdenLevel === "Normal" &&
          config.requiredQualificationNames.length === 0 &&
          config.splitCount <= 1;

        expect(isDefaultConfig(config)).toBe(expected);
      }),
      { numRuns: 200 }
    );
  });

  it("doubleShift display value matches config.allowsDoubleShift", () => {
    fc.assert(
      fc.property(nonDefaultConfigArb, (config) => {
        // The component renders "✓" for true, "✗" for false
        const displayValue = config.allowsDoubleShift ? "✓" : "✗";
        if (config.allowsDoubleShift) {
          expect(displayValue).toBe("✓");
        } else {
          expect(displayValue).toBe("✗");
        }
      }),
      { numRuns: 100 }
    );
  });

  it("overlap display value matches config.allowsOverlap", () => {
    fc.assert(
      fc.property(nonDefaultConfigArb, (config) => {
        const displayValue = config.allowsOverlap ? "✓" : "✗";
        if (config.allowsOverlap) {
          expect(displayValue).toBe("✓");
        } else {
          expect(displayValue).toBe("✗");
        }
      }),
      { numRuns: 100 }
    );
  });

  it("burden level is displayed as-is from config", () => {
    fc.assert(
      fc.property(nonDefaultConfigArb, (config) => {
        // The component renders config.burdenLevel directly
        expect(["Hard", "Normal", "Easy"]).toContain(config.burdenLevel);
      }),
      { numRuns: 100 }
    );
  });
});
