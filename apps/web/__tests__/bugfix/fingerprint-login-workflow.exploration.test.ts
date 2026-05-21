/**
 * Bug Condition Exploration Property Test
 *
 * Feature: fingerprint-login-workflow
 * Task: 1 - Write bug condition exploration test
 *
 * **Validates: Requirements 1.1, 1.2**
 *
 * CRITICAL: These tests are EXPECTED TO FAIL on unfixed code.
 * Failure confirms the bugs exist. DO NOT fix the code or tests when they fail.
 *
 * Bug Conditions:
 * - Bug 1: After login(), listCredentials() is called but the request lacks a valid
 *   Authorization header (token race condition). The handleSubmit function calls
 *   listCredentials() without passing the fresh token explicitly, relying on the
 *   apiClient interceptor which reads from localStorage — creating a race condition.
 * - Bug 2: ReAuthDialog renders with hasWebAuthn=true and credentialLoading=false,
 *   but navigator.credentials.get() is never called automatically. There is no
 *   useEffect that auto-triggers the WebAuthn flow.
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";
import * as fs from "fs";
import * as path from "path";

// ── Source file paths ─────────────────────────────────────────────────────────

const LOGIN_PAGE_PATH = path.resolve(
  __dirname,
  "../../app/login/page.tsx"
);
const REAUTH_DIALOG_PATH = path.resolve(
  __dirname,
  "../../components/admin/ReAuthDialog.tsx"
);
const WEBAUTHN_LIB_PATH = path.resolve(
  __dirname,
  "../../lib/webauthn.ts"
);

// ── Read source files ─────────────────────────────────────────────────────────

const loginPageSource = fs.readFileSync(LOGIN_PAGE_PATH, "utf-8");
const reAuthDialogSource = fs.readFileSync(REAUTH_DIALOG_PATH, "utf-8");
const webAuthnLibSource = fs.readFileSync(WEBAUTHN_LIB_PATH, "utf-8");

// ── Bug Condition Types ───────────────────────────────────────────────────────

type PostLoginScenario = {
  type: "post-login";
  webAuthnSupported: boolean;
  biometricPromptNotDismissed: boolean;
  existingCredentialCount: number;
};

type ReAuthDialogScenario = {
  type: "reauth-dialog";
  hasWebAuthn: boolean;
  credentialLoadingComplete: boolean;
};

type BugConditionInput = PostLoginScenario | ReAuthDialogScenario;

// ── Generators ────────────────────────────────────────────────────────────────

/**
 * Generate post-login scenarios where the bug condition holds:
 * - WebAuthn is supported
 * - Biometric prompt not dismissed
 * - User has no existing credentials (so prompt should appear)
 */
const postLoginBugConditionArb: fc.Arbitrary<PostLoginScenario> = fc.record({
  type: fc.constant("post-login" as const),
  webAuthnSupported: fc.constant(true),
  biometricPromptNotDismissed: fc.constant(true),
  existingCredentialCount: fc.constant(0),
});

/**
 * Generate ReAuthDialog scenarios where the bug condition holds:
 * - hasWebAuthn is true (user has registered credentials)
 * - Credential loading is complete
 */
const reAuthDialogBugConditionArb: fc.Arbitrary<ReAuthDialogScenario> = fc.record({
  type: fc.constant("reauth-dialog" as const),
  hasWebAuthn: fc.constant(true),
  credentialLoadingComplete: fc.constant(true),
});

const bugConditionArb: fc.Arbitrary<BugConditionInput> = fc.oneof(
  postLoginBugConditionArb,
  reAuthDialogBugConditionArb
);

// ── Property Tests ────────────────────────────────────────────────────────────

describe("Property 1: Bug Condition - Post-Login Token Race & ReAuthDialog Missing Auto-Trigger", () => {
  /**
   * Bug 1.1: Post-login listCredentials() must use fresh token explicitly
   *
   * Expected behavior: After login(), the handleSubmit function passes the fresh
   * access token directly to listCredentials() (or makes the API call with an
   * explicit Authorization header), rather than relying on the apiClient interceptor
   * to read it from localStorage (which creates a race condition).
   *
   * Current bug: handleSubmit calls `await listCredentials()` without any token
   * parameter, relying entirely on the interceptor's localStorage read.
   *
   * We verify this by checking that either:
   * - listCredentials() accepts a token parameter in webauthn.ts, OR
   * - The login page passes a token to listCredentials(), OR
   * - The login page makes the credentials API call with an explicit Authorization header
   */
  it("listCredentials() is called with explicit fresh token after login (not relying on interceptor)", () => {
    fc.assert(
      fc.property(postLoginBugConditionArb, (_input) => {
        // Check if listCredentials in webauthn.ts accepts an optional token parameter
        const listCredentialsFnMatch = webAuthnLibSource.match(
          /export async function listCredentials\(([^)]*)\)/
        );
        const listCredentialsParams = listCredentialsFnMatch?.[1]?.trim() ?? "";
        const acceptsTokenParam = listCredentialsParams.includes("token");

        // Check if the login page passes a token to listCredentials
        const passesTokenToListCredentials =
          loginPageSource.includes("listCredentials(") &&
          !loginPageSource.match(/listCredentials\(\s*\)/); // Has arguments, not empty parens

        // Check if the login page makes a direct API call with explicit Authorization header
        const usesExplicitAuthHeader =
          loginPageSource.includes("Authorization") &&
          loginPageSource.includes("Bearer") &&
          loginPageSource.includes("webauthn/credentials");

        // Expected behavior: at least one of these approaches is used
        const tokenPassedExplicitly =
          acceptsTokenParam || passesTokenToListCredentials || usesExplicitAuthHeader;

        expect(tokenPassedExplicitly).toBe(true);
      }),
      { numRuns: 10 }
    );
  });

  /**
   * Bug 1.1 (supplementary): The login page's handleSubmit retrieves the fresh
   * token from localStorage immediately after login and uses it for the
   * credentials check.
   *
   * Expected behavior: After `await login(email, password)`, the code reads
   * `localStorage.getItem("access_token")` and passes it to the credentials call.
   *
   * Current bug: The code calls `listCredentials()` without reading the fresh token.
   */
  it("handleSubmit reads fresh token from localStorage after login for credentials check", () => {
    fc.assert(
      fc.property(postLoginBugConditionArb, (_input) => {
        // Extract the handleSubmit function body
        const handleSubmitMatch = loginPageSource.match(
          /async function handleSubmit[\s\S]*?(?=\n\s*async function|\n\s*function\s|\n\s*return\s*\()/
        );
        const handleSubmitBody = handleSubmitMatch?.[0] ?? "";

        // Check that after login(), the code reads the fresh token
        const loginCallIndex = handleSubmitBody.indexOf("await login(");
        const afterLogin = handleSubmitBody.slice(loginCallIndex);

        // The code should read the fresh token after login
        const readsFreshToken =
          afterLogin.includes('localStorage.getItem("access_token")') ||
          afterLogin.includes("localStorage.getItem('access_token')");

        // And uses it in the credentials call (either passing to listCredentials or in a header)
        const usesTokenForCredentials =
          readsFreshToken ||
          afterLogin.includes("listCredentials(token") ||
          afterLogin.includes("listCredentials(freshToken") ||
          afterLogin.includes("listCredentials(accessToken");

        expect(usesTokenForCredentials).toBe(true);
      }),
      { numRuns: 10 }
    );
  });

  /**
   * Bug 1.2: ReAuthDialog must auto-trigger WebAuthn when hasWebAuthn is true
   *
   * Expected behavior: When the ReAuthDialog opens with hasWebAuthn=true and
   * credential loading is complete, a useEffect automatically calls
   * handleWebAuthnSubmit() (which internally calls navigator.credentials.get()).
   *
   * Current bug: The ReAuthDialog only renders the WebAuthn button and focuses
   * the password input. There is no useEffect that auto-triggers the WebAuthn flow.
   *
   * We verify this by checking that the ReAuthDialog source contains a useEffect
   * that calls handleWebAuthnSubmit() as a statement (not just as an onClick handler).
   * The auto-trigger must be a standalone call inside a useEffect body, not just
   * referenced in JSX onClick props.
   */
  it("ReAuthDialog has useEffect that auto-triggers handleWebAuthnSubmit when hasWebAuthn is true", () => {
    fc.assert(
      fc.property(reAuthDialogBugConditionArb, (_input) => {
        // Extract all useEffect bodies from the source
        // We need to find a useEffect that contains handleWebAuthnSubmit() as a call
        // (not just referenced in onClick={handleWebAuthnSubmit})
        
        // Split source into useEffect blocks
        const useEffectBlocks: string[] = [];
        const effectRegex = /useEffect\(\s*\(\)\s*=>\s*\{/g;
        let match;
        while ((match = effectRegex.exec(reAuthDialogSource)) !== null) {
          // Find the matching closing of this useEffect by counting braces
          const startIdx = match.index + match[0].length;
          let braceCount = 1;
          let endIdx = startIdx;
          while (braceCount > 0 && endIdx < reAuthDialogSource.length) {
            if (reAuthDialogSource[endIdx] === "{") braceCount++;
            if (reAuthDialogSource[endIdx] === "}") braceCount--;
            endIdx++;
          }
          useEffectBlocks.push(reAuthDialogSource.slice(startIdx, endIdx - 1));
        }

        // Check if any useEffect block calls handleWebAuthnSubmit() as a statement
        // (not just referencing it as a callback prop)
        const hasAutoTriggerInEffect = useEffectBlocks.some((block) => {
          // Must call handleWebAuthnSubmit() directly (with parentheses = invocation)
          // and must reference hasWebAuthn or credentials state
          return (
            block.includes("handleWebAuthnSubmit()") &&
            (block.includes("hasWebAuthn") || block.includes("credentials"))
          );
        });

        // Expected behavior: auto-trigger effect exists
        expect(hasAutoTriggerInEffect).toBe(true);
      }),
      { numRuns: 10 }
    );
  });

  /**
   * Bug 1.2 (supplementary): ReAuthDialog must have a webAuthnDeclined state
   * to prevent re-triggering after user cancels the auto-triggered prompt.
   *
   * Expected behavior: A `webAuthnDeclined` (or similar) boolean state variable
   * exists as a useState hook that is set to true when the user cancels,
   * preventing the auto-trigger useEffect from firing again.
   *
   * Current bug: No such state exists because there is no auto-trigger mechanism.
   * (Note: t("webAuthnCancelled") is a translation key, not a state variable)
   */
  it("ReAuthDialog has webAuthnDeclined state to prevent re-triggering after cancel", () => {
    fc.assert(
      fc.property(reAuthDialogBugConditionArb, (_input) => {
        // Check for a useState declaration that tracks whether WebAuthn was declined
        // Must be an actual state variable (useState), not just a translation key reference
        const hasDeclinedStateHook =
          reAuthDialogSource.match(
            /\[\s*webAuthnDeclined\s*,\s*setWebAuthnDeclined\s*\]\s*=\s*useState/
          ) !== null ||
          reAuthDialogSource.match(
            /\[\s*autoTriggerDone\s*,\s*setAutoTriggerDone\s*\]\s*=\s*useState/
          ) !== null ||
          reAuthDialogSource.match(
            /\[\s*webAuthnAttempted\s*,\s*setWebAuthnAttempted\s*\]\s*=\s*useState/
          ) !== null;

        // Expected behavior: declined state hook exists to guard auto-trigger
        expect(hasDeclinedStateHook).toBe(true);
      }),
      { numRuns: 10 }
    );
  });
});
