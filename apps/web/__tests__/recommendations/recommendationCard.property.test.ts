/**
 * Feature: recommendation-approval-flow
 * Property 2: Recommendation card displays all task names and slot counts
 *
 * For any set of active recommendations with varying task names and uncovered
 * slot counts, the rendered RecommendationCard SHALL contain every task name
 * from the recommendation set and display the total uncovered slot count.
 *
 * **Validates: Requirements 2.2**
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import * as fc from "fast-check";
import { render, screen } from "@testing-library/react";
import React from "react";

import type { Recommendation } from "@/lib/api/recommendations";

// ─── Mocks ────────────────────────────────────────────────────────────────────

// Mock next-intl: return interpolated string so we can assert on task names and counts
vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, params?: Record<string, unknown>) => {
    if (key === "cardTitle") return "Recommendation";
    if (key === "cardDescription" && params) {
      return `Tasks: ${params.taskNames}, Uncovered: ${params.count}`;
    }
    if (key === "slotsCount" && params) {
      return `${params.count} uncovered slots`;
    }
    if (key === "goToTasks") return "Go to Tasks";
    if (key === "dismiss") return "Dismiss";
    return key;
  },
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// We'll control what useRecommendations returns per test via this variable
let mockRecommendations: Recommendation[] = [];

vi.mock("@/lib/query/hooks/useRecommendations", () => ({
  useRecommendations: () => ({
    data: mockRecommendations,
    isLoading: false,
  }),
  useDismissRecommendation: () => ({
    mutate: vi.fn(),
    isPending: false,
  }),
}));

// ─── Import component after mocks ────────────────────────────────────────────

import RecommendationCard from "@/components/recommendations/RecommendationCard";

// ─── Arbitrary generators ────────────────────────────────────────────────────

/**
 * Generate a non-empty task name (alphanumeric + spaces, 1-30 chars).
 * We use a restricted character set to avoid issues with regex special chars
 * when searching rendered text.
 */
const taskNameArb = fc.stringOf(
  fc.constantFrom(
    "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
    "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
    "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
    "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
    "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", " "
  ),
  { minLength: 1, maxLength: 30 }
).filter((s) => s.trim().length > 0);

/**
 * Generate a valid Recommendation object with arbitrary task name and slot counts.
 */
const recommendationArb = (taskName: string): fc.Arbitrary<Recommendation> =>
  fc.record({
    id: fc.uuid(),
    groupTaskId: fc.uuid(),
    taskName: fc.constant(taskName),
    status: fc.constant("Active" as const),
    additionalSlotsCovered: fc.integer({ min: 1, max: 50 }),
    affectedDateStart: fc.constant("2024-01-01"),
    affectedDateEnd: fc.constant("2024-01-07"),
    totalUncoveredSlotsInRun: fc.integer({ min: 1, max: 100 }),
    createdAt: fc.constant("2024-01-01T00:00:00Z"),
  });

/**
 * Generate an array of 1-10 recommendations with unique task names.
 */
const recommendationsArb = fc
  .uniqueArray(taskNameArb, { minLength: 1, maxLength: 10 })
  .chain((taskNames) =>
    fc.tuple(...taskNames.map((name) => recommendationArb(name)))
  );

// ─── Property-based tests ────────────────────────────────────────────────────

describe("Property 2: Recommendation card displays all task names and slot counts", () => {
  // **Validates: Requirements 2.2**

  beforeEach(() => {
    mockRecommendations = [];
  });

  it("for any set of active recommendations, the rendered card contains every task name", () => {
    fc.assert(
      fc.property(recommendationsArb, (recommendations) => {
        mockRecommendations = recommendations;

        const { container, unmount } = render(
          React.createElement(RecommendationCard, {
            spaceId: "space-1",
            groupId: "group-1",
          })
        );

        const textContent = container.textContent ?? "";

        // Every task name must appear in the rendered output
        for (const rec of recommendations) {
          expect(textContent).toContain(rec.taskName);
        }

        unmount();
      }),
      { numRuns: 100 }
    );
  });

  it("for any set of active recommendations, the rendered card displays the total uncovered slot count", () => {
    fc.assert(
      fc.property(recommendationsArb, (recommendations) => {
        mockRecommendations = recommendations;

        const { container, unmount } = render(
          React.createElement(RecommendationCard, {
            spaceId: "space-1",
            groupId: "group-1",
          })
        );

        const textContent = container.textContent ?? "";

        // Calculate expected total uncovered slots
        const expectedTotal = recommendations.reduce(
          (sum, r) => sum + r.totalUncoveredSlotsInRun,
          0
        );

        // The total uncovered slot count must appear in the rendered output
        expect(textContent).toContain(String(expectedTotal));

        unmount();
      }),
      { numRuns: 100 }
    );
  });

  it("for any set of active recommendations, all task names appear as a comma-separated list", () => {
    fc.assert(
      fc.property(recommendationsArb, (recommendations) => {
        mockRecommendations = recommendations;

        const { container, unmount } = render(
          React.createElement(RecommendationCard, {
            spaceId: "space-1",
            groupId: "group-1",
          })
        );

        const textContent = container.textContent ?? "";

        // The component joins task names with ", " — verify the joined string appears
        const expectedTaskNames = recommendations
          .map((r) => r.taskName)
          .join(", ");
        expect(textContent).toContain(expectedTaskNames);

        unmount();
      }),
      { numRuns: 100 }
    );
  });
});
