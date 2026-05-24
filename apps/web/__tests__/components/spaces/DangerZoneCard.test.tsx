/**
 * Unit tests for DangerZoneCard component.
 *
 * Verifies:
 * - Confirmation dialog appears before delete (Req 9.2)
 * - Member dropdown excludes current owner (Req 9.4)
 * - Transfer shows success/error messages (Req 9.3)
 * - Returns null when isOwner is false (Req 9.1)
 *
 * Requirements: 9.1, 9.2, 9.3, 9.4
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import DangerZoneCard from "../../../components/spaces/DangerZoneCard";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockSoftDeleteSpace = vi.hoisted(() => vi.fn());
const mockTransferOwnership = vi.hoisted(() => vi.fn());

vi.mock("@/lib/api/spaces", () => ({
  softDeleteSpace: mockSoftDeleteSpace,
  transferOwnership: mockTransferOwnership,
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      "dangerZone.title": "Danger Zone",
      "dangerZone.description": "Irreversible and destructive actions",
      "dangerZone.deleteTitle": "Delete Space",
      "dangerZone.deleteDescription": "Permanently delete this space and all its data.",
      "dangerZone.deleteButton": "Delete Space",
      "dangerZone.deleteConfirm": "Are you sure? This action cannot be undone.",
      "dangerZone.confirmDelete": "Yes, Delete",
      "dangerZone.deleting": "Deleting...",
      "dangerZone.cancel": "Cancel",
      "dangerZone.deleteSuccess": "Space deleted successfully.",
      "dangerZone.deleteError": "Failed to delete space.",
      "dangerZone.transferTitle": "Transfer Ownership",
      "dangerZone.transferDescription": "Transfer this space to another member.",
      "dangerZone.transferButton": "Transfer",
      "dangerZone.confirmTransfer": "Confirm Transfer",
      "dangerZone.transferring": "Transferring...",
      "dangerZone.transferSuccess": "Ownership transferred successfully.",
      "dangerZone.transferError": "Failed to transfer ownership.",
      "dangerZone.selectMember": "Select a member",
      "dangerZone.selectMemberLabel": "Select transfer target",
    };
    return translations[key] ?? key;
  },
}));

// ── Test Data ─────────────────────────────────────────────────────────────────

const defaultProps = {
  spaceId: "space-123",
  isOwner: true,
  currentOwnerId: "owner-1",
  members: [
    { userId: "owner-1", displayName: "Owner User", email: "owner@test.com", joinedAt: "2025-01-01T00:00:00Z" },
    { userId: "member-2", displayName: "Alice", email: "alice@test.com", joinedAt: "2025-01-02T00:00:00Z" },
    { userId: "member-3", displayName: "Bob", email: "bob@test.com", joinedAt: "2025-01-03T00:00:00Z" },
  ],
};

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("DangerZoneCard (Task 16.3)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Req 9.1: Hidden for non-owners", () => {
    it("renders nothing when isOwner is false", () => {
      const { container } = render(
        <DangerZoneCard {...defaultProps} isOwner={false} />
      );

      expect(container.innerHTML).toBe("");
    });

    it("renders the danger zone section when isOwner is true", () => {
      render(<DangerZoneCard {...defaultProps} />);

      expect(screen.getByText("Danger Zone")).toBeInTheDocument();
    });
  });

  describe("Req 9.2: Confirmation dialog appears before delete", () => {
    it("shows delete button initially without confirmation", () => {
      render(<DangerZoneCard {...defaultProps} />);

      expect(screen.getByRole("button", { name: "Delete Space" })).toBeInTheDocument();
      expect(screen.queryByText("Yes, Delete")).not.toBeInTheDocument();
    });

    it("shows confirmation dialog when delete button is clicked", () => {
      render(<DangerZoneCard {...defaultProps} />);

      fireEvent.click(screen.getByRole("button", { name: "Delete Space" }));

      expect(screen.getByText("Are you sure? This action cannot be undone.")).toBeInTheDocument();
      expect(screen.getByText("Yes, Delete")).toBeInTheDocument();
      expect(screen.getByText("Cancel")).toBeInTheDocument();
    });

    it("does not call softDeleteSpace until confirmation is clicked", () => {
      render(<DangerZoneCard {...defaultProps} />);

      fireEvent.click(screen.getByRole("button", { name: "Delete Space" }));

      expect(mockSoftDeleteSpace).not.toHaveBeenCalled();
    });

    it("calls softDeleteSpace after confirmation", async () => {
      mockSoftDeleteSpace.mockResolvedValue(undefined);
      render(<DangerZoneCard {...defaultProps} />);

      fireEvent.click(screen.getByRole("button", { name: "Delete Space" }));
      await act(async () => {
        fireEvent.click(screen.getByText("Yes, Delete"));
      });

      await waitFor(() => {
        expect(mockSoftDeleteSpace).toHaveBeenCalledWith("space-123");
      });
    });

    it("hides confirmation dialog when cancel is clicked", () => {
      render(<DangerZoneCard {...defaultProps} />);

      fireEvent.click(screen.getByRole("button", { name: "Delete Space" }));
      expect(screen.getByText("Yes, Delete")).toBeInTheDocument();

      fireEvent.click(screen.getByText("Cancel"));
      expect(screen.queryByText("Yes, Delete")).not.toBeInTheDocument();
    });
  });

  describe("Req 9.4: Member dropdown excludes current owner", () => {
    it("does not include the current owner in the dropdown options", () => {
      render(<DangerZoneCard {...defaultProps} />);

      const select = screen.getByLabelText("Select transfer target") as HTMLSelectElement;
      const options = Array.from(select.options);
      const optionValues = options.map((o) => o.value);

      expect(optionValues).not.toContain("owner-1");
    });

    it("includes all non-owner members in the dropdown", () => {
      render(<DangerZoneCard {...defaultProps} />);

      const select = screen.getByLabelText("Select transfer target") as HTMLSelectElement;
      const options = Array.from(select.options);
      const optionValues = options.map((o) => o.value);

      expect(optionValues).toContain("member-2");
      expect(optionValues).toContain("member-3");
    });

    it("displays member display names in the dropdown", () => {
      render(<DangerZoneCard {...defaultProps} />);

      expect(screen.getByText("Alice")).toBeInTheDocument();
      expect(screen.getByText("Bob")).toBeInTheDocument();
    });

    it("shows exactly N-1 member options (plus placeholder)", () => {
      render(<DangerZoneCard {...defaultProps} />);

      const select = screen.getByLabelText("Select transfer target") as HTMLSelectElement;
      // 1 placeholder + 2 members (owner excluded)
      expect(select.options.length).toBe(3);
    });
  });

  describe("Req 9.3: Transfer shows success/error messages", () => {
    it("shows success message after successful transfer", async () => {
      mockTransferOwnership.mockResolvedValue(undefined);
      render(<DangerZoneCard {...defaultProps} />);

      // Select a member
      const select = screen.getByLabelText("Select transfer target");
      fireEvent.change(select, { target: { value: "member-2" } });

      // Click transfer button
      fireEvent.click(screen.getByText("Transfer"));

      // Confirm transfer
      await act(async () => {
        fireEvent.click(screen.getByText("Confirm Transfer"));
      });

      await waitFor(() => {
        expect(screen.getByText("Ownership transferred successfully.")).toBeInTheDocument();
      });
      expect(mockTransferOwnership).toHaveBeenCalledWith("space-123", "member-2");
    });

    it("shows error message when transfer fails", async () => {
      mockTransferOwnership.mockRejectedValue(new Error("Network error"));
      render(<DangerZoneCard {...defaultProps} />);

      // Select a member
      const select = screen.getByLabelText("Select transfer target");
      fireEvent.change(select, { target: { value: "member-3" } });

      // Click transfer button
      fireEvent.click(screen.getByText("Transfer"));

      // Confirm transfer
      await act(async () => {
        fireEvent.click(screen.getByText("Confirm Transfer"));
      });

      await waitFor(() => {
        expect(screen.getByText("Failed to transfer ownership.")).toBeInTheDocument();
      });
    });

    it("shows success message after successful delete", async () => {
      mockSoftDeleteSpace.mockResolvedValue(undefined);
      render(<DangerZoneCard {...defaultProps} />);

      fireEvent.click(screen.getByRole("button", { name: "Delete Space" }));
      await act(async () => {
        fireEvent.click(screen.getByText("Yes, Delete"));
      });

      await waitFor(() => {
        expect(screen.getByText("Space deleted successfully.")).toBeInTheDocument();
      });
    });

    it("shows error message when delete fails", async () => {
      mockSoftDeleteSpace.mockRejectedValue(new Error("Server error"));
      render(<DangerZoneCard {...defaultProps} />);

      fireEvent.click(screen.getByRole("button", { name: "Delete Space" }));
      await act(async () => {
        fireEvent.click(screen.getByText("Yes, Delete"));
      });

      await waitFor(() => {
        expect(screen.getByText("Failed to delete space.")).toBeInTheDocument();
      });
    });
  });
});
