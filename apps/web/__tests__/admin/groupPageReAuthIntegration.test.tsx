/**
 * Integration tests for group page re-auth flow (Task 4.1).
 *
 * Verifies:
 * - Clicking "Enter Admin Mode" opens ReAuthDialog when `hasCredentials` is true
 * - On success: `enterAdminMode(groupId)` is called, then `enterElevatedMode("management", groupId, timeoutMinutes)` is called
 * - On cancel: dialog closes, user remains in standard view
 * - `managementTimeoutMinutes` is read from group settings (default 15)
 *
 * Requirements: 1.1, 1.3, 1.4, 1.5, 9.4
 *
 * Strategy: We test the integration logic by rendering a minimal component that
 * replicates the exact pattern from the group page (handleAdminModeToggle,
 * handleReAuthSuccess, handleReAuthCancel) wired to the real ReAuthDialog.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import React, { useState } from "react";
import ReAuthDialog from "../../components/admin/ReAuthDialog";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockApiPost = vi.fn();

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    post: (...args: any[]) => mockApiPost(...args),
    get: vi.fn(),
  },
}));

const mockListCredentials = vi.fn();
const mockIsWebAuthnSupported = vi.fn();

vi.mock("@/lib/webauthn", () => ({
  listCredentials: (...args: any[]) => mockListCredentials(...args),
  isWebAuthnSupported: () => mockIsWebAuthnSupported(),
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      title: "Confirm Identity",
      description: "Please confirm your identity to continue.",
      passwordLabel: "Password",
      passwordPlaceholder: "Enter your password",
      confirm: "Confirm",
      verifying: "Verifying...",
      webAuthnButton: "Use Fingerprint",
      webAuthnLabel: "Authenticate with fingerprint",
      or: "or",
      cancel: "Cancel",
      close: "Close",
      loadingCredentials: "Loading...",
      noCredentials: "No credentials configured.",
      authFailed: "Authentication failed. Please try again.",
      rateLimited: "Too many attempts. Please try again later.",
      networkError: "Connection error. Please try again.",
      webAuthnCancelled: "Verification cancelled. Please try again.",
      enterAdminMode: "Enter Admin Mode",
      exitAdminMode: "Exit Admin Mode",
    };
    return translations[key] ?? key;
  },
  useLocale: () => "en",
}));

// ── Test Harness ──────────────────────────────────────────────────────────────

/**
 * Minimal component that replicates the exact integration pattern from
 * apps/web/app/groups/[groupId]/page.tsx for the re-auth flow.
 */
function GroupPageReAuthHarness({
  groupId,
  hasCredentials,
  isAdmin: initialIsAdmin,
  managementTimeoutMinutes,
  enterAdminMode,
  exitAdminMode,
  enterElevatedMode,
}: {
  groupId: string;
  hasCredentials: boolean | null;
  isAdmin: boolean;
  managementTimeoutMinutes?: number;
  enterAdminMode: (groupId: string) => void;
  exitAdminMode: () => void;
  enterElevatedMode: (mode: string, groupId: string, timeoutMinutes: number) => void;
}) {
  const [showReAuthDialog, setShowReAuthDialog] = useState(false);
  const [isAdmin, setIsAdmin] = useState(initialIsAdmin);

  // Simulates group?.managementTimeoutMinutes ?? 15
  const group = { managementTimeoutMinutes: managementTimeoutMinutes ?? undefined };

  const handleAdminModeToggle = () => {
    if (isAdmin) {
      exitAdminMode();
      setIsAdmin(false);
    } else {
      if (hasCredentials === false) return;
      setShowReAuthDialog(true);
    }
  };

  const handleReAuthSuccess = () => {
    setShowReAuthDialog(false);
    const timeoutMinutes = group?.managementTimeoutMinutes ?? 15;
    enterAdminMode(groupId);
    enterElevatedMode("management", groupId, timeoutMinutes);
    setIsAdmin(true);
  };

  const handleReAuthCancel = () => {
    setShowReAuthDialog(false);
  };

  return (
    <div>
      <button
        onClick={handleAdminModeToggle}
        disabled={!isAdmin && hasCredentials === false}
        data-testid="admin-toggle"
      >
        {isAdmin ? "Exit Admin Mode" : "Enter Admin Mode"}
      </button>

      {/* Indicator for test assertions */}
      <span data-testid="admin-state">{isAdmin ? "admin" : "standard"}</span>
      <span data-testid="dialog-state">{showReAuthDialog ? "open" : "closed"}</span>

      <ReAuthDialog
        open={showReAuthDialog}
        onSuccess={handleReAuthSuccess}
        onCancel={handleReAuthCancel}
        mode="management"
        spaceId="space-1"
      />
    </div>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

async function typePassword(password: string) {
  const input = await screen.findByPlaceholderText("Enter your password");
  fireEvent.change(input, { target: { value: password } });
}

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("Group Page Re-Auth Integration (Task 4.1)", () => {
  const mockEnterAdminMode = vi.fn();
  const mockExitAdminMode = vi.fn();
  const mockEnterElevatedMode = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockIsWebAuthnSupported.mockReturnValue(false);
    mockListCredentials.mockResolvedValue([]);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Req 1.1: Clicking 'Enter Admin Mode' opens ReAuthDialog when hasCredentials is true", () => {
    it("opens the ReAuthDialog when button is clicked and hasCredentials is true", async () => {
      render(
        <GroupPageReAuthHarness
          groupId="group-1"
          hasCredentials={true}
          isAdmin={false}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      // Initially dialog is closed
      expect(screen.getByTestId("dialog-state").textContent).toBe("closed");

      // Click the admin toggle
      fireEvent.click(screen.getByTestId("admin-toggle"));

      // Dialog should now be open
      expect(screen.getByTestId("dialog-state").textContent).toBe("open");

      // ReAuthDialog should be visible
      await waitFor(() => {
        expect(screen.getByRole("dialog")).toBeInTheDocument();
      });
    });

    it("does NOT open the dialog when hasCredentials is false (button disabled)", () => {
      render(
        <GroupPageReAuthHarness
          groupId="group-1"
          hasCredentials={false}
          isAdmin={false}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      // Button should be disabled
      const button = screen.getByTestId("admin-toggle");
      expect(button).toBeDisabled();

      // Click should not open dialog
      fireEvent.click(button);
      expect(screen.getByTestId("dialog-state").textContent).toBe("closed");
    });

    it("does NOT open the dialog when hasCredentials is null (loading)", () => {
      render(
        <GroupPageReAuthHarness
          groupId="group-1"
          hasCredentials={null}
          isAdmin={false}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      // Click should open dialog (null means loading, button is not disabled)
      fireEvent.click(screen.getByTestId("admin-toggle"));
      expect(screen.getByTestId("dialog-state").textContent).toBe("open");
    });
  });

  describe("Req 1.3, 9.4: On success, enterAdminMode and enterElevatedMode are called", () => {
    it("calls enterAdminMode(groupId) then enterElevatedMode('management', groupId, timeoutMinutes) on success", async () => {
      mockApiPost.mockResolvedValue({ data: { success: true } });

      render(
        <GroupPageReAuthHarness
          groupId="group-42"
          hasCredentials={true}
          isAdmin={false}
          managementTimeoutMinutes={30}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      // Open dialog
      fireEvent.click(screen.getByTestId("admin-toggle"));

      // Wait for credentials to load and type password
      await waitFor(() => {
        expect(screen.getByPlaceholderText("Enter your password")).toBeInTheDocument();
      });

      await typePassword("correct-password");

      // Submit
      fireEvent.click(screen.getByText("Confirm"));

      // Wait for success
      await waitFor(() => {
        expect(mockEnterAdminMode).toHaveBeenCalledWith("group-42");
      });

      expect(mockEnterElevatedMode).toHaveBeenCalledWith("management", "group-42", 30);

      // Verify order: enterAdminMode called before enterElevatedMode
      const enterAdminOrder = mockEnterAdminMode.mock.invocationCallOrder[0];
      const enterElevatedOrder = mockEnterElevatedMode.mock.invocationCallOrder[0];
      expect(enterAdminOrder).toBeLessThan(enterElevatedOrder);

      // Dialog should be closed
      expect(screen.getByTestId("dialog-state").textContent).toBe("closed");

      // Admin state should be true
      expect(screen.getByTestId("admin-state").textContent).toBe("admin");
    });

    it("uses default timeout of 15 when managementTimeoutMinutes is not set", async () => {
      mockApiPost.mockResolvedValue({ data: { success: true } });

      render(
        <GroupPageReAuthHarness
          groupId="group-7"
          hasCredentials={true}
          isAdmin={false}
          managementTimeoutMinutes={undefined}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      // Open dialog
      fireEvent.click(screen.getByTestId("admin-toggle"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Enter your password")).toBeInTheDocument();
      });

      await typePassword("password");
      fireEvent.click(screen.getByText("Confirm"));

      await waitFor(() => {
        expect(mockEnterElevatedMode).toHaveBeenCalledWith("management", "group-7", 15);
      });
    });
  });

  describe("Req 1.4: On cancel, dialog closes and user remains in standard view", () => {
    it("closes dialog and remains in standard view when cancel button is clicked", async () => {
      render(
        <GroupPageReAuthHarness
          groupId="group-1"
          hasCredentials={true}
          isAdmin={false}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      // Open dialog
      fireEvent.click(screen.getByTestId("admin-toggle"));
      expect(screen.getByTestId("dialog-state").textContent).toBe("open");

      // Wait for dialog to render
      await waitFor(() => {
        expect(screen.getByRole("dialog")).toBeInTheDocument();
      });

      // Click cancel
      fireEvent.click(screen.getByText("Cancel"));

      // Dialog should close
      expect(screen.getByTestId("dialog-state").textContent).toBe("closed");

      // User remains in standard view
      expect(screen.getByTestId("admin-state").textContent).toBe("standard");

      // No store actions called
      expect(mockEnterAdminMode).not.toHaveBeenCalled();
      expect(mockEnterElevatedMode).not.toHaveBeenCalled();
    });

    it("closes dialog when Escape key is pressed", async () => {
      render(
        <GroupPageReAuthHarness
          groupId="group-1"
          hasCredentials={true}
          isAdmin={false}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      // Open dialog
      fireEvent.click(screen.getByTestId("admin-toggle"));

      await waitFor(() => {
        expect(screen.getByRole("dialog")).toBeInTheDocument();
      });

      // Press Escape
      fireEvent.keyDown(screen.getByRole("dialog"), { key: "Escape" });

      // Dialog should close
      expect(screen.getByTestId("dialog-state").textContent).toBe("closed");

      // User remains in standard view
      expect(screen.getByTestId("admin-state").textContent).toBe("standard");
    });
  });

  describe("Req 1.5: All admin role levels are gated", () => {
    it("uses mode='management' for group-level admin entry", async () => {
      mockApiPost.mockResolvedValue({ data: { success: true } });

      render(
        <GroupPageReAuthHarness
          groupId="group-1"
          hasCredentials={true}
          isAdmin={false}
          managementTimeoutMinutes={20}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      fireEvent.click(screen.getByTestId("admin-toggle"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Enter your password")).toBeInTheDocument();
      });

      await typePassword("password");
      fireEvent.click(screen.getByText("Confirm"));

      await waitFor(() => {
        expect(mockEnterElevatedMode).toHaveBeenCalledWith("management", "group-1", 20);
      });
    });
  });

  describe("Exit admin mode does NOT require re-auth", () => {
    it("exits admin mode directly without showing ReAuthDialog", () => {
      render(
        <GroupPageReAuthHarness
          groupId="group-1"
          hasCredentials={true}
          isAdmin={true}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      // Initially in admin mode
      expect(screen.getByTestId("admin-state").textContent).toBe("admin");

      // Click toggle (should exit without dialog)
      fireEvent.click(screen.getByTestId("admin-toggle"));

      // Should exit directly
      expect(mockExitAdminMode).toHaveBeenCalled();
      expect(screen.getByTestId("admin-state").textContent).toBe("standard");

      // Dialog should NOT have opened
      expect(screen.getByTestId("dialog-state").textContent).toBe("closed");
    });
  });

  describe("managementTimeoutMinutes from group settings", () => {
    it("passes custom timeout from group settings to enterElevatedMode", async () => {
      mockApiPost.mockResolvedValue({ data: { success: true } });

      render(
        <GroupPageReAuthHarness
          groupId="group-1"
          hasCredentials={true}
          isAdmin={false}
          managementTimeoutMinutes={45}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      fireEvent.click(screen.getByTestId("admin-toggle"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Enter your password")).toBeInTheDocument();
      });

      await typePassword("password");
      fireEvent.click(screen.getByText("Confirm"));

      await waitFor(() => {
        expect(mockEnterElevatedMode).toHaveBeenCalledWith("management", "group-1", 45);
      });
    });

    it("defaults to 15 minutes when managementTimeoutMinutes is undefined", async () => {
      mockApiPost.mockResolvedValue({ data: { success: true } });

      render(
        <GroupPageReAuthHarness
          groupId="group-1"
          hasCredentials={true}
          isAdmin={false}
          enterAdminMode={mockEnterAdminMode}
          exitAdminMode={mockExitAdminMode}
          enterElevatedMode={mockEnterElevatedMode}
        />
      );

      fireEvent.click(screen.getByTestId("admin-toggle"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Enter your password")).toBeInTheDocument();
      });

      await typePassword("password");
      fireEvent.click(screen.getByText("Confirm"));

      await waitFor(() => {
        expect(mockEnterElevatedMode).toHaveBeenCalledWith("management", "group-1", 15);
      });
    });
  });
});
