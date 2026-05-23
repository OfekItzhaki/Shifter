/**
 * Property-based test: No DOM rendering when closed.
 *
 * Feature: admin-reauth-security
 * Property 3: No DOM rendering when closed
 *
 * For any prop combination with `open=false`, the component renders no DOM elements.
 *
 * **Validates: Requirements 5.3**
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import * as fc from "fast-check";
import { render } from "@testing-library/react";
import React from "react";
import ReAuthDialog from "../../components/admin/ReAuthDialog";

// ── Mocks ─────────────────────────────────────────────────────────────────────

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
      webAuthnTimedOut: "Authentication timed out.",
      webAuthnNotRecognized: "Credential not recognized.",
      webAuthnVerifying: "Verifying...",
      usePasswordInstead: "Use password instead",
      passwordRequired: "Password is required",
      invalidCredentials: "Invalid credentials",
      connectionProblem: "Connection problem",
      lockedOut: "Too many attempts. Try again in {minutes} minutes.",
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

// ── Generators ────────────────────────────────────────────────────────────────

/** Generate arbitrary mode values */
const modeArb = fc.constantFrom("management" as const, "platform" as const);

/** Generate arbitrary spaceId values (optional) */
const spaceIdArb = fc.oneof(
  fc.constant(undefined),
  fc.string({ minLength: 1, maxLength: 50 })
);

/** Generate arbitrary callback functions */
const callbackArb = fc.constant(() => {});

// ── Property Tests ────────────────────────────────────────────────────────────

describe("Feature: admin-reauth-security, Property 3: No DOM rendering when closed", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("for any prop combination with open=false, the component renders no DOM elements", () => {
    fc.assert(
      fc.property(
        modeArb,
        spaceIdArb,
        callbackArb,
        callbackArb,
        (mode, spaceId, onSuccess, onCancel) => {
          const { container } = render(
            React.createElement(ReAuthDialog, {
              open: false,
              mode,
              spaceId,
              onSuccess,
              onCancel,
            })
          );

          // Assert: container is empty — no DOM elements rendered
          expect(container.innerHTML).toBe("");
          expect(container.childElementCount).toBe(0);
        }
      ),
      { numRuns: 100 }
    );
  });
});
