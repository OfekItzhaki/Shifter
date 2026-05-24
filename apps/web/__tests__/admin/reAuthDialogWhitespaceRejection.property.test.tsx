/**
 * Property-based test for whitespace password rejection in ReAuthDialog.
 *
 * Feature: admin-reauth-security
 * Property 2: Whitespace password rejection
 *
 * **Validates: Requirements 3.4**
 *
 * For any string composed entirely of whitespace characters (spaces, tabs, newlines, etc.),
 * attempting to submit the re-auth password form SHALL be prevented, the form SHALL NOT
 * call the Re_Auth_Endpoint, and a validation error SHALL be displayed.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import * as fc from "fast-check";
import { render, screen, waitFor, fireEvent, act } from "@testing-library/react";
import React from "react";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockApiPost = vi.fn();
const mockApiGet = vi.fn();

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    post: (...args: any[]) => mockApiPost(...args),
    get: (...args: any[]) => mockApiGet(...args),
  },
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, params?: any) => {
    const translations: Record<string, string> = {
      title: "Confirm Identity",
      description: "Please confirm your identity to continue.",
      passwordLabel: "Password",
      passwordPlaceholder: "Enter your password",
      passwordRequired: "Password is required",
      confirm: "Confirm",
      verifying: "Verifying...",
      webAuthnButton: "Use Fingerprint",
      webAuthnVerifying: "Verifying...",
      or: "or",
      cancel: "Cancel",
      close: "Close",
      loadingCredentials: "Loading...",
      noCredentials: "No credentials configured.",
      invalidCredentials: "Invalid credentials",
      rateLimited: "Too many attempts. Please try again later.",
      connectionProblem: "Connection error. Please try again.",
      webAuthnCancelled: "Verification cancelled.",
      webAuthnTimedOut: "Authentication timed out.",
      webAuthnNotRecognized: "Credential not recognized.",
      usePasswordInstead: "Use password instead",
      lockedOut: `Account locked for ${params?.minutes ?? 15} minutes`,
    };
    return translations[key] ?? key;
  },
  useLocale: () => "en",
}));

// ── Arbitraries ───────────────────────────────────────────────────────────────

/**
 * Generates arbitrary whitespace-only strings composed of spaces, tabs,
 * newlines, carriage returns, form feeds, and vertical tabs.
 */
const whitespaceOnlyArb = fc.stringOf(
  fc.constantFrom(" ", "\t", "\n", "\r", "\f", "\v", "  ", "\t\t", "\n\n"),
  { minLength: 1, maxLength: 50 }
);

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("Feature: admin-reauth-security — Property 2: Whitespace password rejection", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Mock credential check to return empty (password-only mode)
    mockApiGet.mockResolvedValue({ data: [] });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  /**
   * Helper: waits for the credential check to complete and the password form to render.
   */
  async function waitForPasswordForm() {
    await waitFor(() => {
      expect(screen.getByLabelText("Password")).toBeInTheDocument();
    });
  }

  it("for any whitespace-only string, form submission is prevented, no API call is made, and a validation error is displayed", async () => {
    // We run the property test with numRuns: 100 as required.
    // Each iteration renders the dialog, types a whitespace string, submits, and asserts.
    await fc.assert(
      fc.asyncProperty(whitespaceOnlyArb, async (whitespacePassword) => {
        vi.clearAllMocks();
        mockApiGet.mockResolvedValue({ data: [] });

        const onSuccess = vi.fn();
        const onCancel = vi.fn();

        const { unmount } = render(
          React.createElement(
            (await import("../../components/admin/ReAuthDialog")).default,
            {
              open: true,
              onSuccess,
              onCancel,
              mode: "management" as const,
              spaceId: "space-test",
            }
          )
        );

        // Wait for credential check to complete and password form to appear
        await waitForPasswordForm();

        const passwordInput = screen.getByLabelText("Password");

        // Focus the input to remove readonly (autofill prevention trick)
        fireEvent.focus(passwordInput);

        // Type the whitespace-only password
        fireEvent.change(passwordInput, { target: { value: whitespacePassword } });

        // Submit the form
        const form = passwordInput.closest("form")!;
        await act(async () => {
          fireEvent.submit(form);
        });

        // Assert 1: No API call was made to the re-authenticate endpoint
        const reAuthCalls = mockApiPost.mock.calls.filter(
          (call: any[]) => call[0] === "/auth/re-authenticate"
        );
        expect(reAuthCalls).toHaveLength(0);

        // Assert 2: onSuccess was NOT called (submission was prevented)
        expect(onSuccess).not.toHaveBeenCalled();

        // Assert 3: A validation error is displayed
        const validationError = screen.getByRole("alert");
        expect(validationError).toBeInTheDocument();
        expect(validationError).toHaveTextContent("Password is required");

        unmount();
      }),
      { numRuns: 100 }
    );
  });
});
