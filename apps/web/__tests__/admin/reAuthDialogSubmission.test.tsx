/**
 * Unit tests for ReAuthDialog loading and submission state handling.
 *
 * Verifies:
 * - `isSubmitting` state disables all form inputs and prevents duplicate submissions
 * - Loading indicator is displayed during API call
 * - On success: dialog closes, `onSuccess` is called
 * - On failure: error message shown, password cleared, inputs re-enabled, dialog stays open
 *
 * Requirements: 5.1, 5.2, 5.3, 5.4
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

// ── Helpers ───────────────────────────────────────────────────────────────────

function createDeferredPromise<T = any>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: any) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("ReAuthDialog - Loading and Submission State Handling (Task 3.5)", () => {
  const defaultProps = {
    open: true,
    onSuccess: vi.fn(),
    onCancel: vi.fn(),
    mode: "management" as const,
    spaceId: "space-123",
  };

  beforeEach(() => {
    vi.clearAllMocks();
    // Default: WebAuthn not supported, password only
    mockIsWebAuthnSupported.mockReturnValue(false);
    mockListCredentials.mockResolvedValue([]);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // Helper to wait for credentials to load and password input to appear
  async function waitForPasswordInput() {
    await waitFor(() => {
      expect(screen.getByLabelText("Password")).toBeInTheDocument();
    });
  }

  // Helper to type password and get the input element
  async function typePassword(value: string) {
    await waitForPasswordInput();
    const input = screen.getByLabelText("Password");
    fireEvent.change(input, { target: { value } });
    return input;
  }

  describe("Req 5.1: Loading indicator and disabled inputs during submission", () => {
    it("disables password input while submitting", async () => {
      const deferred = createDeferredPromise();
      mockApiPost.mockReturnValue(deferred.promise);

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("mypassword");

      const input = screen.getByLabelText("Password");
      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      // Submit the form
      await act(async () => {
        fireEvent.click(submitBtn);
      });

      // Input should be disabled during submission
      expect(input).toBeDisabled();

      // Resolve to clean up
      await act(async () => {
        deferred.resolve({ data: { success: true } });
      });
    });

    it("disables submit button while submitting", async () => {
      const deferred = createDeferredPromise();
      mockApiPost.mockReturnValue(deferred.promise);

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("mypassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      // Submit button should be disabled
      const verifyingBtn = screen.getByRole("button", { name: "Verifying..." });
      expect(verifyingBtn).toBeDisabled();

      await act(async () => {
        deferred.resolve({ data: { success: true } });
      });
    });

    it("disables cancel button while submitting", async () => {
      const deferred = createDeferredPromise();
      mockApiPost.mockReturnValue(deferred.promise);

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("mypassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      // Cancel button should be disabled
      const cancelBtn = screen.getByRole("button", { name: "Cancel" });
      expect(cancelBtn).toBeDisabled();

      await act(async () => {
        deferred.resolve({ data: { success: true } });
      });
    });

    it("disables close (X) button while submitting", async () => {
      const deferred = createDeferredPromise();
      mockApiPost.mockReturnValue(deferred.promise);

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("mypassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      // Close button should be disabled
      const closeBtn = screen.getByRole("button", { name: "Close" });
      expect(closeBtn).toBeDisabled();

      await act(async () => {
        deferred.resolve({ data: { success: true } });
      });
    });

    it("disables WebAuthn button while submitting via password", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([
        { id: "cred-1", nickname: "Key", createdAt: "2024-01-01", lastUsedAt: null, isDisabled: false },
      ]);

      const deferred = createDeferredPromise();
      mockApiPost.mockReturnValue(deferred.promise);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Authenticate with fingerprint" })).toBeInTheDocument();
      });

      await typePassword("mypassword");
      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      // WebAuthn button should be disabled
      const webAuthnBtn = screen.getByRole("button", { name: "Authenticate with fingerprint" });
      expect(webAuthnBtn).toBeDisabled();

      await act(async () => {
        deferred.resolve({ data: { success: true } });
      });
    });

    it("shows 'Verifying...' text on submit button during submission", async () => {
      const deferred = createDeferredPromise();
      mockApiPost.mockReturnValue(deferred.promise);

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("mypassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      // Button text should change to loading indicator
      expect(screen.getByRole("button", { name: "Verifying..." })).toBeInTheDocument();

      await act(async () => {
        deferred.resolve({ data: { success: true } });
      });
    });

    it("shows 'Verifying...' text on WebAuthn button during WebAuthn submission", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([
        { id: "cred-1", nickname: "Key", createdAt: "2024-01-01", lastUsedAt: null, isDisabled: false },
      ]);

      const deferred = createDeferredPromise();
      mockApiPost.mockReturnValue(deferred.promise);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Authenticate with fingerprint" })).toBeInTheDocument();
      });

      const webAuthnBtn = screen.getByRole("button", { name: "Authenticate with fingerprint" });

      await act(async () => {
        fireEvent.click(webAuthnBtn);
      });

      // Both buttons should show "Verifying..." (submit button + WebAuthn button)
      await waitFor(() => {
        const verifyingButtons = screen.getAllByText("Verifying...");
        expect(verifyingButtons.length).toBeGreaterThanOrEqual(1);
      });

      await act(async () => {
        deferred.resolve({ data: { optionsJson: "{}", challengeId: "ch-1" } });
      });
    });
  });

  describe("Req 5.2: Prevent duplicate submissions", () => {
    it("prevents duplicate password submissions while already submitting", async () => {
      const deferred = createDeferredPromise();
      mockApiPost.mockReturnValue(deferred.promise);

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("mypassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      // First submission
      await act(async () => {
        fireEvent.click(submitBtn);
      });

      // The submit button should now be disabled, preventing further clicks
      const disabledBtn = screen.getByRole("button", { name: "Verifying..." });
      expect(disabledBtn).toBeDisabled();

      // Only one API call should have been made
      expect(mockApiPost).toHaveBeenCalledTimes(1);

      await act(async () => {
        deferred.resolve({ data: { success: true } });
      });
    });

    it("prevents WebAuthn submission while password submission is in progress", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([
        { id: "cred-1", nickname: "Key", createdAt: "2024-01-01", lastUsedAt: null, isDisabled: false },
      ]);

      const deferred = createDeferredPromise();
      mockApiPost.mockReturnValue(deferred.promise);

      render(<ReAuthDialog {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Authenticate with fingerprint" })).toBeInTheDocument();
      });

      await typePassword("mypassword");
      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      // Submit password
      await act(async () => {
        fireEvent.click(submitBtn);
      });

      // Try WebAuthn while password is submitting (button is disabled)
      const webAuthnBtn = screen.getByRole("button", { name: "Authenticate with fingerprint" });
      expect(webAuthnBtn).toBeDisabled();

      // Only one API call (the password one)
      expect(mockApiPost).toHaveBeenCalledTimes(1);

      await act(async () => {
        deferred.resolve({ data: { success: true } });
      });
    });
  });

  describe("Req 5.3: On success - dialog closes, onSuccess is called", () => {
    it("calls onSuccess when API returns success", async () => {
      mockApiPost.mockResolvedValue({ data: { success: true } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("correctpassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(defaultProps.onSuccess).toHaveBeenCalledTimes(1);
      });
    });

    it("does not show error message on success", async () => {
      mockApiPost.mockResolvedValue({ data: { success: true } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("correctpassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(defaultProps.onSuccess).toHaveBeenCalled();
      });

      expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    });

    it("does not call onCancel on success", async () => {
      mockApiPost.mockResolvedValue({ data: { success: true } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("correctpassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(defaultProps.onSuccess).toHaveBeenCalled();
      });

      expect(defaultProps.onCancel).not.toHaveBeenCalled();
    });
  });

  describe("Req 5.4: On failure - error shown, password cleared, inputs re-enabled, dialog stays open", () => {
    it("shows error message on 401 failure", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("wrongpassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toHaveTextContent("Authentication failed. Please try again.");
      });
    });

    it("shows rate limit error on 429 failure", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 429 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("password");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toHaveTextContent("Too many attempts. Please try again later.");
      });
    });

    it("shows network error on other failures", async () => {
      mockApiPost.mockRejectedValue(new Error("Network Error"));

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("password");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toHaveTextContent("Connection error. Please try again.");
      });
    });

    it("clears password input on failure", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("wrongpassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      // Password should be cleared
      const input = screen.getByLabelText("Password") as HTMLInputElement;
      expect(input.value).toBe("");
    });

    it("re-enables all inputs after failure", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("wrongpassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      // All inputs should be re-enabled
      const input = screen.getByLabelText("Password");
      expect(input).not.toBeDisabled();

      // Cancel button should be re-enabled
      const cancelBtn = screen.getByRole("button", { name: "Cancel" });
      expect(cancelBtn).not.toBeDisabled();
    });

    it("dialog stays open after failure (onSuccess not called)", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("wrongpassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      // onSuccess should NOT have been called
      expect(defaultProps.onSuccess).not.toHaveBeenCalled();
      // onCancel should NOT have been called
      expect(defaultProps.onCancel).not.toHaveBeenCalled();
      // Dialog should still be visible
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });

    it("allows retry after failure", async () => {
      // First call fails, second succeeds
      mockApiPost
        .mockRejectedValueOnce({ response: { status: 401 } })
        .mockResolvedValueOnce({ data: { success: true } });

      render(<ReAuthDialog {...defaultProps} />);
      await typePassword("wrongpassword");

      const submitBtn = screen.getByRole("button", { name: "Confirm" });

      // First attempt - fails
      await act(async () => {
        fireEvent.click(submitBtn);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toBeInTheDocument();
      });

      // Type new password and retry
      const input = screen.getByLabelText("Password");
      fireEvent.change(input, { target: { value: "correctpassword" } });

      const retryBtn = screen.getByRole("button", { name: "Confirm" });
      await act(async () => {
        fireEvent.click(retryBtn);
      });

      // Second attempt should succeed
      await waitFor(() => {
        expect(defaultProps.onSuccess).toHaveBeenCalledTimes(1);
      });

      expect(mockApiPost).toHaveBeenCalledTimes(2);
    });
  });
});
