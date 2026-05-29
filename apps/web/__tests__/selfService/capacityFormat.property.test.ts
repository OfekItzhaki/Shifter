/**
 * Property-based tests for the capacity indicator format utility.
 *
 * Feature: shift-picker-lite
 * Task: 11.2 Write property test for capacity indicator format
 *
 * **Validates: Requirements 4.2**
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";
import { formatCapacity } from "../../lib/utils/pickCapacityFormat";

describe("Property 6: Capacity indicator format", () => {
  it('for any slot with currentFillCount and capacity values, the capacity indicator renders as "{currentFillCount}/{capacity}"', () => {
    fc.assert(
      fc.property(
        fc.nat(), // currentFillCount: non-negative integer
        fc.integer({ min: 1 }), // capacity: positive integer
        (currentFillCount, capacity) => {
          const result = formatCapacity(currentFillCount, capacity);
          expect(result).toBe(`${currentFillCount}/${capacity}`);
        }
      ),
      { numRuns: 100 }
    );
  });
});
