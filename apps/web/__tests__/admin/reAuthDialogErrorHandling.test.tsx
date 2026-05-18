/**
 * Unit tests for ReAuthDialog error handling and recovery.
 *
 * Verifies:
 * - API 401 shows localized "Authentication failed" message
 * - API 429 shows localized "Too many attempts" message
 * - Network errors show localized "Connection error" message
 * - WebAuthn user cancellation (NotAllowedError) shows appropriate message
 * - After any error: password input is cleared and re-focused, dialog remains open, isSubmitting is reset
 *
 * Requirements: 3.4, 4.5, 4.6, 5.4
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, act } from "@testing-library/react";
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
    };
    return translations[key] ?? key;
  },
  useLocale: () => "en",
}));

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("ReAuthDialog - Error Handling and Recovery (Task 4.4)", () => {
  const defaultProps = {
    open: true,
    onSuccess: vi.fn(),
    onCancel: vi.fn(),
    mode: "management" as const,
    spaceId: "space-123",
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockIsWebAuthnSupported.mockReturnValue(false);
    mockListCredentials.mockResolvedValue([]);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  // Helper to wait for credentials to load and password input to appear
  async function waitForPasswordInput() {
    await waitFor(() => {
      expect(screen.getByLabelText("Password")).toBeInTheDocument();
    });
  }

  // Helper to type password and submit
  async function typePasswordAndSubmit(value: string) {
    await waitForPasswordInput();
    const input = screen.getByLabelText("Password");
    fireEvent.change(input, { target: { value } });
    const submitBtn = screen.getByRole("button", { name: "Confirm" });
    await act(async () => {
      fireEvent.click(submitBtn);
    });
  }

  // ── Error Message Display ─────────────────────────────────────────────────

  describe("Req 3.4: API 401 shows localized 'Authentication failed' message", () => {
    it("displays 'Authentication failed' error on 401 response", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("wrongpassword");

      await waitFor(() => {
        const alert = screen.getByRole("alert");
        expect(alert).toHaveTextContent("Authentication failed. Please try again.");
      });
    });

    it("uses the t('authFailed') translation key for 401 errors", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("wrongpassword");

      await waitFor(() => {
        // The error message matches the authFailed translation
        expect(screen.getByRole("alert")).toHaveTextContent("Authentication failed. Please try again.");
      });
    });
  });

  describe("Req 3.4: API 429 shows localized 'Too many attempts' message", () => {
    it("displays 'Too many attempts' error on 429 response", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 429 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("password");

      await waitFor(() => {
        const alert = screen.getByRole("alert");
        expect(alert).toHaveTextContent("Too many attempts. Please try again later.");
      });
    });
  });

  describe("Req 3.4: Network errors show localized 'Connection error' message", () => {
    it("displays 'Connection error' on network failure (no response object)", async () => {
      mockApiPost.mockRejectedValue(new Error("Network Error"));

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("password");

      await waitFor(() => {
        const alert = screen.getByRole("alert");
        expect(alert).toHaveTextContent("Connection error. Please try again.");
      });
    });

    it("displays 'Connection error' on timeout error", async () => {
      const timeoutError = new Error("timeout of 30000ms exceeded");
      (timeoutError as any).code = "ECONNABORTED";
      mockApiPost.mockRejectedValue(timeoutError);

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("password");

      await waitFor(() => {
        const alert = screen.getByRole("alert");
        expect(alert).toHaveTextContent("Connection error. Please try again.");
      });
    });

    it("displays 'Connection error' on 500 server error", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 500 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("password");

      await waitFor(() => {
        const alert = screen.getByRole("alert");
        expect(alert).toHaveTextContent("Connection error. Please try again.");
      });
    });
  });

  describe("Req 4.5, 4.6: WebAuthn user cancellation (NotAllowedError) shows appropriate message", () => {
    beforeEach(() => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([
        { id: "cred-1", nickname: "Key", createdAt: "2024-01-01", lastUsedAt: null, isDisabled: false },
      ]);
    });

    it("displays 'Verification cancelled' when user cancels WebAuthn prompt (NotAllowedError)", async () => {
      // First call returns WebAuthn options, then navigator.credentials.get throws NotAllowedError
      mockApiPost.mockResolvedValueOnce({
        data: { optionsJson: JSON.stringify({ challenge: "dGVzdA", allowCredentials: [] }), challengeId: "ch-1" },
      });

      // Mock navigator.credentials.get to throw NotAllowedError
      const mockCredentialsGet = vi.fn().mockRejectedValue(
        Object.assign(new Error("The operation either timed out or was not allowed."), { name: "NotAllowedError" })
      );
      Object.defineProperty(global.navigator, "credentials", {
        value: { get: mockCredentialsGet },
        writable: true,
        configurable: true,
      });

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Authenticate with fingerprint" })).toBeInTheDocument();
      });

      const webAuthnBtn = screen.getByRole("button", { name: "Authenticate with fingerprint" });
      await act(async () => {
        fireEvent.click(webAuthnBtn);
      });

      await waitFor(() => {
        const alert = screen.getByRole("alert");
        expect(alert).toHaveTextContent("Verification cancelled. Please try again.");
      });
    });

    it("resets isSubmitting after WebAuthn cancellation", async () => {
      mockApiPost.mockResolvedValueOnce({
        data: { optionsJson: JSON.stringify({ challenge: "dGVzdA", allowCredentials: [] }), challengeId: "ch-1" },
      });

      const mockCredentialsGet = vi.fn().mockRejectedValue(
        Object.assign(new Error("The operation either timed out or was not allowed."), { name: "NotAllowedError" })
      );
      Object.defineProperty(global.navigator, "credentials", {
        value: { get: mockCredentialsGet },
        writable: true,
        configurable: true,
      });

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Authenticate with fingerprint" })).toBeInTheDocument();
      });

      const webAuthnBtn = screen.getByRole("button", { name: "Authenticate with fingerprint" });
      await act(async () => {
        fireEvent.click(webAuthnBtn);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      // WebAuthn button should be re-enabled after cancellation
      const reEnabledBtn = screen.getByRole("button", { name: "Authenticate with fingerprint" });
      expect(reEnabledBtn).not.toBeDisabled();
    });

    it("dialog remains open after WebAuthn cancellation", async () => {
      mockApiPost.mockResolvedValueOnce({
        data: { optionsJson: JSON.stringify({ challenge: "dGVzdA", allowCredentials: [] }), challengeId: "ch-1" },
      });

      const mockCredentialsGet = vi.fn().mockRejectedValue(
        Object.assign(new Error("The operation either timed out or was not allowed."), { name: "NotAllowedError" })
      );
      Object.defineProperty(global.navigator, "credentials", {
        value: { get: mockCredentialsGet },
        writable: true,
        configurable: true,
      });

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Authenticate with fingerprint" })).toBeInTheDocument();
      });

      const webAuthnBtn = screen.getByRole("button", { name: "Authenticate with fingerprint" });
      await act(async () => {
        fireEvent.click(webAuthnBtn);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      // Dialog should still be visible
      expect(screen.getByRole("dialog")).toBeInTheDocument();
      // onSuccess should NOT have been called
      expect(defaultProps.onSuccess).not.toHaveBeenCalled();
      // onCancel should NOT have been called
      expect(defaultProps.onCancel).not.toHaveBeenCalled();
    });

    it("displays 'Authentication failed' when WebAuthn verification fails with 401", async () => {
      mockApiPost
        .mockResolvedValueOnce({
          data: { optionsJson: JSON.stringify({ challenge: "dGVzdA", allowCredentials: [] }), challengeId: "ch-1" },
        })
        .mockRejectedValueOnce({ response: { status: 401 } });

      // Mock successful navigator.credentials.get
      const mockAssertion = {
        id: "cred-id",
        rawId: new ArrayBuffer(8),
        type: "public-key",
        response: {
          authenticatorData: new ArrayBuffer(8),
          clientDataJSON: new ArrayBuffer(8),
          signature: new ArrayBuffer(8),
          userHandle: new ArrayBuffer(8),
        },
      };
      const mockCredentialsGet = vi.fn().mockResolvedValue(mockAssertion);
      Object.defineProperty(global.navigator, "credentials", {
        value: { get: mockCredentialsGet },
        writable: true,
        configurable: true,
      });

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Authenticate with fingerprint" })).toBeInTheDocument();
      });

      const webAuthnBtn = screen.getByRole("button", { name: "Authenticate with fingerprint" });
      await act(async () => {
        fireEvent.click(webAuthnBtn);
      });

      await waitFor(() => {
        const alert = screen.getByRole("alert");
        expect(alert).toHaveTextContent("Authentication failed. Please try again.");
      });
    });

    it("displays 'Too many attempts' when WebAuthn verification returns 429", async () => {
      mockApiPost
        .mockResolvedValueOnce({
          data: { optionsJson: JSON.stringify({ challenge: "dGVzdA", allowCredentials: [] }), challengeId: "ch-1" },
        })
        .mockRejectedValueOnce({ response: { status: 429 } });

      const mockAssertion = {
        id: "cred-id",
        rawId: new ArrayBuffer(8),
        type: "public-key",
        response: {
          authenticatorData: new ArrayBuffer(8),
          clientDataJSON: new ArrayBuffer(8),
          signature: new ArrayBuffer(8),
          userHandle: new ArrayBuffer(8),
        },
      };
      const mockCredentialsGet = vi.fn().mockResolvedValue(mockAssertion);
      Object.defineProperty(global.navigator, "credentials", {
        value: { get: mockCredentialsGet },
        writable: true,
        configurable: true,
      });

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Authenticate with fingerprint" })).toBeInTheDocument();
      });

      const webAuthnBtn = screen.getByRole("button", { name: "Authenticate with fingerprint" });
      await act(async () => {
        fireEvent.click(webAuthnBtn);
      });

      await waitFor(() => {
        const alert = screen.getByRole("alert");
        expect(alert).toHaveTextContent("Too many attempts. Please try again later.");
      });
    });
  });

  // ── Recovery Behavior ─────────────────────────────────────────────────────

  describe("Req 5.4: After password error - password cleared, re-focused, dialog open, isSubmitting reset", () => {
    it("clears password input after 401 error", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("wrongpassword");

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      const input = screen.getByLabelText("Password") as HTMLInputElement;
      expect(input.value).toBe("");
    });

    it("clears password input after 429 error", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 429 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("password");

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      const input = screen.getByLabelText("Password") as HTMLInputElement;
      expect(input.value).toBe("");
    });

    it("clears password input after network error", async () => {
      mockApiPost.mockRejectedValue(new Error("Network Error"));

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("password");

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      const input = screen.getByLabelText("Password") as HTMLInputElement;
      expect(input.value).toBe("");
    });

    it("re-focuses password input after error", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("wrongpassword");

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      // Advance timers to trigger the setTimeout for focus
      await act(async () => {
        vi.advanceTimersByTime(100);
      });

      const input = screen.getByLabelText("Password");
      expect(document.activeElement).toBe(input);
    });

    it("re-focuses password input after 429 error", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 429 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("password");

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      await act(async () => {
        vi.advanceTimersByTime(100);
      });

      const input = screen.getByLabelText("Password");
      expect(document.activeElement).toBe(input);
    });

    it("re-focuses password input after network error", async () => {
      mockApiPost.mockRejectedValue(new Error("Network Error"));

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("password");

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      await act(async () => {
        vi.advanceTimersByTime(100);
      });

      const input = screen.getByLabelText("Password");
      expect(document.activeElement).toBe(input);
    });

    it("dialog remains open after any error (onSuccess and onCancel not called)", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("wrongpassword");

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      expect(screen.getByRole("dialog")).toBeInTheDocument();
      expect(defaultProps.onSuccess).not.toHaveBeenCalled();
      expect(defaultProps.onCancel).not.toHaveBeenCalled();
    });

    it("resets isSubmitting after error (inputs re-enabled)", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("wrongpassword");

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      // Password input should be re-enabled
      const input = screen.getByLabelText("Password");
      expect(input).not.toBeDisabled();

      // Submit button should be present (though disabled because password is empty)
      const submitBtn = screen.getByRole("button", { name: "Confirm" });
      expect(submitBtn).toBeInTheDocument();

      // Cancel button should be re-enabled
      const cancelBtn = screen.getByRole("button", { name: "Cancel" });
      expect(cancelBtn).not.toBeDisabled();
    });

    it("allows retry after error - user can type new password and resubmit", async () => {
      mockApiPost
        .mockRejectedValueOnce({ response: { status: 401 } })
        .mockResolvedValueOnce({ data: { success: true } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("wrongpassword");

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      // Type new password
      const input = screen.getByLabelText("Password");
      fireEvent.change(input, { target: { value: "correctpassword" } });

      // Submit again
      const submitBtn = screen.getByRole("button", { name: "Confirm" });
      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(defaultProps.onSuccess).toHaveBeenCalledTimes(1);
      });
    });

    it("clears previous error message when user retries", async () => {
      mockApiPost
        .mockRejectedValueOnce({ response: { status: 401 } })
        .mockResolvedValueOnce({ data: { success: true } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("wrongpassword");

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      // Type new password and submit
      const input = screen.getByLabelText("Password");
      fireEvent.change(input, { target: { value: "correctpassword" } });

      const submitBtn = screen.getByRole("button", { name: "Confirm" });
      await act(async () => {
        fireEvent.click(submitBtn);
      });

      // Error should be cleared during submission
      await waitFor(() => {
        expect(screen.queryByRole("alert")).not.toBeInTheDocument();
      });
    });
  });

  describe("Error message uses role='alert' with aria-live='assertive'", () => {
    it("error message has role='alert' for screen reader announcement", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePasswordAndSubmit("wrongpassword");

      await waitFor(() => {
        const alert = screen.getByRole("alert");
        expect(alert).toHaveAttribute("aria-live", "assertive");
      });
    });
  });
});
