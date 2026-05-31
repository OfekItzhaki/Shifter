/**
 * Unit tests for rest duration utilities.
 *
 * Feature: rest-duration-display
 * Task: 3.4 Write unit tests for RestDurationBadge and ScheduleTaskTable integration
 *
 * **Validates: Requirements 1.4, 1.5, 2.1, 2.2, 4.1, 4.2, 4.3**
 */

import { describe, it, expect } from "vitest";
import {
  computeRestDurations,
  formatRestDuration,
  getRestColorClass,
  type RestDurationInput,
} from "../lib/utils/restDuration";

// ---------------------------------------------------------------------------
// formatRestDuration
// ---------------------------------------------------------------------------

describe("formatRestDuration", () => {
  describe("specific formatting examples", () => {
    it('formats 25.5h as "1d 1h" in English', () => {
      expect(formatRestDuration(25.5, "en")).toBe("1d 1h");
    });

    it('formats 8h as "8h" in English', () => {
      expect(formatRestDuration(8, "en")).toBe("8h");
    });

    it('formats 0h as "0h" in English', () => {
      expect(formatRestDuration(0, "en")).toBe("0h");
    });

    it('formats exactly 24h as "1d 0h" in English', () => {
      expect(formatRestDuration(24, "en")).toBe("1d 0h");
    });

    it('formats 48h as "2d 0h" in English', () => {
      expect(formatRestDuration(48, "en")).toBe("2d 0h");
    });

    it('formats 47.9h as "1d 23h" in English', () => {
      expect(formatRestDuration(47.9, "en")).toBe("1d 23h");
    });
  });

  describe("locale-specific output", () => {
    it("formats in Hebrew (he) with ש for hours and י for days", () => {
      expect(formatRestDuration(8, "he")).toBe("8ש");
      expect(formatRestDuration(25, "he")).toBe("1י 1ש");
    });

    it("formats in English (en) with h for hours and d for days", () => {
      expect(formatRestDuration(8, "en")).toBe("8h");
      expect(formatRestDuration(25, "en")).toBe("1d 1h");
    });

    it("formats in Russian (ru) with ч for hours and д for days", () => {
      expect(formatRestDuration(8, "ru")).toBe("8ч");
      expect(formatRestDuration(25, "ru")).toBe("1д 1ч");
    });
  });
});

// ---------------------------------------------------------------------------
// getRestColorClass
// ---------------------------------------------------------------------------

describe("getRestColorClass", () => {
  it("returns text-red-600 when restHours is below threshold", () => {
    expect(getRestColorClass(6, 8)).toBe("text-red-600");
    expect(getRestColorClass(0, 8)).toBe("text-red-600");
    expect(getRestColorClass(7.99, 8)).toBe("text-red-600");
  });

  it("returns text-amber-600 when restHours equals threshold exactly", () => {
    expect(getRestColorClass(8, 8)).toBe("text-amber-600");
    expect(getRestColorClass(12, 12)).toBe("text-amber-600");
    expect(getRestColorClass(0, 0)).toBe("text-amber-600");
  });

  it("returns text-slate-500 when restHours is above threshold", () => {
    expect(getRestColorClass(9, 8)).toBe("text-slate-500");
    expect(getRestColorClass(24, 8)).toBe("text-slate-500");
    expect(getRestColorClass(8.01, 8)).toBe("text-slate-500");
  });
});

// ---------------------------------------------------------------------------
// computeRestDurations
// ---------------------------------------------------------------------------

describe("computeRestDurations", () => {
  it("person with single assignment produces no rest entry", () => {
    const assignments: RestDurationInput[] = [
      {
        personId: "person-1",
        slotStartsAt: "2024-01-15T08:00:00.000Z",
        slotEndsAt: "2024-01-15T16:00:00.000Z",
      },
    ];

    const results = computeRestDurations(assignments);
    expect(results.length).toBe(0);
  });

  it("overlapping assignments (negative gap) produce restHours clamped to 0", () => {
    const assignments: RestDurationInput[] = [
      {
        personId: "person-1",
        slotStartsAt: "2024-01-15T08:00:00.000Z",
        slotEndsAt: "2024-01-15T18:00:00.000Z", // ends at 18:00
      },
      {
        personId: "person-1",
        slotStartsAt: "2024-01-15T16:00:00.000Z", // starts at 16:00 (overlap!)
        slotEndsAt: "2024-01-15T22:00:00.000Z",
      },
    ];

    const results = computeRestDurations(assignments);
    expect(results.length).toBe(1);
    expect(results[0].restHours).toBe(0);
  });

  it("exactly 24h gap produces restHours of 24", () => {
    const assignments: RestDurationInput[] = [
      {
        personId: "person-1",
        slotStartsAt: "2024-01-15T08:00:00.000Z",
        slotEndsAt: "2024-01-15T16:00:00.000Z",
      },
      {
        personId: "person-1",
        slotStartsAt: "2024-01-16T16:00:00.000Z", // 24h after previous ends
        slotEndsAt: "2024-01-17T00:00:00.000Z",
      },
    ];

    const results = computeRestDurations(assignments);
    expect(results.length).toBe(1);
    expect(results[0].restHours).toBe(24);
    // Verify formatting shows "1d 0h"
    expect(formatRestDuration(results[0].restHours, "en")).toBe("1d 0h");
  });

  it("multiple persons across multiple tasks produce correct cross-task computation", () => {
    const assignments: RestDurationInput[] = [
      // Person 1 - Task A
      {
        personId: "person-1",
        slotStartsAt: "2024-01-15T06:00:00.000Z",
        slotEndsAt: "2024-01-15T14:00:00.000Z",
      },
      // Person 1 - Task B (different task, 4h gap)
      {
        personId: "person-1",
        slotStartsAt: "2024-01-15T18:00:00.000Z",
        slotEndsAt: "2024-01-16T02:00:00.000Z",
      },
      // Person 2 - Task A
      {
        personId: "person-2",
        slotStartsAt: "2024-01-15T08:00:00.000Z",
        slotEndsAt: "2024-01-15T16:00:00.000Z",
      },
      // Person 2 - Task C (10h gap)
      {
        personId: "person-2",
        slotStartsAt: "2024-01-16T02:00:00.000Z",
        slotEndsAt: "2024-01-16T10:00:00.000Z",
      },
      // Person 1 - Task C (another assignment, 6h gap from Task B end)
      {
        personId: "person-1",
        slotStartsAt: "2024-01-16T08:00:00.000Z",
        slotEndsAt: "2024-01-16T16:00:00.000Z",
      },
    ];

    const results = computeRestDurations(assignments);

    // Person 1 has 3 assignments → 2 rest entries
    const person1Results = results.filter((r) => r.personId === "person-1");
    expect(person1Results.length).toBe(2);

    // First gap: 18:00 - 14:00 = 4 hours
    expect(person1Results[0].restHours).toBe(4);
    // Second gap: 08:00 next day - 02:00 = 6 hours
    expect(person1Results[1].restHours).toBe(6);

    // Person 2 has 2 assignments → 1 rest entry
    const person2Results = results.filter((r) => r.personId === "person-2");
    expect(person2Results.length).toBe(1);

    // Gap: 02:00 next day - 16:00 = 10 hours
    expect(person2Results[0].restHours).toBe(10);
  });

  it("skips assignments with missing personId", () => {
    const assignments: RestDurationInput[] = [
      {
        personId: "",
        slotStartsAt: "2024-01-15T08:00:00.000Z",
        slotEndsAt: "2024-01-15T16:00:00.000Z",
      },
      {
        personId: "person-1",
        slotStartsAt: "2024-01-15T08:00:00.000Z",
        slotEndsAt: "2024-01-15T16:00:00.000Z",
      },
    ];

    const results = computeRestDurations(assignments);
    // Empty personId is skipped, person-1 has only 1 assignment → 0 entries
    expect(results.length).toBe(0);
  });

  it("skips assignments with invalid timestamps", () => {
    const assignments: RestDurationInput[] = [
      {
        personId: "person-1",
        slotStartsAt: "invalid-date",
        slotEndsAt: "2024-01-15T16:00:00.000Z",
      },
      {
        personId: "person-1",
        slotStartsAt: "2024-01-15T18:00:00.000Z",
        slotEndsAt: "2024-01-16T02:00:00.000Z",
      },
    ];

    const results = computeRestDurations(assignments);
    // First assignment is skipped, person-1 has only 1 valid assignment → 0 entries
    expect(results.length).toBe(0);
  });

  it("returns empty array for empty input", () => {
    expect(computeRestDurations([])).toEqual([]);
  });
});
