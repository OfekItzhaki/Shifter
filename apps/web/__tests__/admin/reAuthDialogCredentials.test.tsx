/**
 * Unit tests for ReAuthDialog credential availability check.
 *
 * Verifies:
 * - Dialog fetches WebAuthn credential availability via listCredentials()
 * - Password is always shown (system invariant: all registered users have a password)
 * - WebAuthn button is only rendered when isWebAuthnSupported() returns true AND user has registered credentials
 * - Dialog handles the case where WebAuthn is not supported by the browser (button not rendered)
 *
 * Requirements: 2.3, 2.4, 2.5, 2.6, 4.6, 9.2
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import ReAuthDialog from "../../components/admin/ReAuthDialog";

// ── Mocks ─────────────────────────────────────────────────────────────────────

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
    };
    return translations[key] ?? key;
  },
  useLocale: () => "en",
}));

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    post: vi.fn(),
    get: vi.fn(),
  },
}));

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("ReAuthDialog - Credential Availability Check (Task 3.2)", () => {
  const defaultProps = {
    open: true,
    onSuccess: vi.fn(),
    onCancel: vi.fn(),
    mode: "management" as const,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Fetches WebAuthn credential availability via listCredentials() (Req 9.2)", () => {
    it("calls listCredentials() when dialog opens and WebAuthn is supported", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([
        { id: "cred-1", nickname: "My Key", createdAt: "2024-01-01", lastUsedAt: null, isDisabled: false },
      ]);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(mockListCredentials).toHaveBeenCalledTimes(1);
      });
    });

    it("does NOT call listCredentials() when WebAuthn is not supported", async () => {
      mockIsWebAuthnSupported.mockReturnValue(false);

      render(<ReAuthDialog {...defaultProps} />);

      // Wait for credential loading to complete
      await waitFor(() => {
        expect(screen.getByLabelText("Password")).toBeInTheDocument();
      });

      expect(mockListCredentials).not.toHaveBeenCalled();
    });
  });

  describe("Password is always shown (Req 2.3)", () => {
    it("renders password input when WebAuthn is supported and user has credentials", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([
        { id: "cred-1", nickname: "My Key", createdAt: "2024-01-01", lastUsedAt: null, isDisabled: false },
      ]);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Password")).toBeInTheDocument();
      });
    });

    it("renders password input when WebAuthn is not supported", async () => {
      mockIsWebAuthnSupported.mockReturnValue(false);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Password")).toBeInTheDocument();
      });
    });

    it("renders password input when WebAuthn is supported but user has no credentials", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([]);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Password")).toBeInTheDocument();
      });
    });

    it("password input has autocomplete='current-password'", async () => {
      mockIsWebAuthnSupported.mockReturnValue(false);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        const input = screen.getByLabelText("Password");
        expect(input).toHaveAttribute("autocomplete", "current-password");
      });
    });
  });

  describe("WebAuthn button only rendered when supported AND user has credentials (Req 2.4, 2.5, 2.6)", () => {
    it("renders WebAuthn button when browser supports it AND user has registered credentials", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([
        { id: "cred-1", nickname: "My Key", createdAt: "2024-01-01", lastUsedAt: null, isDisabled: false },
      ]);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Authenticate with fingerprint" })).toBeInTheDocument();
      });
    });

    it("does NOT render WebAuthn button when browser does not support WebAuthn (Req 4.6)", async () => {
      mockIsWebAuthnSupported.mockReturnValue(false);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Password")).toBeInTheDocument();
      });

      expect(screen.queryByRole("button", { name: "Authenticate with fingerprint" })).not.toBeInTheDocument();
    });

    it("does NOT render WebAuthn button when user has no registered credentials", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([]);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Password")).toBeInTheDocument();
      });

      expect(screen.queryByRole("button", { name: "Authenticate with fingerprint" })).not.toBeInTheDocument();
    });

    it("shows both password and WebAuthn when user has both (Req 2.5)", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([
        { id: "cred-1", nickname: "My Key", createdAt: "2024-01-01", lastUsedAt: null, isDisabled: false },
      ]);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Password")).toBeInTheDocument();
        expect(screen.getByRole("button", { name: "Authenticate with fingerprint" })).toBeInTheDocument();
      });
    });

    it("shows only password when WebAuthn is not available (Req 2.6)", async () => {
      mockIsWebAuthnSupported.mockReturnValue(false);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Password")).toBeInTheDocument();
      });

      expect(screen.queryByRole("button", { name: "Authenticate with fingerprint" })).not.toBeInTheDocument();
    });
  });

  describe("Handles listCredentials() failure gracefully", () => {
    it("falls back to password-only when listCredentials() throws", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockRejectedValue(new Error("Network error"));

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Password")).toBeInTheDocument();
      });

      expect(screen.queryByRole("button", { name: "Authenticate with fingerprint" })).not.toBeInTheDocument();
    });
  });

  describe("Dialog does not render when closed", () => {
    it("renders nothing when open is false", () => {
      mockIsWebAuthnSupported.mockReturnValue(true);

      const { container } = render(<ReAuthDialog {...defaultProps} open={false} />);

      expect(container.innerHTML).toBe("");
    });
  });
});
