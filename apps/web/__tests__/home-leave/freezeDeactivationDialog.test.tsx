/**
 * Unit tests for FreezeDeactivationDialog component.
 *
 * Verifies:
 * - Dialog displays change counts correctly (Req 1.3)
 * - Discard toggle hidden when user lacks permission (Req 5.2)
 * - Discard toggle hidden when no changes exist (Req 1.4)
 * - Discard toggle disabled on count fetch error (Req 1.5)
 * - Confirm calls API with correct discard flag (Req 1.1, 1.2)
 *
 * Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.2
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, act } from "@testing-library/react";
import FreezeDeactivationDialog from "../../components/home-leave/FreezeDeactivationDialog";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockGetFreezePeriodChangesCount = vi.fn();

vi.mock("@/lib/api/homeLeave", () => ({
  getFreezePeriodChangesCount: (...args: any[]) =>
    mockGetFreezePeriodChangesCount(...args),
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      title: "Deactivate Emergency Freeze",
      description: "Are you sure you want to deactivate the emergency freeze?",
      loadingCounts: "Loading changes...",
      changesTitle: "Changes made during freeze",
      overrides: "Overrides",
      manualAssignments: "Manual Assignments",
      swaps: "Swaps",
      noChanges: "No changes were made during the freeze period.",
      fetchError: "Unable to load change summary.",
      discardLabel: "Discard freeze-period changes",
      discardHint: "This will revert all schedule changes made during the freeze.",
      discardUnavailable: "Change summary unavailable — discard is disabled.",
      confirm: "Confirm",
      cancel: "Cancel",
    };
    return translations[key] ?? key;
  },
}));

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("FreezeDeactivationDialog (Task 7.4)", () => {
  const defaultProps = {
    open: true,
    spaceId: "space-123",
    groupId: "group-456",
    canRollback: true,
    onConfirm: vi.fn(),
    onCancel: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Req 1.3: Dialog displays change counts correctly", () => {
    it("displays categorized change counts when changes exist", async () => {
      mockGetFreezePeriodChangesCount.mockResolvedValue({
        overrideCount: 5,
        manualAssignmentCount: 3,
        swapCount: 2,
        totalCount: 10,
      });

      render(<FreezeDeactivationDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText("5")).toBeInTheDocument();
      });

      expect(screen.getByText("3")).toBeInTheDocument();
      expect(screen.getByText("2")).toBeInTheDocument();
      expect(screen.getByText("Overrides")).toBeInTheDocument();
      expect(screen.getByText("Manual Assignments")).toBeInTheDocument();
      expect(screen.getByText("Swaps")).toBeInTheDocument();
    });

    it("fetches counts with correct spaceId and groupId", async () => {
      mockGetFreezePeriodChangesCount.mockResolvedValue({
        overrideCount: 1,
        manualAssignmentCount: 0,
        swapCount: 0,
        totalCount: 1,
      });

      render(<FreezeDeactivationDialog {...defaultProps} />);

      await waitFor(() => {
        expect(mockGetFreezePeriodChangesCount).toHaveBeenCalledWith(
          "space-123",
          "group-456"
        );
      });
    });
  });

  describe("Req 5.2: Discard toggle hidden when user lacks permission", () => {
    it("hides discard toggle when canRollback is false", async () => {
      mockGetFreezePeriodChangesCount.mockResolvedValue({
        overrideCount: 5,
        manualAssignmentCount: 3,
        swapCount: 2,
        totalCount: 10,
      });

      render(
        <FreezeDeactivationDialog {...defaultProps} canRollback={false} />
      );

      await waitFor(() => {
        expect(screen.getByText("5")).toBeInTheDocument();
      });

      // Discard toggle should not be present
      expect(
        screen.queryByLabelText("Discard freeze-period changes")
      ).not.toBeInTheDocument();
      expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
    });

    it("shows discard toggle when canRollback is true and changes exist", async () => {
      mockGetFreezePeriodChangesCount.mockResolvedValue({
        overrideCount: 5,
        manualAssignmentCount: 3,
        swapCount: 2,
        totalCount: 10,
      });

      render(<FreezeDeactivationDialog {...defaultProps} canRollback={true} />);

      await waitFor(() => {
        expect(screen.getByText("5")).toBeInTheDocument();
      });

      expect(screen.getByText("Discard freeze-period changes")).toBeInTheDocument();
    });
  });

  describe("Req 1.4: Discard toggle hidden when no changes exist", () => {
    it("hides discard toggle and shows no-changes message when totalCount is zero", async () => {
      mockGetFreezePeriodChangesCount.mockResolvedValue({
        overrideCount: 0,
        manualAssignmentCount: 0,
        swapCount: 0,
        totalCount: 0,
      });

      render(<FreezeDeactivationDialog {...defaultProps} />);

      await waitFor(() => {
        expect(
          screen.getByText("No changes were made during the freeze period.")
        ).toBeInTheDocument();
      });

      // Discard toggle should not be present
      expect(
        screen.queryByText("Discard freeze-period changes")
      ).not.toBeInTheDocument();
    });
  });

  describe("Req 1.5: Discard toggle disabled on count fetch error", () => {
    it("shows error message and disabled discard toggle when fetch fails", async () => {
      mockGetFreezePeriodChangesCount.mockRejectedValue(
        new Error("Network error")
      );

      render(<FreezeDeactivationDialog {...defaultProps} canRollback={true} />);

      await waitFor(() => {
        expect(
          screen.getByText("Unable to load change summary.")
        ).toBeInTheDocument();
      });

      // The disabled discard toggle should be present with disabled checkbox
      const checkbox = screen.getByRole("checkbox");
      expect(checkbox).toBeDisabled();
    });

    it("shows error message without discard toggle when user lacks permission and fetch fails", async () => {
      mockGetFreezePeriodChangesCount.mockRejectedValue(
        new Error("Network error")
      );

      render(
        <FreezeDeactivationDialog {...defaultProps} canRollback={false} />
      );

      await waitFor(() => {
        expect(
          screen.getByText("Unable to load change summary.")
        ).toBeInTheDocument();
      });

      // No checkbox should be shown since user lacks permission
      expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
    });
  });

  describe("Req 1.1, 1.2: Confirm calls API with correct discard flag", () => {
    it("calls onConfirm with false when discard toggle is unchecked (default)", async () => {
      mockGetFreezePeriodChangesCount.mockResolvedValue({
        overrideCount: 5,
        manualAssignmentCount: 3,
        swapCount: 2,
        totalCount: 10,
      });

      render(<FreezeDeactivationDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText("5")).toBeInTheDocument();
      });

      const confirmBtn = screen.getByRole("button", { name: "Confirm" });
      await act(async () => {
        fireEvent.click(confirmBtn);
      });

      expect(defaultProps.onConfirm).toHaveBeenCalledWith(false);
    });

    it("calls onConfirm with true when discard toggle is checked", async () => {
      mockGetFreezePeriodChangesCount.mockResolvedValue({
        overrideCount: 5,
        manualAssignmentCount: 3,
        swapCount: 2,
        totalCount: 10,
      });

      render(<FreezeDeactivationDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText("5")).toBeInTheDocument();
      });

      // Check the discard toggle
      const checkbox = screen.getByRole("checkbox");
      await act(async () => {
        fireEvent.click(checkbox);
      });

      const confirmBtn = screen.getByRole("button", { name: "Confirm" });
      await act(async () => {
        fireEvent.click(confirmBtn);
      });

      expect(defaultProps.onConfirm).toHaveBeenCalledWith(true);
    });

    it("defaults discard toggle to unchecked (Req 1.2)", async () => {
      mockGetFreezePeriodChangesCount.mockResolvedValue({
        overrideCount: 5,
        manualAssignmentCount: 3,
        swapCount: 2,
        totalCount: 10,
      });

      render(<FreezeDeactivationDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText("5")).toBeInTheDocument();
      });

      const checkbox = screen.getByRole("checkbox") as HTMLInputElement;
      expect(checkbox.checked).toBe(false);
    });

    it("calls onCancel when cancel button is clicked", async () => {
      mockGetFreezePeriodChangesCount.mockResolvedValue({
        overrideCount: 1,
        manualAssignmentCount: 0,
        swapCount: 0,
        totalCount: 1,
      });

      render(<FreezeDeactivationDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText("1")).toBeInTheDocument();
      });

      const cancelBtn = screen.getByRole("button", { name: "Cancel" });
      await act(async () => {
        fireEvent.click(cancelBtn);
      });

      expect(defaultProps.onCancel).toHaveBeenCalledTimes(1);
      expect(defaultProps.onConfirm).not.toHaveBeenCalled();
    });
  });
});
