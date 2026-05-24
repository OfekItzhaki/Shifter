/**
 * Unit tests for RegenerationStatusIndicator component.
 *
 * Verifies:
 * - Polls GET /spaces/{spaceId}/schedule-runs/{runId} for status updates (Req 8.2)
 * - Displays queued state with spinner (Req 8.5)
 * - Displays running state with spinner (Req 8.5)
 * - Displays completed state with success icon and "Review Draft" button (Req 8.3, 4.4)
 * - Displays failed state with error message from errorSummary (Req 8.4)
 * - Stops polling when status is "completed" or "failed"
 * - Calls onCompleted with resultVersionId on success (Req 8.3)
 * - Dismiss button appears on terminal states
 *
 * Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 4.4
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, act, fireEvent, waitFor } from "@testing-library/react";

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockGetRunStatus = vi.fn();

vi.mock("@/lib/api/schedule", () => ({
  getRunStatus: (...args: unknown[]) => mockGetRunStatus(...args),
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      ariaLabel: "Regeneration run status",
      queued: "Queued for processing...",
      running: "Computing new schedule...",
      completed: "New schedule is ready",
      failed: "Schedule regeneration failed",
      unknownError: "An unknown error occurred",
      pollError: "Error checking run status",
      reviewDraft: "Review Draft",
      dismiss: "Dismiss",
    };
    return translations[key] ?? key;
  },
}));

import RegenerationStatusIndicator, {
  type RegenerationStatusIndicatorProps,
} from "../../components/schedule/RegenerationStatusIndicator";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function defaultProps(
  overrides?: Partial<RegenerationStatusIndicatorProps>
): RegenerationStatusIndicatorProps {
  return {
    spaceId: "space-1",
    runId: "run-1",
    onCompleted: vi.fn(),
    onDismiss: vi.fn(),
    ...overrides,
  };
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe("RegenerationStatusIndicator", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mockGetRunStatus.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe("Req 8.5: Queued state display", () => {
    it("shows queued text on initial render when status is queued", async () => {
      mockGetRunStatus.mockResolvedValue({ status: "Queued" });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.getByText("Queued for processing...")).toBeInTheDocument();
    });

    it("has role=status and aria-live=polite for accessibility", async () => {
      mockGetRunStatus.mockResolvedValue({ status: "Queued" });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      const statusEl = screen.getByRole("status");
      expect(statusEl).toHaveAttribute("aria-live", "polite");
      expect(statusEl).toHaveAttribute("aria-label", "Regeneration run status");
    });
  });

  describe("Req 8.5: Running state display", () => {
    it("shows running text when status transitions to running", async () => {
      mockGetRunStatus.mockResolvedValue({ status: "Running" });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.getByText("Computing new schedule...")).toBeInTheDocument();
    });
  });

  describe("Req 8.3, 4.4: Completed state display", () => {
    it("shows completed text and Review Draft button when status is completed", async () => {
      mockGetRunStatus.mockResolvedValue({
        status: "Completed",
        resultVersionId: "version-123",
      });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.getByText("New schedule is ready")).toBeInTheDocument();
      expect(screen.getByText("Review Draft")).toBeInTheDocument();
    });

    it("calls onCompleted with resultVersionId when run completes", async () => {
      const onCompleted = vi.fn();
      mockGetRunStatus.mockResolvedValue({
        status: "Completed",
        resultVersionId: "version-456",
      });

      await act(async () => {
        render(
          <RegenerationStatusIndicator {...defaultProps({ onCompleted })} />
        );
      });

      expect(onCompleted).toHaveBeenCalledWith("version-456");
    });

    it("clicking Review Draft calls onCompleted with the version ID", async () => {
      const onCompleted = vi.fn();
      mockGetRunStatus.mockResolvedValue({
        status: "Completed",
        resultVersionId: "version-789",
      });

      await act(async () => {
        render(
          <RegenerationStatusIndicator {...defaultProps({ onCompleted })} />
        );
      });

      // onCompleted called automatically on completion
      expect(onCompleted).toHaveBeenCalledWith("version-789");
      const callsBefore = onCompleted.mock.calls.length;

      // Click Review Draft button — should call onCompleted again
      fireEvent.click(screen.getByText("Review Draft"));
      expect(onCompleted).toHaveBeenCalledTimes(callsBefore + 1);
      expect(onCompleted).toHaveBeenLastCalledWith("version-789");
    });
  });

  describe("Req 8.4: Failed state display", () => {
    it("shows failed text and error message from errorSummary", async () => {
      mockGetRunStatus.mockResolvedValue({
        status: "Failed",
        errorSummary: "Solver timed out after 300 seconds",
      });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.getByText("Schedule regeneration failed")).toBeInTheDocument();
      expect(
        screen.getByText("Solver timed out after 300 seconds")
      ).toBeInTheDocument();
    });

    it("shows generic error when errorSummary is null", async () => {
      mockGetRunStatus.mockResolvedValue({
        status: "Failed",
        errorSummary: null,
      });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.getByText("An unknown error occurred")).toBeInTheDocument();
    });

    it("shows poll error when API call throws", async () => {
      mockGetRunStatus.mockRejectedValue(new Error("Network error"));

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.getByText("Error checking run status")).toBeInTheDocument();
    });
  });

  describe("Polling behavior", () => {
    it("polls the API with correct spaceId and runId", async () => {
      mockGetRunStatus.mockResolvedValue({ status: "Queued" });

      await act(async () => {
        render(
          <RegenerationStatusIndicator
            {...defaultProps({ spaceId: "my-space", runId: "my-run" })}
          />
        );
      });

      expect(mockGetRunStatus).toHaveBeenCalledWith("my-space", "my-run");
    });

    it("stops polling after status becomes completed", async () => {
      mockGetRunStatus.mockResolvedValue({
        status: "Completed",
        resultVersionId: "v-1",
      });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      const callCount = mockGetRunStatus.mock.calls.length;

      // Advance timer — should not poll again
      await act(async () => {
        vi.advanceTimersByTime(6000);
      });

      expect(mockGetRunStatus.mock.calls.length).toBe(callCount);
    });

    it("stops polling after status becomes failed", async () => {
      mockGetRunStatus.mockResolvedValue({
        status: "Failed",
        errorSummary: "Error",
      });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      const callCount = mockGetRunStatus.mock.calls.length;

      await act(async () => {
        vi.advanceTimersByTime(6000);
      });

      expect(mockGetRunStatus.mock.calls.length).toBe(callCount);
    });

    it("continues polling while status is queued or running", async () => {
      mockGetRunStatus.mockResolvedValue({ status: "Queued" });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      // Initial call
      expect(mockGetRunStatus).toHaveBeenCalledTimes(1);

      // Advance 3 seconds — should poll again
      await act(async () => {
        vi.advanceTimersByTime(3000);
      });

      expect(mockGetRunStatus).toHaveBeenCalledTimes(2);
    });
  });

  describe("Dismiss button", () => {
    it("shows dismiss button on completed state", async () => {
      mockGetRunStatus.mockResolvedValue({
        status: "Completed",
        resultVersionId: "v-1",
      });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.getByLabelText("Dismiss")).toBeInTheDocument();
    });

    it("shows dismiss button on failed state", async () => {
      mockGetRunStatus.mockResolvedValue({
        status: "Failed",
        errorSummary: "Error",
      });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.getByLabelText("Dismiss")).toBeInTheDocument();
    });

    it("calls onDismiss when dismiss button is clicked", async () => {
      const onDismiss = vi.fn();
      mockGetRunStatus.mockResolvedValue({
        status: "Completed",
        resultVersionId: "v-1",
      });

      await act(async () => {
        render(
          <RegenerationStatusIndicator {...defaultProps({ onDismiss })} />
        );
      });

      fireEvent.click(screen.getByLabelText("Dismiss"));
      expect(onDismiss).toHaveBeenCalledTimes(1);
    });

    it("does not show dismiss button while queued", async () => {
      mockGetRunStatus.mockResolvedValue({ status: "Queued" });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.queryByLabelText("Dismiss")).not.toBeInTheDocument();
    });

    it("does not show dismiss button while running", async () => {
      mockGetRunStatus.mockResolvedValue({ status: "Running" });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.queryByLabelText("Dismiss")).not.toBeInTheDocument();
    });
  });

  describe("data-phase attribute", () => {
    it("sets data-phase to queued", async () => {
      mockGetRunStatus.mockResolvedValue({ status: "Queued" });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.getByRole("status")).toHaveAttribute("data-phase", "queued");
    });

    it("sets data-phase to completed", async () => {
      mockGetRunStatus.mockResolvedValue({
        status: "Completed",
        resultVersionId: "v-1",
      });

      await act(async () => {
        render(<RegenerationStatusIndicator {...defaultProps()} />);
      });

      expect(screen.getByRole("status")).toHaveAttribute("data-phase", "completed");
    });
  });
});
