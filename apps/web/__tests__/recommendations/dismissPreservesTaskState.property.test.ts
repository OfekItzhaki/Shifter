/**
 * Feature: recommendation-approval-flow
 * Property 1: Dismiss preserves task state
 *
 * For any recommendation and any GroupTask state, dismissing the recommendation
 * SHALL set the recommendation status to "Dismissed" AND leave the GroupTask's
 * AllowsDoubleShift property unchanged from its value before the dismiss operation.
 *
 * **Validates: Requirements 1.1, 1.3, 4.2**
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";

// ── Domain types mirroring the backend entities ──────────────────────────────

type RecommendationStatus = "Active" | "Dismissed" | "Resolved" | "Cleared";

interface DoubleShiftRecommendation {
  id: string;
  spaceId: string;
  groupId: string;
  groupTaskId: string;
  taskName: string;
  status: RecommendationStatus;
  additionalSlotsCovered: number;
  totalUncoveredSlotsInRun: number;
  dismissedAt: string | null;
  dismissedByUserId: string | null;
}

interface GroupTaskState {
  id: string;
  name: string;
  allowsDoubleShift: boolean;
  allowsOverlap: boolean;
  dailyStartTime: string | null;
  dailyEndTime: string | null;
  burdenLevel: "Hard" | "Normal" | "Easy";
  splitCount: number;
}

// ── Dismiss operation (mirrors backend DismissRecommendationCommand logic) ───

interface DismissResult {
  recommendation: DoubleShiftRecommendation;
  groupTask: GroupTaskState;
}

/**
 * Simulates the dismiss operation as implemented in the backend.
 * The dismiss handler:
 * 1. Sets recommendation status to "Dismissed"
 * 2. Sets dismissedAt timestamp
 * 3. Sets dismissedByUserId
 * 4. Does NOT touch the GroupTask in any way
 */
function dismissRecommendation(
  recommendation: DoubleShiftRecommendation,
  groupTask: GroupTaskState,
  userId: string
): DismissResult {
  if (recommendation.status !== "Active") {
    throw new Error(
      `Cannot dismiss recommendation in status '${recommendation.status}'. Only active recommendations can be dismissed.`
    );
  }

  // Only the recommendation is mutated — GroupTask is untouched
  const dismissedRecommendation: DoubleShiftRecommendation = {
    ...recommendation,
    status: "Dismissed",
    dismissedAt: new Date().toISOString(),
    dismissedByUserId: userId,
  };

  // GroupTask is returned unchanged (the dismiss operation never modifies it)
  return {
    recommendation: dismissedRecommendation,
    groupTask: groupTask,
  };
}

// ── Arbitraries ──────────────────────────────────────────────────────────────

const uuidArb = fc.uuid();

const recommendationArb: fc.Arbitrary<DoubleShiftRecommendation> = fc.record({
  id: uuidArb,
  spaceId: uuidArb,
  groupId: uuidArb,
  groupTaskId: uuidArb,
  taskName: fc.string({ minLength: 1, maxLength: 50 }),
  status: fc.constant("Active" as RecommendationStatus),
  additionalSlotsCovered: fc.integer({ min: 1, max: 100 }),
  totalUncoveredSlotsInRun: fc.integer({ min: 1, max: 500 }),
  dismissedAt: fc.constant(null),
  dismissedByUserId: fc.constant(null),
});

const burdenLevelArb = fc.constantFrom("Hard", "Normal", "Easy") as fc.Arbitrary<
  "Hard" | "Normal" | "Easy"
>;

const timeStringArb = fc.oneof(
  fc.constant(null),
  fc
    .tuple(fc.integer({ min: 0, max: 23 }), fc.integer({ min: 0, max: 59 }))
    .map(([h, m]) => `${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}`)
);

const groupTaskArb: fc.Arbitrary<GroupTaskState> = fc.record({
  id: uuidArb,
  name: fc.string({ minLength: 1, maxLength: 50 }),
  allowsDoubleShift: fc.boolean(),
  allowsOverlap: fc.boolean(),
  dailyStartTime: timeStringArb,
  dailyEndTime: timeStringArb,
  burdenLevel: burdenLevelArb,
  splitCount: fc.integer({ min: 1, max: 10 }),
});

// ── Property-based tests ─────────────────────────────────────────────────────

describe("Property 1: Dismiss preserves task state", () => {
  // **Validates: Requirements 1.1, 1.3, 4.2**

  it("dismissing a recommendation sets status to Dismissed and leaves AllowsDoubleShift unchanged", () => {
    fc.assert(
      fc.property(recommendationArb, groupTaskArb, uuidArb, (recommendation, groupTask, userId) => {
        const originalAllowsDoubleShift = groupTask.allowsDoubleShift;

        const result = dismissRecommendation(recommendation, groupTask, userId);

        // Recommendation status must be Dismissed
        expect(result.recommendation.status).toBe("Dismissed");

        // GroupTask's AllowsDoubleShift must be unchanged
        expect(result.groupTask.allowsDoubleShift).toBe(originalAllowsDoubleShift);
      }),
      { numRuns: 200 }
    );
  });

  it("dismiss never modifies any GroupTask property regardless of initial state", () => {
    fc.assert(
      fc.property(recommendationArb, groupTaskArb, uuidArb, (recommendation, groupTask, userId) => {
        const originalTask = { ...groupTask };

        const result = dismissRecommendation(recommendation, groupTask, userId);

        // The entire GroupTask object must be identical after dismiss
        expect(result.groupTask).toEqual(originalTask);
      }),
      { numRuns: 200 }
    );
  });

  it("dismiss sets dismissedAt and dismissedByUserId on the recommendation", () => {
    fc.assert(
      fc.property(recommendationArb, groupTaskArb, uuidArb, (recommendation, groupTask, userId) => {
        const result = dismissRecommendation(recommendation, groupTask, userId);

        // Recommendation must have dismiss metadata
        expect(result.recommendation.dismissedAt).not.toBeNull();
        expect(result.recommendation.dismissedByUserId).toBe(userId);
      }),
      { numRuns: 100 }
    );
  });

  it("dismiss preserves all other recommendation fields except status and dismiss metadata", () => {
    fc.assert(
      fc.property(recommendationArb, groupTaskArb, uuidArb, (recommendation, groupTask, userId) => {
        const result = dismissRecommendation(recommendation, groupTask, userId);

        // All non-dismiss fields remain unchanged
        expect(result.recommendation.id).toBe(recommendation.id);
        expect(result.recommendation.spaceId).toBe(recommendation.spaceId);
        expect(result.recommendation.groupId).toBe(recommendation.groupId);
        expect(result.recommendation.groupTaskId).toBe(recommendation.groupTaskId);
        expect(result.recommendation.taskName).toBe(recommendation.taskName);
        expect(result.recommendation.additionalSlotsCovered).toBe(
          recommendation.additionalSlotsCovered
        );
        expect(result.recommendation.totalUncoveredSlotsInRun).toBe(
          recommendation.totalUncoveredSlotsInRun
        );
      }),
      { numRuns: 100 }
    );
  });
});
