/**
 * Property-based tests for TaskInfoBadge presence and accessibility.
 *
 * Feature: recommendation-approval-flow
 * Property 3: Task info badge presence and accessibility
 *
 * For any set of tasks displayed in the ScheduleTable2D with available
 * configuration data, each task column header SHALL contain exactly one
 * TaskInfoBadge element with an `aria-label` attribute describing its purpose.
 *
 * **Validates: Requirements 5.1, 5.3**
 */

import { describe, it, expect, vi } from "vitest";
import * as fc from "fast-check";
import { render, screen } from "@testing-library/react";
import React from "react";
import TaskInfoBadge from "../../components/schedule/TaskInfoBadge";
import type { TaskConfigSummaryDto } from "@/lib/api/groups";

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      "taskInfo.doubleShift": "Double Shift",
      "taskInfo.overlap": "Overlap",
      "taskInfo.timeWindow": "Time Window",
      "taskInfo.allDay": "24/7",
      "taskInfo.burden": "Burden",
      "taskInfo.qualifications": "Qualifications",
      "taskInfo.splitCount": "Split Count",
      "taskInfo.defaultSettings": "Default settings",
    };
    return translations[key] ?? key;
  },
}));

// ── Arbitraries ───────────────────────────────────────────────────────────────

/** Arbitrary for a valid HH:mm time string */
const timeStringArb = fc
  .tuple(fc.integer({ min: 0, max: 23 }), fc.integer({ min: 0, max: 59 }))
  .map(([h, m]) => `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}`);

/** Arbitrary for burden level */
const burdenLevelArb = fc.constantFrom("Hard", "Normal", "Easy");

/** Arbitrary for a TaskConfigSummaryDto */
const taskConfigArb: fc.Arbitrary<TaskConfigSummaryDto> = fc.record({
  taskId: fc.uuid(),
  allowsDoubleShift: fc.boolean(),
  allowsOverlap: fc.boolean(),
  dailyStartTime: fc.oneof(fc.constant(null), timeStringArb),
  dailyEndTime: fc.oneof(fc.constant(null), timeStringArb),
  burdenLevel: burdenLevelArb,
  requiredQualificationNames: fc.array(fc.string({ minLength: 1, maxLength: 20 }), {
    minLength: 0,
    maxLength: 5,
  }),
  splitCount: fc.integer({ min: 1, max: 10 }),
});

// ── Property Tests ────────────────────────────────────────────────────────────

describe("Property 3: Task info badge presence and accessibility", () => {
  // **Validates: Requirements 5.1, 5.3**

  it("renders exactly one button with correct aria-label when config is provided", () => {
    fc.assert(
      fc.property(taskConfigArb, (config) => {
        const { container, unmount } = render(<TaskInfoBadge config={config} />);

        // Should render exactly one button element
        const buttons = container.querySelectorAll("button");
        expect(buttons).toHaveLength(1);

        // The button must have the correct aria-label for accessibility
        const button = buttons[0];
        expect(button.getAttribute("aria-label")).toBe("Task configuration info");

        unmount();
      }),
      { numRuns: 150 }
    );
  });

  it("renders nothing when config is null", () => {
    fc.assert(
      fc.property(fc.constant(null), (config) => {
        const { container, unmount } = render(<TaskInfoBadge config={config} />);

        // Should render no button elements
        const buttons = container.querySelectorAll("button");
        expect(buttons).toHaveLength(0);

        // Container should be empty
        expect(container.innerHTML).toBe("");

        unmount();
      }),
      { numRuns: 100 }
    );
  });

  it("renders nothing when config is undefined", () => {
    fc.assert(
      fc.property(fc.constant(undefined), (config) => {
        const { container, unmount } = render(<TaskInfoBadge config={config} />);

        // Should render no button elements
        const buttons = container.querySelectorAll("button");
        expect(buttons).toHaveLength(0);

        // Container should be empty
        expect(container.innerHTML).toBe("");

        unmount();
      }),
      { numRuns: 100 }
    );
  });

  it("badge aria-label is always 'Task configuration info' regardless of config content", () => {
    fc.assert(
      fc.property(taskConfigArb, (config) => {
        const { container, unmount } = render(<TaskInfoBadge config={config} />);

        const button = container.querySelector("button");
        expect(button).not.toBeNull();

        // The aria-label must be consistent regardless of config values
        expect(button!.getAttribute("aria-label")).toBe("Task configuration info");

        // The button must be of type="button" (not submit)
        expect(button!.getAttribute("type")).toBe("button");

        unmount();
      }),
      { numRuns: 150 }
    );
  });
});
