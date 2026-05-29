/**
 * Property-based tests for slot sorting utility.
 *
 * Feature: shift-picker-lite
 * Task: 11.1 Write property test for slot sorting
 *
 * **Validates: Requirements 4.1**
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";
import { sortSlotsByDateTime } from "../../lib/utils/pickSlotSort";
import type { AvailableSlotDto } from "../../lib/api/selfService";

/**
 * Arbitrary that generates a valid date string in "YYYY-MM-DD" format.
 */
const dateArb = fc
  .record({
    year: fc.integer({ min: 2024, max: 2030 }),
    month: fc.integer({ min: 1, max: 12 }),
    day: fc.integer({ min: 1, max: 28 }), // Use 28 to avoid invalid dates
  })
  .map(
    ({ year, month, day }) =>
      `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`
  );

/**
 * Arbitrary that generates a valid time string in "HH:mm" format.
 */
const timeArb = fc
  .record({
    hour: fc.integer({ min: 0, max: 23 }),
    minute: fc.integer({ min: 0, max: 59 }),
  })
  .map(
    ({ hour, minute }) =>
      `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`
  );

/**
 * Arbitrary that generates a valid AvailableSlotDto with random date and time values.
 */
const slotArb: fc.Arbitrary<AvailableSlotDto> = fc.record({
  id: fc.uuid(),
  date: dateArb,
  startTime: timeArb,
  endTime: timeArb,
  taskName: fc.string({ minLength: 1, maxLength: 30 }),
  capacity: fc.integer({ min: 1, max: 20 }),
  currentFillCount: fc.integer({ min: 0, max: 20 }),
  schedulingCycleId: fc.uuid(),
});

/**
 * Arbitrary that generates a list of slots with random dates and start times.
 */
const slotListArb = fc.array(slotArb, { minLength: 0, maxLength: 50 });

describe("Property 5: Slot sorting is date-first then time-second", () => {
  it("sorted slots maintain date ascending then start time ascending ordering", () => {
    fc.assert(
      fc.property(slotListArb, (slots) => {
        const sorted = sortSlotsByDateTime(slots);

        // For every consecutive pair (a, b) in the sorted result:
        // either a.date < b.date, or (a.date === b.date and a.startTime <= b.startTime)
        for (let i = 0; i < sorted.length - 1; i++) {
          const a = sorted[i];
          const b = sorted[i + 1];

          if (a.date === b.date) {
            expect(a.startTime <= b.startTime).toBe(true);
          } else {
            expect(a.date < b.date).toBe(true);
          }
        }
      }),
      { numRuns: 100 }
    );
  });

  it("sorting does not add or remove slots", () => {
    fc.assert(
      fc.property(slotListArb, (slots) => {
        const sorted = sortSlotsByDateTime(slots);
        expect(sorted).toHaveLength(slots.length);
      }),
      { numRuns: 100 }
    );
  });

  it("sorting does not mutate the original array", () => {
    fc.assert(
      fc.property(slotListArb, (slots) => {
        const original = [...slots];
        sortSlotsByDateTime(slots);
        expect(slots).toEqual(original);
      }),
      { numRuns: 100 }
    );
  });
});
