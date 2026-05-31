/**
 * Property-based tests for rest duration utilities.
 *
 * Feature: rest-duration-display
 *
 * Uses fast-check to verify correctness properties hold across
 * all valid inputs for the pure computation functions.
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";
import {
  computeRestDurations,
  formatRestDuration,
  getRestColorClass,
  type RestDurationInput,
  type SupportedLocale,
} from "../lib/utils/restDuration";

// ---------------------------------------------------------------------------
// Arbitraries
// ---------------------------------------------------------------------------

/**
 * Generates a random UTC ISO datetime string within a reasonable range.
 */
const utcDatetimeArb = fc
  .date({
    min: new Date("2020-01-01T00:00:00Z"),
    max: new Date("2030-12-31T23:59:59Z"),
  })
  .map((d) => d.toISOString());

/**
 * Generates a random personId from a small pool to ensure grouping occurs.
 */
const personIdArb = fc.constantFrom(
  "person-1",
  "person-2",
  "person-3",
  "person-4",
  "person-5"
);

/**
 * Generates a single valid assignment with a start time and an end time
 * that is 1–12 hours after the start.
 */
const assignmentArb = fc
  .tuple(
    personIdArb,
    fc.date({
      min: new Date("2020-01-01T00:00:00Z"),
      max: new Date("2030-06-01T00:00:00Z"),
    }),
    fc.integer({ min: 1, max: 12 }) // duration in hours
  )
  .map(([personId, startDate, durationHours]): RestDurationInput => {
    const endDate = new Date(startDate.getTime() + durationHours * 3_600_000);
    return {
      personId,
      slotStartsAt: startDate.toISOString(),
      slotEndsAt: endDate.toISOString(),
    };
  });

/**
 * Generates an array of assignments (2–20 items).
 */
const assignmentsArb = fc.array(assignmentArb, { minLength: 2, maxLength: 20 });

/**
 * Generates a supported locale.
 */
const localeArb: fc.Arbitrary<SupportedLocale> = fc.constantFrom("en", "he", "ru");

/**
 * Generates a positive number of hours (0 to 200, with fractional part).
 */
const positiveHoursArb = fc.float({
  min: Math.fround(0),
  max: Math.fround(200),
  noNaN: true,
});

/**
 * Generates a strictly positive threshold value.
 */
const positiveThresholdArb = fc.float({
  min: Math.fround(0.01),
  max: Math.fround(100),
  noNaN: true,
});

// ---------------------------------------------------------------------------
// Property 1: Rest gap computation correctness
// ---------------------------------------------------------------------------

describe("Feature: rest-duration-display, Property 1: Rest gap computation correctness", () => {
  /**
   * **Validates: Requirements 1.1, 1.2, 5.1, 5.2, 5.3**
   *
   * For any set of assignments, the computed rest duration between each
   * consecutive pair equals (next.slotStartsAt - current.slotEndsAt) / 3_600_000,
   * clamped to 0, and results are ordered chronologically per person.
   */
  it("computed gaps equal (next.slotStartsAt - current.slotEndsAt) / 3_600_000 for sorted pairs", () => {
    fc.assert(
      fc.property(assignmentsArb, (assignments) => {
        const results = computeRestDurations(assignments);

        // Group assignments by personId and sort chronologically (same as the utility does)
        const grouped = new Map<string, RestDurationInput[]>();
        for (const a of assignments) {
          if (!a.personId || !a.slotStartsAt || !a.slotEndsAt) continue;
          const startMs = new Date(a.slotStartsAt).getTime();
          const endMs = new Date(a.slotEndsAt).getTime();
          if (isNaN(startMs) || isNaN(endMs)) continue;

          const group = grouped.get(a.personId) ?? [];
          group.push(a);
          grouped.set(a.personId, group);
        }

        for (const [personId, personAssignments] of grouped) {
          personAssignments.sort((a, b) =>
            a.slotStartsAt.localeCompare(b.slotStartsAt)
          );

          // Get results for this person
          const personResults = results.filter((r) => r.personId === personId);

          // Should have N-1 entries
          expect(personResults.length).toBe(personAssignments.length - 1);

          // Verify each gap
          for (let i = 0; i < personAssignments.length - 1; i++) {
            const current = personAssignments[i];
            const next = personAssignments[i + 1];

            const expectedGapMs =
              new Date(next.slotStartsAt).getTime() -
              new Date(current.slotEndsAt).getTime();
            const expectedRestHours = Math.max(expectedGapMs / 3_600_000, 0);

            expect(personResults[i].restHours).toBeCloseTo(expectedRestHours, 10);
            expect(personResults[i].slotStartsAt).toBe(current.slotStartsAt);
            expect(personResults[i].slotEndsAt).toBe(current.slotEndsAt);
            expect(personResults[i].nextSlotStartsAt).toBe(next.slotStartsAt);
          }
        }
      }),
      { numRuns: 100 }
    );
  });

  it("results are ordered chronologically per person", () => {
    fc.assert(
      fc.property(assignmentsArb, (assignments) => {
        const results = computeRestDurations(assignments);

        // Group results by personId
        const grouped = new Map<string, typeof results>();
        for (const r of results) {
          const group = grouped.get(r.personId) ?? [];
          group.push(r);
          grouped.set(r.personId, group);
        }

        // Verify chronological order within each person's results
        for (const [_personId, personResults] of grouped) {
          for (let i = 1; i < personResults.length; i++) {
            const prevStart = new Date(personResults[i - 1].slotStartsAt).getTime();
            const currStart = new Date(personResults[i].slotStartsAt).getTime();
            expect(currStart).toBeGreaterThanOrEqual(prevStart);
          }
        }
      }),
      { numRuns: 100 }
    );
  });
});

// ---------------------------------------------------------------------------
// Property 2: Duration formatting correctness
// ---------------------------------------------------------------------------

describe("Feature: rest-duration-display, Property 2: Duration formatting correctness", () => {
  /**
   * **Validates: Requirements 1.4, 6.3**
   *
   * For any positive hour value and any supported locale, the formatted string
   * uses "Xd Yh" pattern when hours >= 24, and "Xh" pattern when < 24.
   */
  it('uses "Xd Yh" pattern when hours >= 24 with correct X and Y values', () => {
    const hoursGte24 = fc.float({ min: Math.fround(24), max: Math.fround(500), noNaN: true });

    fc.assert(
      fc.property(hoursGte24, localeArb, (hours, locale) => {
        const result = formatRestDuration(hours, locale);

        const expectedDays = Math.floor(hours / 24);
        const expectedRemainingHours = Math.floor(hours % 24);

        const localeMap: Record<SupportedLocale, { h: string; d: string }> = {
          en: { h: "h", d: "d" },
          he: { h: "ש", d: "י" },
          ru: { h: "ч", d: "д" },
        };

        const { h, d } = localeMap[locale];
        const expected = `${expectedDays}${d} ${expectedRemainingHours}${h}`;
        expect(result).toBe(expected);
      }),
      { numRuns: 100 }
    );
  });

  it('uses "Xh" pattern when hours < 24', () => {
    const hoursLt24 = fc.float({ min: Math.fround(0), max: Math.fround(23.999), noNaN: true });

    fc.assert(
      fc.property(hoursLt24, localeArb, (hours, locale) => {
        const result = formatRestDuration(hours, locale);

        const expectedHours = Math.floor(hours);

        const localeMap: Record<SupportedLocale, { h: string }> = {
          en: { h: "h" },
          he: { h: "ש" },
          ru: { h: "ч" },
        };

        const { h } = localeMap[locale];
        const expected = `${expectedHours}${h}`;
        expect(result).toBe(expected);
      }),
      { numRuns: 100 }
    );
  });
});

// ---------------------------------------------------------------------------
// Property 3: Color classification correctness
// ---------------------------------------------------------------------------

describe("Feature: rest-duration-display, Property 3: Color classification correctness", () => {
  /**
   * **Validates: Requirements 4.1, 4.2, 4.3**
   *
   * For any (restHours, threshold) pair with positive threshold:
   * - text-red-600 when rest < threshold
   * - text-amber-600 when rest === threshold
   * - text-slate-500 when rest > threshold
   */
  it("returns text-red-600 when restHours < threshold", () => {
    fc.assert(
      fc.property(positiveThresholdArb, (threshold) => {
        // Generate a rest value strictly less than threshold
        const restHours = threshold * 0.5; // always less than threshold since threshold > 0
        expect(getRestColorClass(restHours, threshold)).toBe("text-red-600");
      }),
      { numRuns: 100 }
    );
  });

  it("returns text-amber-600 when restHours === threshold", () => {
    fc.assert(
      fc.property(positiveThresholdArb, (threshold) => {
        expect(getRestColorClass(threshold, threshold)).toBe("text-amber-600");
      }),
      { numRuns: 100 }
    );
  });

  it("returns text-slate-500 when restHours > threshold", () => {
    fc.assert(
      fc.property(positiveThresholdArb, (threshold) => {
        // Generate a rest value strictly greater than threshold
        const restHours = threshold + 1;
        expect(getRestColorClass(restHours, threshold)).toBe("text-slate-500");
      }),
      { numRuns: 100 }
    );
  });

  it("correctly classifies any random (restHours, threshold) pair", () => {
    fc.assert(
      fc.property(
        positiveHoursArb,
        positiveThresholdArb,
        (restHours, threshold) => {
          const result = getRestColorClass(restHours, threshold);

          if (restHours < threshold) {
            expect(result).toBe("text-red-600");
          } else if (restHours === threshold) {
            expect(result).toBe("text-amber-600");
          } else {
            expect(result).toBe("text-slate-500");
          }
        }
      ),
      { numRuns: 100 }
    );
  });
});

// ---------------------------------------------------------------------------
// Property 4: No rest entry for terminal assignments
// ---------------------------------------------------------------------------

describe("Feature: rest-duration-display, Property 4: No rest entry for terminal assignments", () => {
  /**
   * **Validates: Requirements 1.5**
   *
   * A person with 1 assignment produces 0 rest entries.
   * A person with N assignments produces exactly N-1 rest entries.
   */
  it("a person with exactly 1 assignment produces 0 rest entries", () => {
    fc.assert(
      fc.property(assignmentArb, (assignment) => {
        const results = computeRestDurations([assignment]);
        const personResults = results.filter(
          (r) => r.personId === assignment.personId
        );
        expect(personResults.length).toBe(0);
      }),
      { numRuns: 100 }
    );
  });

  it("a person with N assignments produces exactly N-1 rest entries", () => {
    // Generate a specific person with 2-10 assignments
    const personAssignmentsArb = fc
      .tuple(
        personIdArb,
        fc.array(
          fc.tuple(
            fc.date({
              min: new Date("2020-01-01T00:00:00Z"),
              max: new Date("2030-06-01T00:00:00Z"),
            }),
            fc.integer({ min: 1, max: 8 })
          ),
          { minLength: 2, maxLength: 10 }
        )
      )
      .map(([personId, dateAndDurations]) => {
        return dateAndDurations.map(
          ([startDate, durationHours]): RestDurationInput => {
            const endDate = new Date(
              startDate.getTime() + durationHours * 3_600_000
            );
            return {
              personId,
              slotStartsAt: startDate.toISOString(),
              slotEndsAt: endDate.toISOString(),
            };
          }
        );
      });

    fc.assert(
      fc.property(personAssignmentsArb, (assignments) => {
        const n = assignments.length;
        const results = computeRestDurations(assignments);
        // All assignments belong to the same person
        const personId = assignments[0].personId;
        const personResults = results.filter((r) => r.personId === personId);
        expect(personResults.length).toBe(n - 1);
      }),
      { numRuns: 100 }
    );
  });

  it("multiple persons each produce N-1 rest entries independently", () => {
    // Generate assignments for multiple distinct persons
    const multiPersonArb = fc
      .tuple(
        fc.array(assignmentArb, { minLength: 1, maxLength: 5 }).map((arr) =>
          arr.map((a) => ({ ...a, personId: "alpha" }))
        ),
        fc.array(assignmentArb, { minLength: 1, maxLength: 5 }).map((arr) =>
          arr.map((a) => ({ ...a, personId: "beta" }))
        )
      )
      .map(([alphaAssignments, betaAssignments]) => ({
        alpha: alphaAssignments,
        beta: betaAssignments,
        all: [...alphaAssignments, ...betaAssignments],
      }));

    fc.assert(
      fc.property(multiPersonArb, ({ alpha, beta, all }) => {
        const results = computeRestDurations(all);

        const alphaResults = results.filter((r) => r.personId === "alpha");
        const betaResults = results.filter((r) => r.personId === "beta");

        expect(alphaResults.length).toBe(alpha.length - 1);
        expect(betaResults.length).toBe(beta.length - 1);
      }),
      { numRuns: 100 }
    );
  });
});
