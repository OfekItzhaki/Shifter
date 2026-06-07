/**
 * Property-based test for ReAuthDialog state reset on open.
 *
 * Feature: admin-reauth-security
 * Property 1: Dialog state reset on open
 *
 * For any previous password value, when dialog transitions from closed to open,
 * password field value is empty string.
 *
 * **Validates: Requirements 1.3**
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import * as fc from "fast-check";
import ReAuthDialog from "../../components/admin/ReAuthDialog";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockApiGet = vi.fn();
const mockApiPost = vi.fn();

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    get: (...args: any[]) => mockApiGet(...args),
    post: (...args: any[]) => mockApiPost(...args),
  },
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, params?: Record<string, any>) => {
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
      invalidCredentials: "Authentication failed. Please try again.",
      rateLimited: "Too many attempts. Please try again later.",
      connectionProblem: "Connection error. Please try again.",
      passwordRequired: "Password is required.",
      webAuthnCancelled: "Verification cancelled.",
      webAuthnTimedOut: "Verification timed out.",
      webAuthnNotRecognized: "Credential not recognized.",
      lockedOut: `Too many attempts. Try again in ${params?.minutes ?? ""} minutes.`,
      usePasswordInstead: "Use password instead",
      useBiometricInstead: "Use biometric instead",
    };
    return translations[key] ?? key;
  },
  useLocale: () => "en",
}));

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("Feature: admin-reauth-security | Property 1: Dialog state reset on open", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Mock credential check to return no WebAuthn credentials (password-only mode)
    mockApiGet.mockResolvedValue({ data: [] });
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it("for any previous password value, when dialog transitions from closed to open, password field value is empty string", async () => {
    await fc.assert(
      fc.asyncProperty(
        fc.string({ minLength: 1, maxLength: 100 }),
        async (previousPassword) => {
          // Step 1: Render the dialog open
          const props = {
            open: true,
            onSuccess: vi.fn(),
            onCancel: vi.fn(),
            mode: "management" as const,
            spaceId: "space-test",
          };

          const { rerender, unmount } = render(<ReAuthDialog {...props} />);

          // Wait for credential check to complete and password input to appear
          await waitFor(() => {
            expect(screen.getByLabelText("Password")).toBeInTheDocument();
          });

          // Step 2: Type the generated string into the password field
          const input = screen.getByLabelText("Password") as HTMLInputElement;
          fireEvent.change(input, { target: { value: previousPassword } });

          // Verify the password was typed
          expect(input.value).toBe(previousPassword);

          // Step 3: Close the dialog (set open=false)
          rerender(<ReAuthDialog {...props} open={false} />);

          // Step 4: Re-open the dialog (set open=true)
          rerender(<ReAuthDialog {...props} open={true} />);

          // Wait for credential check to complete and password input to appear again
          await waitFor(() => {
            expect(screen.getByLabelText("Password")).toBeInTheDocument();
          });

          // Step 5: Assert the password field value is empty string
          const resetInput = screen.getByLabelText("Password") as HTMLInputElement;
          expect(resetInput.value).toBe("");

          // Clean up
          unmount();
        }
      ),
      { numRuns: 100 }
    );
  }, 30000);
});
