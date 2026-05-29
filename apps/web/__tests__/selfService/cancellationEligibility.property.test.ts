/**
 * Property-based tests for cancellation eligibility.
 *
 * Feature: shift-picker-lite
 * Task: 11.3 Write property test for cancellation eligibility
 *
 * **Validates: Requirements 5.4**
 *
 * Property 7: Cancellation eligibility is time-based
 * For any approved shift request and cancellation cutoff hours value,
 * the cancel button is visible if and only if
 * shiftStartTime - currentTime > cutoffHours * 3600000.
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";
import { isCancellable } from "../../lib/utils/pickCancellationEligibility";

/**
 * Arbitrary that generates a cutoff hours value in a reasonable range (1-72).
 */
const cutoffHoursArb = fc.integer({ min: 1, max: 72 });

/**
 * Arbitrary that generates a current time as a Date within a reasonable range.
 */
const currentTimeArb = fc.date({
  min: new Date("2024-01-01T00:00:00Z"),
  max: new Date("2026-12-31T23:59:59Z"),
});

/**
 * Arbitrary that generates a positive offset in milliseconds (1 minute to 30 days).
 * Used to create a shift start time relative to current time.
 */
const futureOffsetMsArb = fc.integer({ min: 60_000, max: 30 * 24 * 3600_000 });

describe("Property 7: Cancellation eligibility is time-based", () => {
  it("isCancellable returns true iff shiftStartTime - currentTime > cutoffHours * 3600000", () => {
    fc.assert(
      fc.property(
        currentTimeArb,
        futureOffsetMsArb,
        cutoffHoursArb,
        (currentTime, offsetMs, cutoffHours) => {
          const shiftStartTime = new Date(
            currentTime.getTime() + offsetMs
          );

          const result = isCancellable(
            shiftStartTime,
            currentTime,
            cutoffHours
          );

          const timeDiff = shiftStartTime.getTime() - currentTime.getTime();
          const cutoffMs = cutoffHours * 3600000;
          const expected = timeDiff > cutoffMs;

          expect(result).toBe(expected);
        }
      ),
      { numRuns: 100 }
    );
  });

  it("isCancellable returns false when time remaining equals exactly the cutoff (boundary)", () => {
    fc.assert(
      fc.property(
        currentTimeArb,
        cutoffHoursArb,
        (currentTime, cutoffHours) => {
          // Exactly at the cutoff boundary — should return false (not strictly greater)
          const cutoffMs = cutoffHours * 3600000;
          const shiftStartTime = new Date(
            currentTime.getTime() + cutoffMs
          );

          const result = isCancellable(shiftStartTime, currentTime, cutoffHours);
          expect(result).toBe(false);
        }
      ),
      { numRuns: 100 }
    );
  });

  it("isCancellable accepts string dates and produces the same result as Date objects", () => {
    fc.assert(
      fc.property(
        currentTimeArb,
        futureOffsetMsArb,
        cutoffHoursArb,
        (currentTime, offsetMs, cutoffHours) => {
          const shiftStartTime = new Date(
            currentTime.getTime() + offsetMs
          );

          const resultWithDate = isCancellable(
            shiftStartTime,
            currentTime,
            cutoffHours
          );
          const resultWithString = isCancellable(
            shiftStartTime.toISOString(),
            currentTime,
            cutoffHours
          );

          expect(resultWithString).toBe(resultWithDate);
        }
      ),
      { numRuns: 100 }
    );
  });
});
