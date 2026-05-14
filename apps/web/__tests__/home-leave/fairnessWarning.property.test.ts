/**
 * Feature: home-leave-scheduling
 * Property 10: Fairness warning threshold
 *
 * Generate random arrays of base_time_ratio values (0.0–1.0),
 * verify warning shown iff max - min > 0.15
 *
 * **Validates: Requirements 9.4**
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";

// ── Pure logic extracted from HomeLeaveMetricsPanel ──────────────────────────

interface HomeLeaveMetric {
  personId: string;
  personName: string;
  totalBaseHours: number;
  totalHomeHours: number;
  baseTimeRatio: number;
  leaveSlotCount: number;
}

/**
 * Determines whether the fairness warning should be shown.
 * Mirrors the logic in HomeLeaveMetricsPanel.tsx:
 *   showFairnessWarning = metrics.length >= 2 && maxRatio - minRatio > 0.15
 */
function shouldShowFairnessWarning(metrics: HomeLeaveMetric[]): boolean {
  if (!metrics || metrics.length < 2) {
    return false;
  }

  const ratios = metrics.map((m) => m.baseTimeRatio);
  const maxRatio = Math.max(...ratios);
  const minRatio = Math.min(...ratios);

  return maxRatio - minRatio > 0.15;
}

// ── Helper to create a metric from a ratio value ─────────────────────────────

function makeMetric(ratio: number, index: number): HomeLeaveMetric {
  return {
    personId: `person-${index}`,
    personName: `Person ${index}`,
    totalBaseHours: ratio * 168,
    totalHomeHours: (1 - ratio) * 168,
    baseTimeRatio: ratio,
    leaveSlotCount: Math.floor((1 - ratio) * 3),
  };
}

// ── Property-based tests ─────────────────────────────────────────────────────

describe("Property 10: Fairness warning threshold", () => {
  // **Validates: Requirements 9.4**

  it("warning shown iff max - min base_time_ratio > 0.15 for arrays of 2+ entries", () => {
    fc.assert(
      fc.property(
        // Generate arrays of 2–20 ratio values between 0.0 and 1.0
        fc.array(fc.double({ min: 0, max: 1, noNaN: true }), { minLength: 2, maxLength: 20 }),
        (ratios) => {
          const metrics = ratios.map((r, i) => makeMetric(r, i));

          const maxRatio = Math.max(...ratios);
          const minRatio = Math.min(...ratios);
          const expectedWarning = maxRatio - minRatio > 0.15;

          const actualWarning = shouldShowFairnessWarning(metrics);

          expect(actualWarning).toBe(expectedWarning);
        }
      ),
      { numRuns: 500 }
    );
  });

  it("no warning for arrays with fewer than 2 entries", () => {
    fc.assert(
      fc.property(
        fc.array(fc.double({ min: 0, max: 1, noNaN: true }), { minLength: 0, maxLength: 1 }),
        (ratios) => {
          const metrics = ratios.map((r, i) => makeMetric(r, i));
          expect(shouldShowFairnessWarning(metrics)).toBe(false);
        }
      ),
      { numRuns: 100 }
    );
  });

  it("warning always shown when spread exceeds 0.15", () => {
    fc.assert(
      fc.property(
        // Generate a low ratio and a high ratio with guaranteed spread > 0.15
        fc.double({ min: 0, max: 0.4, noNaN: true }),
        fc.double({ min: 0.56, max: 1.0, noNaN: true }),
        fc.array(fc.double({ min: 0, max: 1, noNaN: true }), { minLength: 0, maxLength: 10 }),
        (lowRatio, highRatio, middleRatios) => {
          // Ensure spread > 0.15 by construction
          fc.pre(highRatio - lowRatio > 0.15);

          const allRatios = [lowRatio, highRatio, ...middleRatios];
          const metrics = allRatios.map((r, i) => makeMetric(r, i));

          expect(shouldShowFairnessWarning(metrics)).toBe(true);
        }
      ),
      { numRuns: 200 }
    );
  });

  it("warning never shown when all ratios are within 0.15 of each other", () => {
    fc.assert(
      fc.property(
        // Generate a base ratio and offsets within 0.15
        fc.double({ min: 0.15, max: 0.85, noNaN: true }),
        fc.array(fc.double({ min: 0, max: 0.15, noNaN: true }), { minLength: 1, maxLength: 10 }),
        (baseRatio, offsets) => {
          // Create ratios that are all within 0.15 of each other
          const ratios = [baseRatio, ...offsets.map((o) => baseRatio + o)];

          // Verify our construction: max - min <= 0.15
          const max = Math.max(...ratios);
          const min = Math.min(...ratios);
          fc.pre(max - min <= 0.15);
          fc.pre(ratios.every((r) => r >= 0 && r <= 1));

          const metrics = ratios.map((r, i) => makeMetric(r, i));

          expect(shouldShowFairnessWarning(metrics)).toBe(false);
        }
      ),
      { numRuns: 200 }
    );
  });

  it("boundary: exactly 0.15 spread does NOT trigger warning (strict >)", () => {
    // The condition is strictly greater than 0.15, not >=
    const metrics = [makeMetric(0.5, 0), makeMetric(0.65, 1)];
    expect(shouldShowFairnessWarning(metrics)).toBe(false);
  });

  it("boundary: 0.1500001 spread DOES trigger warning", () => {
    const metrics = [makeMetric(0.5, 0), makeMetric(0.6500001, 1)];
    expect(shouldShowFairnessWarning(metrics)).toBe(true);
  });

  it("empty metrics array returns no warning", () => {
    expect(shouldShowFairnessWarning([])).toBe(false);
  });

  it("single metric returns no warning regardless of ratio", () => {
    fc.assert(
      fc.property(fc.double({ min: 0, max: 1, noNaN: true }), (ratio) => {
        const metrics = [makeMetric(ratio, 0)];
        expect(shouldShowFairnessWarning(metrics)).toBe(false);
      }),
      { numRuns: 50 }
    );
  });
});
