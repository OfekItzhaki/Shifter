# Implementation Plan

## Overview

This plan addresses two bugs in the fingerprint/WebAuthn login workflow: (1) a token race condition that prevents the post-login biometric registration prompt from appearing, and (2) the ReAuthDialog not auto-triggering WebAuthn authentication when the user has registered credentials. The implementation follows the exploratory bugfix methodology — write tests to confirm the bugs exist, write preservation tests for non-buggy behavior, implement the fix, then verify all tests pass.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Post-Login Token Race & ReAuthDialog Missing Auto-Trigger
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate both bugs exist
  - **Scoped PBT Approach**: Scope the property to the concrete failing cases:
    - Bug 1: After login(), listCredentials() is called but the request lacks a valid Authorization header (token race condition)
    - Bug 2: ReAuthDialog renders with hasWebAuthn=true and credentialLoading=false, but navigator.credentials.get() is never called automatically
  - Test that after a successful login with no existing WebAuthn credentials, listCredentials() is called with a valid Bearer token and the biometric prompt is shown (from Bug Condition in design: `isBugCondition(input)` where `input.scenario == "post-login"` AND `listCredentials() fails with 401`)
  - Test that when ReAuthDialog opens with hasWebAuthn=true and credential loading complete, navigator.credentials.get() is called automatically without user interaction (from Bug Condition in design: `isBugCondition(input)` where `input.scenario == "reauth-dialog"` AND `webAuthnFlowNotAutoTriggered == true`)
  - Run test on UNFIXED code - expect FAILURE (this confirms the bug exists)
  - Document counterexamples found:
    - "listCredentials() sends request without Authorization header or with stale token, receives 401"
    - "navigator.credentials.get() is never called automatically in ReAuthDialog when hasWebAuthn is true"
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Non-Buggy Login Flows & ReAuthDialog Without WebAuthn
  - **IMPORTANT**: Follow observation-first methodology
  - **Observe on UNFIXED code**:
    - Login with user who already has WebAuthn credentials → redirects immediately, no biometric prompt shown
    - Login on browser without WebAuthn support → redirects immediately, no biometric prompt shown
    - Login with biometric prompt previously dismissed (localStorage flag set) → redirects immediately, no biometric prompt shown
    - ReAuthDialog opens with hasWebAuthn=false → shows only password form, focuses password input
    - ReAuthDialog opens on browser without WebAuthn support → shows only password form
    - Conditional mediation (passkey autofill) login → authenticates and redirects without biometric prompt
  - Write property-based tests capturing observed behavior patterns:
    - For all login scenarios where user has existing credentials OR WebAuthn unsupported OR prompt dismissed: result is immediate redirect without biometric prompt (from Preservation Requirements in design)
    - For all ReAuthDialog openings where hasWebAuthn=false OR WebAuthn unsupported: result is password-only form with focused input (from Preservation Requirements in design)
    - For all conditional mediation authentications: result is redirect without biometric prompt
  - Property-based testing generates many test cases for stronger preservation guarantees across the input domain
  - Verify tests PASS on UNFIXED code (confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [x] 3. Fix for fingerprint login workflow bugs (token race condition & ReAuthDialog auto-trigger)

  - [x] 3.1 Fix token race condition in post-login listCredentials call
    - In `apps/web/app/login/page.tsx`, modify `handleSubmit` to pass the fresh access token directly to `listCredentials()` after login completes
    - Add optional `token` parameter to `listCredentials()` in `lib/webauthn.ts` that bypasses the apiClient interceptor
    - When token param is provided, use it directly in the Authorization header instead of relying on the interceptor reading from localStorage
    - Read `localStorage.getItem("access_token")` immediately after `await login(email, password)` and pass it to `listCredentials(token)`
    - _Bug_Condition: isBugCondition(input) where input.scenario == "post-login" AND listCredentials() fails with 401 due to token race condition_
    - _Expected_Behavior: listCredentials() called with valid fresh token, returns credential list, biometric prompt shown when list is empty_
    - _Preservation: Users with existing credentials, unsupported browsers, and dismissed prompts still skip the prompt_
    - _Requirements: 2.1, 3.1, 3.2, 3.3_

  - [x] 3.2 Add WebAuthn auto-trigger useEffect in ReAuthDialog
    - In `apps/web/components/admin/ReAuthDialog.tsx`, add a new `useEffect` that fires when `credentials.loading` becomes false and `credentials.hasWebAuthn` is true
    - The effect calls `handleWebAuthnSubmit()` automatically when conditions are met
    - Guard with `webAuthnDeclined` state to prevent re-triggering after user cancels
    - Guard with `open` state to prevent triggering when dialog is closed
    - _Bug_Condition: isBugCondition(input) where input.scenario == "reauth-dialog" AND input.hasWebAuthn == true AND webAuthnFlowNotAutoTriggered == true_
    - _Expected_Behavior: navigator.credentials.get() called automatically, onSuccess if WebAuthn succeeds, password form shown if cancelled_
    - _Preservation: ReAuthDialog without WebAuthn credentials continues to show only password form_
    - _Requirements: 2.2, 3.4, 3.5_

  - [x] 3.3 Add webAuthnDeclined state and update cancel handling
    - Add `const [webAuthnDeclined, setWebAuthnDeclined] = useState(false)` state
    - In `handleWebAuthnSubmit` error handling, when user cancels (NotAllowedError or USER_CANCELLED), set `webAuthnDeclined` to true
    - When `webAuthnDeclined` is true, focus the password input as fallback
    - Reset `webAuthnDeclined` to false when dialog closes (in the existing reset useEffect when `open` changes)
    - _Bug_Condition: Auto-triggered WebAuthn cancelled by user must not re-trigger_
    - _Expected_Behavior: After cancel, password form shown and focused, no re-trigger_
    - _Preservation: Manual WebAuthn button click still works, password form still works_
    - _Requirements: 2.2, 3.6_

  - [x] 3.4 Update focus management for auto-trigger flow
    - Modify the existing focus management `useEffect` in ReAuthDialog to not focus the password input when WebAuthn auto-trigger is pending (hasWebAuthn=true AND !webAuthnDeclined)
    - Only focus password input when: hasWebAuthn is false, OR webAuthnDeclined is true, OR WebAuthn is not supported
    - Ensure the WebAuthn browser prompt receives focus when auto-triggered
    - _Preservation: Focus behavior unchanged for password-only scenarios_
    - _Requirements: 2.2, 3.4_

  - [x] 3.5 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Post-Login Token Race & ReAuthDialog Auto-Trigger Fixed
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms the expected behavior is satisfied:
      - listCredentials() receives a valid token and returns credentials successfully
      - Biometric prompt appears for eligible users
      - ReAuthDialog auto-triggers WebAuthn when hasWebAuthn is true
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bugs are fixed)
    - _Requirements: 2.1, 2.2_

  - [x] 3.6 Verify preservation tests still pass
    - **Property 2: Preservation** - Non-Buggy Login Flows & ReAuthDialog Without WebAuthn
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all preservation tests still pass after fix:
      - Users with existing credentials still skip biometric prompt
      - Unsupported browsers still skip biometric prompt
      - Dismissed prompt flag still respected
      - ReAuthDialog without WebAuthn still shows password-only form
      - Conditional mediation still works unchanged
      - Cancel fallback to password form still works

- [x] 4. Checkpoint - Ensure all tests pass
  - Run full test suite to confirm no regressions
  - Verify bug condition test (task 1) passes on fixed code
  - Verify preservation tests (task 2) still pass on fixed code
  - Verify any existing WebAuthn-related tests still pass
  - Ensure all tests pass, ask the user if questions arise

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1", "2"] },
    { "id": 1, "tasks": ["3.1", "3.2"] },
    { "id": 2, "tasks": ["3.3"] },
    { "id": 3, "tasks": ["3.4"] },
    { "id": 4, "tasks": ["3.5", "3.6"] },
    { "id": 5, "tasks": ["4"] }
  ]
}
```

## Notes

- Both bugs are in the frontend (`apps/web`). No backend changes are required.
- Bug 1 (token race condition) is in `apps/web/app/login/page.tsx` and `lib/webauthn.ts`.
- Bug 2 (missing auto-trigger) is in `apps/web/components/admin/ReAuthDialog.tsx`.
- The exploration test (task 1) is expected to FAIL on unfixed code — this confirms the bug exists. Do not attempt to fix the test.
- The preservation tests (task 2) are expected to PASS on unfixed code — this confirms baseline behavior is captured correctly.
- After the fix, both exploration and preservation tests should PASS.
