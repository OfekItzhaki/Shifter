/**
 * Unit tests for RecommendationCard component.
 *
 * Verifies:
 * - Card renders when recommendations exist (Req 2.1)
 * - Card does not render when recommendations array is empty (Req 2.5)
 * - "Go to Tasks" button navigates to ?tab=tasks (Req 3.1)
 * - "Dismiss" button calls dismiss mutation (Req 2.4)
 *
 * Requirements: 2.1, 2.4, 2.5, 3.1
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import RecommendationCard from "../../components/recommendations/RecommendationCard";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockMutate = vi.fn();
const mockPush = vi.fn();

let mockRecommendationsData: any[] | undefined = undefined;
let mockIsLoading = false;

vi.mock("@/lib/query/hooks/useRecommendations", () => ({
  useRecommendations: () => ({
    data: mockRecommendationsData,
    isLoading: mockIsLoading,
  }),
  useDismissRecommendation: () => ({
    mutate: mockMutate,
    isPending: false,
  }),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: mockPush,
  }),
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, params?: Record<string, any>) => {
    const translations: Record<string, string> = {
      cardTitle: "Recommendation",
      cardDescription: `Tasks: ${params?.taskNames ?? ""}, Slots: ${params?.count ?? 0}`,
      goToTasks: "Go to Tasks",
      dismiss: "Dismiss",
      slotsCount: `${params?.count ?? 0} uncovered slots`,
    };
    return translations[key] ?? key;
  },
}));

// ── Test Data ─────────────────────────────────────────────────────────────────

const sampleRecommendations = [
  {
    id: "rec-1",
    groupTaskId: "task-1",
    taskName: "Guard Duty",
    status: "Active" as const,
    additionalSlotsCovered: 3,
    affectedDateStart: "2026-01-01",
    affectedDateEnd: "2026-01-07",
    totalUncoveredSlotsInRun: 5,
    createdAt: "2026-01-01T00:00:00Z",
  },
  {
    id: "rec-2",
    groupTaskId: "task-2",
    taskName: "Kitchen",
    status: "Active" as const,
    additionalSlotsCovered: 2,
    affectedDateStart: "2026-01-01",
    affectedDateEnd: "2026-01-07",
    totalUncoveredSlotsInRun: 3,
    createdAt: "2026-01-01T00:00:00Z",
  },
];

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("RecommendationCard (Task 4.4)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockRecommendationsData = undefined;
    mockIsLoading = false;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Req 2.1: Card renders when recommendations exist", () => {
    it("renders the card when active recommendations are present", () => {
      mockRecommendationsData = sampleRecommendations;

      const { container } = render(
        <RecommendationCard spaceId="space-1" groupId="group-1" />
      );

      expect(container.querySelector('[role="status"]')).toBeInTheDocument();
      expect(screen.getByText("Recommendation")).toBeInTheDocument();
      expect(screen.getByText("Go to Tasks")).toBeInTheDocument();
      expect(screen.getByText("Dismiss")).toBeInTheDocument();
    });

    it("displays task names in the card description", () => {
      mockRecommendationsData = sampleRecommendations;

      render(<RecommendationCard spaceId="space-1" groupId="group-1" />);

      // The description should contain both task names
      expect(
        screen.getByText(/Guard Duty, Kitchen/)
      ).toBeInTheDocument();
    });
  });

  describe("Req 2.5: Card does not render when recommendations array is empty", () => {
    it("renders nothing when recommendations array is empty", () => {
      mockRecommendationsData = [];

      const { container } = render(
        <RecommendationCard spaceId="space-1" groupId="group-1" />
      );

      expect(container.querySelector('[role="status"]')).not.toBeInTheDocument();
      expect(container.innerHTML).toBe("");
    });

    it("renders nothing when recommendations data is undefined", () => {
      mockRecommendationsData = undefined;

      const { container } = render(
        <RecommendationCard spaceId="space-1" groupId="group-1" />
      );

      expect(container.innerHTML).toBe("");
    });

    it("renders nothing while loading", () => {
      mockIsLoading = true;
      mockRecommendationsData = sampleRecommendations;

      const { container } = render(
        <RecommendationCard spaceId="space-1" groupId="group-1" />
      );

      expect(container.innerHTML).toBe("");
    });
  });

  describe("Req 3.1: 'Go to Tasks' button navigates to ?tab=tasks", () => {
    it("navigates to the tasks tab when 'Go to Tasks' is clicked", async () => {
      mockRecommendationsData = sampleRecommendations;

      render(<RecommendationCard spaceId="space-1" groupId="group-1" />);

      const goToTasksBtn = screen.getByText("Go to Tasks");
      await act(async () => {
        fireEvent.click(goToTasksBtn);
      });

      expect(mockPush).toHaveBeenCalledWith("/groups/group-1?tab=tasks");
    });

    it("uses the correct groupId in the navigation URL", async () => {
      mockRecommendationsData = sampleRecommendations;

      render(<RecommendationCard spaceId="space-1" groupId="my-group-id" />);

      const goToTasksBtn = screen.getByText("Go to Tasks");
      await act(async () => {
        fireEvent.click(goToTasksBtn);
      });

      expect(mockPush).toHaveBeenCalledWith("/groups/my-group-id?tab=tasks");
    });
  });

  describe("Req 2.4: 'Dismiss' button calls dismiss mutation", () => {
    it("calls dismiss mutation for each recommendation when 'Dismiss' is clicked", async () => {
      mockRecommendationsData = sampleRecommendations;

      render(<RecommendationCard spaceId="space-1" groupId="group-1" />);

      const dismissBtn = screen.getByText("Dismiss");
      await act(async () => {
        fireEvent.click(dismissBtn);
      });

      expect(mockMutate).toHaveBeenCalledTimes(2);
      expect(mockMutate).toHaveBeenCalledWith("rec-1");
      expect(mockMutate).toHaveBeenCalledWith("rec-2");
    });

    it("calls dismiss mutation with single recommendation id", async () => {
      mockRecommendationsData = [sampleRecommendations[0]];

      render(<RecommendationCard spaceId="space-1" groupId="group-1" />);

      const dismissBtn = screen.getByText("Dismiss");
      await act(async () => {
        fireEvent.click(dismissBtn);
      });

      expect(mockMutate).toHaveBeenCalledTimes(1);
      expect(mockMutate).toHaveBeenCalledWith("rec-1");
    });
  });
});
