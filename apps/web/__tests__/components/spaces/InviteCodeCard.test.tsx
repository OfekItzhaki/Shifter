/**
 * Unit tests for InviteCodeCard component.
 *
 * Verifies:
 * - Displays the current invite code (Req 8.1)
 * - Copy button copies code to clipboard (Req 8.2)
 * - Regenerate calls API and updates displayed code (Req 8.3)
 * - Returns null when isOwner is false (Req 8.4)
 *
 * Requirements: 8.1, 8.2, 8.3
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import InviteCodeCard from "../../../components/spaces/InviteCodeCard";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockRegenerateSpaceInviteCode = vi.hoisted(() => vi.fn());

vi.mock("@/lib/api/spaces", () => ({
  regenerateSpaceInviteCode: mockRegenerateSpaceInviteCode,
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      inviteCode: "Invite Code",
      copyCode: "Copy",
      copied: "Copied",
      regenerateCode: "Regenerate",
      regenerateConfirm: "This will invalidate the current code. Are you sure?",
    };
    return translations[key] ?? key;
  },
}));

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("InviteCodeCard (Task 18.2)", () => {
  const mockWriteText = vi.fn().mockResolvedValue(undefined);

  beforeEach(() => {
    vi.clearAllMocks();
    Object.assign(navigator, {
      clipboard: { writeText: mockWriteText },
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Req 8.1: Displays the current invite code", () => {
    it("renders the invite code in a code element", () => {
      render(
        <InviteCodeCard
          spaceId="space-1"
          inviteCode="ABC12345"
          isOwner={true}
        />
      );

      expect(screen.getByText("ABC12345")).toBeInTheDocument();
    });

    it("renders the section heading", () => {
      render(
        <InviteCodeCard
          spaceId="space-1"
          inviteCode="XYZ99999"
          isOwner={true}
        />
      );

      expect(screen.getByText("Invite Code")).toBeInTheDocument();
    });
  });

  describe("Req 8.2: Copy button copies code to clipboard", () => {
    it("calls navigator.clipboard.writeText with the invite code on copy click", async () => {
      render(
        <InviteCodeCard
          spaceId="space-1"
          inviteCode="TESTCODE"
          isOwner={true}
        />
      );

      const copyButton = screen.getByRole("button", { name: "Copy" });
      await act(async () => {
        fireEvent.click(copyButton);
      });

      expect(mockWriteText).toHaveBeenCalledWith("TESTCODE");
    });

    it("shows 'Copied' text after clicking copy", async () => {
      render(
        <InviteCodeCard
          spaceId="space-1"
          inviteCode="TESTCODE"
          isOwner={true}
        />
      );

      const copyButton = screen.getByRole("button", { name: "Copy" });
      await act(async () => {
        fireEvent.click(copyButton);
      });

      expect(screen.getByText("Copied")).toBeInTheDocument();
    });
  });

  describe("Req 8.3: Regenerate calls API and updates display", () => {
    it("shows confirmation UI when regenerate button is clicked", () => {
      render(
        <InviteCodeCard
          spaceId="space-1"
          inviteCode="OLDCODE1"
          isOwner={true}
        />
      );

      const regenerateButton = screen.getByText("Regenerate");
      fireEvent.click(regenerateButton);

      expect(
        screen.getByText("This will invalidate the current code. Are you sure?")
      ).toBeInTheDocument();
    });

    it("calls regenerateSpaceInviteCode and updates the displayed code", async () => {
      mockRegenerateSpaceInviteCode.mockResolvedValue({
        inviteCode: "NEWCODE1",
      });

      render(
        <InviteCodeCard
          spaceId="space-1"
          inviteCode="OLDCODE1"
          isOwner={true}
        />
      );

      // Click regenerate to show confirmation
      fireEvent.click(screen.getByText("Regenerate"));

      // Confirm regeneration
      const confirmButtons = screen.getAllByText("Regenerate");
      await act(async () => {
        fireEvent.click(confirmButtons[0]);
      });

      await waitFor(() => {
        expect(mockRegenerateSpaceInviteCode).toHaveBeenCalledWith("space-1");
      });

      await waitFor(() => {
        expect(screen.getByText("NEWCODE1")).toBeInTheDocument();
      });
    });

    it("calls onCodeRegenerated callback with the new code", async () => {
      mockRegenerateSpaceInviteCode.mockResolvedValue({
        inviteCode: "NEWCODE2",
      });
      const onCodeRegenerated = vi.fn();

      render(
        <InviteCodeCard
          spaceId="space-1"
          inviteCode="OLDCODE2"
          isOwner={true}
          onCodeRegenerated={onCodeRegenerated}
        />
      );

      // Click regenerate to show confirmation
      fireEvent.click(screen.getByText("Regenerate"));

      // Confirm regeneration
      const confirmButtons = screen.getAllByText("Regenerate");
      await act(async () => {
        fireEvent.click(confirmButtons[0]);
      });

      await waitFor(() => {
        expect(onCodeRegenerated).toHaveBeenCalledWith("NEWCODE2");
      });
    });

    it("hides confirmation UI after successful regeneration", async () => {
      mockRegenerateSpaceInviteCode.mockResolvedValue({
        inviteCode: "NEWCODE3",
      });

      render(
        <InviteCodeCard
          spaceId="space-1"
          inviteCode="OLDCODE3"
          isOwner={true}
        />
      );

      // Click regenerate to show confirmation
      fireEvent.click(screen.getByText("Regenerate"));

      // Confirm regeneration
      const confirmButtons = screen.getAllByText("Regenerate");
      await act(async () => {
        fireEvent.click(confirmButtons[0]);
      });

      await waitFor(() => {
        expect(
          screen.queryByText(
            "This will invalidate the current code. Are you sure?"
          )
        ).not.toBeInTheDocument();
      });
    });
  });

  describe("Req 8.4: Returns null when isOwner is false", () => {
    it("renders nothing when isOwner is false", () => {
      const { container } = render(
        <InviteCodeCard
          spaceId="space-1"
          inviteCode="ABC12345"
          isOwner={false}
        />
      );

      expect(container.innerHTML).toBe("");
    });

    it("renders nothing when inviteCode is null", () => {
      const { container } = render(
        <InviteCodeCard
          spaceId="space-1"
          inviteCode={null}
          isOwner={true}
        />
      );

      expect(container.innerHTML).toBe("");
    });
  });
});
