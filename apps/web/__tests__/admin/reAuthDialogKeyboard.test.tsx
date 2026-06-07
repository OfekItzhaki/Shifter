/**
 * Unit tests for ReAuthDialog keyboard submission and Tab navigation.
 *
 * Verifies:
 * - Enter key in password input triggers form submission
 * - Tab navigation works correctly between password input, WebAuthn button, submit button, and cancel button
 *
 * Requirements: 3.6, 6.5
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, act } from "@testing-library/react";
import ReAuthDialog from "../../components/admin/ReAuthDialog";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockApiPost = vi.fn();
const mockApiGet = vi.fn();

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    post: (...args: any[]) => mockApiPost(...args),
    get: (...args: any[]) => mockApiGet(...args),
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
      invalidCredentials: "Authentication failed. Please try again.",
      connectionProblem: "Connection error. Please try again.",
      webAuthnCancelled: "Verification cancelled. Please try again.",
      webAuthnVerifying: "Verifying...",
      usePasswordInstead: "Use password instead",
      useBiometricInstead: "Use biometric instead",
      webAuthnNotRecognized: "Biometric sign-in was not recognized. Please try again or use your password.",
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

describe("ReAuthDialog - Keyboard Submission and Tab Navigation (Task 4.3)", () => {
  const defaultProps = {
    open: true,
    onSuccess: vi.fn(),
    onCancel: vi.fn(),
    mode: "management" as const,
    spaceId: "space-123",
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockIsWebAuthnSupported.mockReturnValue(false);
    mockListCredentials.mockResolvedValue([]);
    Object.defineProperty(global.navigator, "credentials", {
      value: { get: vi.fn() },
      writable: true,
      configurable: true,
    });
    Object.defineProperty(window, "PublicKeyCredential", {
      value: {
        isUserVerifyingPlatformAuthenticatorAvailable: vi.fn().mockResolvedValue(true),
      },
      writable: true,
      configurable: true,
    });
    mockApiGet.mockImplementation(async () => ({ data: await mockListCredentials() }));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function waitForPasswordInput() {
    await waitFor(() => {
      expect(screen.getByLabelText("Password")).toBeInTheDocument();
    });
  }

  async function typePassword(value: string) {
    await waitForPasswordInput();
    const input = screen.getByLabelText("Password");
    fireEvent.change(input, { target: { value } });
    return input;
  }

  async function switchToPassword() {
    fireEvent.click(await screen.findByRole("button", { name: "Use password instead" }));
    await waitForPasswordInput();
  }

  // ── Enter Key Submission (Req 3.6) ─────────────────────────────────────────

  describe("Req 3.6: Enter key triggers form submission", () => {
    it("submits the form when Enter is pressed in the password input", async () => {
      mockApiPost.mockResolvedValue({ data: { success: true } });

      render(<ReAuthDialog {...defaultProps} />);
      const input = await typePassword("mypassword");

      // Simulate pressing Enter in the password input (native form submission)
      await act(async () => {
        fireEvent.submit(input.closest("form")!);
      });

      await waitFor(() => {
        expect(mockApiPost).toHaveBeenCalledTimes(1);
        expect(mockApiPost).toHaveBeenCalledWith("/auth/re-authenticate", {
          password: "mypassword",
          spaceId: "space-123",
        });
      });
    });

    it("calls onSuccess after successful Enter key submission", async () => {
      mockApiPost.mockResolvedValue({ data: { success: true } });

      render(<ReAuthDialog {...defaultProps} />);
      const input = await typePassword("correctpassword");

      await act(async () => {
        fireEvent.submit(input.closest("form")!);
      });

      await waitFor(() => {
        expect(defaultProps.onSuccess).toHaveBeenCalledTimes(1);
      });
    });

    it("shows error after failed Enter key submission", async () => {
      mockApiPost.mockRejectedValue({ response: { status: 401 } });

      render(<ReAuthDialog {...defaultProps} />);
      const input = await typePassword("wrongpassword");

      await act(async () => {
        fireEvent.submit(input.closest("form")!);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toHaveTextContent(
          "Authentication failed. Please try again."
        );
      });
    });

    it("does not submit when password is empty", async () => {
      render(<ReAuthDialog {...defaultProps} />);
      await waitForPasswordInput();

      const input = screen.getByLabelText("Password");
      // Password is empty (whitespace only)
      fireEvent.change(input, { target: { value: "   " } });

      await act(async () => {
        fireEvent.submit(input.closest("form")!);
      });

      // API should NOT be called because password.trim() is empty
      expect(mockApiPost).not.toHaveBeenCalled();
    });

    it("does not submit when already submitting (Enter key during loading)", async () => {
      const deferred = createDeferredPromise();
      mockApiPost.mockReturnValue(deferred.promise);

      render(<ReAuthDialog {...defaultProps} />);
      const input = await typePassword("mypassword");

      // First submission
      await act(async () => {
        fireEvent.submit(input.closest("form")!);
      });

      // Try submitting again while first is in progress
      await act(async () => {
        fireEvent.submit(input.closest("form")!);
      });

      // Only one API call should have been made
      expect(mockApiPost).toHaveBeenCalledTimes(1);

      await act(async () => {
        deferred.resolve({ data: { success: true } });
      });
    });
  });

  // ── Tab Navigation (Req 6.5) ───────────────────────────────────────────────

  describe("Req 6.5: Tab navigation between interactive elements", () => {
    it("Tab navigation includes all interactive elements (password only)", async () => {
      render(<ReAuthDialog {...defaultProps} />);
      // Type a password so the submit button becomes enabled
      await typePassword("mypassword");

      const dialog = screen.getByRole("dialog");
      const focusableElements = dialog.querySelectorAll<HTMLElement>(
        'button:not([disabled]):not([tabindex="-1"]), input:not([disabled]):not([tabindex="-1"]), [tabindex]:not([tabindex="-1"])'
      );

      // Should have: close button, password input, submit button, cancel button
      expect(focusableElements.length).toBe(4);

      // Verify the elements are in logical order
      const elementTypes = Array.from(focusableElements).map((el) => {
        if (el.tagName === "INPUT") return "password-input";
        if (el.getAttribute("aria-label") === "Close") return "close-button";
        if (el.textContent === "Confirm") return "submit-button";
        if (el.textContent === "Cancel") return "cancel-button";
        return el.tagName;
      });

      expect(elementTypes).toEqual([
        "close-button",
        "password-input",
        "submit-button",
        "cancel-button",
      ]);
    });

    it("Tab navigation includes biometric fallback when available in password mode", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([
        { id: "cred-1", nickname: "Key", createdAt: "2024-01-01", lastUsedAt: null, isDisabled: false },
      ]);

      render(<ReAuthDialog {...defaultProps} />);

      expect(await screen.findByRole("button", { name: "Use Fingerprint" })).toBeInTheDocument();
      await switchToPassword();

      // Type a password so the submit button becomes enabled
      const input = screen.getByLabelText("Password");
      fireEvent.change(input, { target: { value: "mypassword" } });

      const dialog = screen.getByRole("dialog");
      const focusableElements = dialog.querySelectorAll<HTMLElement>(
        'button:not([disabled]):not([tabindex="-1"]), input:not([disabled]):not([tabindex="-1"]), [tabindex]:not([tabindex="-1"])'
      );

      // Should have: close button, password input, submit button, biometric fallback, cancel button
      expect(focusableElements.length).toBe(5);

      // Verify the elements are in logical order
      const elementTypes = Array.from(focusableElements).map((el) => {
        if (el.tagName === "INPUT") return "password-input";
        if (el.getAttribute("aria-label") === "Close") return "close-button";
        if (el.textContent === "Confirm") return "submit-button";
        if (el.textContent === "Use biometric instead") return "webauthn-switch-button";
        if (el.textContent === "Cancel") return "cancel-button";
        return el.tagName;
      });

      expect(elementTypes).toEqual([
        "close-button",
        "password-input",
        "submit-button",
        "webauthn-switch-button",
        "cancel-button",
      ]);
    });

    it("Tab wraps from last element (cancel) to first element (close)", async () => {
      render(<ReAuthDialog {...defaultProps} />);
      await waitForPasswordInput();

      const dialog = screen.getByRole("dialog");
      const cancelButton = screen.getByRole("button", { name: "Cancel" });

      // Focus the cancel button (last focusable element)
      cancelButton.focus();
      expect(document.activeElement).toBe(cancelButton);

      // Press Tab — should wrap to first focusable element (close button)
      fireEvent.keyDown(dialog, { key: "Tab", shiftKey: false });

      const focusableElements = dialog.querySelectorAll<HTMLElement>(
        'button:not([disabled]):not([tabindex="-1"]), input:not([disabled]):not([tabindex="-1"]), [tabindex]:not([tabindex="-1"])'
      );
      expect(document.activeElement).toBe(focusableElements[0]);
    });

    it("Shift+Tab wraps from first element (close) to last element (cancel)", async () => {
      render(<ReAuthDialog {...defaultProps} />);
      await waitForPasswordInput();

      const dialog = screen.getByRole("dialog");
      const focusableElements = dialog.querySelectorAll<HTMLElement>(
        'button:not([disabled]):not([tabindex="-1"]), input:not([disabled]):not([tabindex="-1"]), [tabindex]:not([tabindex="-1"])'
      );
      const firstElement = focusableElements[0];
      const lastElement = focusableElements[focusableElements.length - 1];

      // Focus the first element (close button)
      firstElement.focus();
      expect(document.activeElement).toBe(firstElement);

      // Press Shift+Tab — should wrap to last focusable element (cancel button)
      fireEvent.keyDown(dialog, { key: "Tab", shiftKey: true });

      expect(document.activeElement).toBe(lastElement);
    });

    it("Tab does not escape the dialog container", async () => {
      mockIsWebAuthnSupported.mockReturnValue(true);
      mockListCredentials.mockResolvedValue([
        { id: "cred-1", nickname: "Key", createdAt: "2024-01-01", lastUsedAt: null, isDisabled: false },
      ]);

      render(<ReAuthDialog {...defaultProps} />);

      expect(await screen.findByRole("button", { name: "Use Fingerprint" })).toBeInTheDocument();
      await switchToPassword();

      const dialog = screen.getByRole("dialog");
      const focusableElements = dialog.querySelectorAll<HTMLElement>(
        'button:not([disabled]):not([tabindex="-1"]), input:not([disabled]):not([tabindex="-1"]), [tabindex]:not([tabindex="-1"])'
      );

      // Cycle through all elements with Tab, verify focus stays inside
      for (let i = 0; i < focusableElements.length + 1; i++) {
        const lastEl = focusableElements[focusableElements.length - 1];
        lastEl.focus();
        fireEvent.keyDown(dialog, { key: "Tab", shiftKey: false });

        // After wrapping, focus should be on the first element
        expect(dialog.contains(document.activeElement)).toBe(true);
      }
    });
  });
});
