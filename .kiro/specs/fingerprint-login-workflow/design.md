# Fingerprint Login Workflow Bugfix Design

## Overview

Two bugs prevent the fingerprint/WebAuthn login workflow from functioning as designed. The first bug is a race condition where `listCredentials()` fails with a 401 after login because the apiClient interceptor reads the access token from `localStorage` but the token may not be reliably available at the exact moment the call is made (potential timing issue between the zustand store's async `login()` resolution and the subsequent API call). The second bug is that the `ReAuthDialog` component never auto-triggers the WebAuthn authentication flow when the user has registered credentials — it simply renders both options side by side and focuses the password input.

The fix approach is:
1. Pass the fresh access token directly to `listCredentials()` (or use it inline) rather than relying on the interceptor to pick it up from localStorage.
2. Add an auto-trigger effect in `ReAuthDialog` that calls `handleWebAuthnSubmit()` once credential loading completes and `hasWebAuthn` is true.

## Glossary

- **Bug_Condition (C)**: The conditions that trigger each bug — (1) post-login `listCredentials()` call failing due to token unavailability, (2) `ReAuthDialog` not auto-triggering WebAuthn when `hasWebAuthn` is true
- **Property (P)**: The desired behavior — (1) biometric registration prompt appears after login for eligible users, (2) WebAuthn prompt auto-triggers in ReAuthDialog
- **Preservation**: Existing behaviors that must remain unchanged — mouse/password login flows, conditional mediation, dismissed prompt handling, password-only ReAuth for users without WebAuthn
- **`listCredentials()`**: Function in `lib/webauthn.ts` that calls `GET /auth/webauthn/credentials` via `apiClient`
- **`apiClient`**: Axios instance in `lib/api/client.ts` with a request interceptor that reads `access_token` from `localStorage`
- **`useAuthStore.login()`**: Zustand store action in `lib/store/authStore.ts` that calls the login API, stores tokens in localStorage, and updates store state
- **`ReAuthDialog`**: Component in `components/admin/ReAuthDialog.tsx` that handles re-authentication for admin/super-admin mode elevation
- **`handleWebAuthnSubmit()`**: Callback in `ReAuthDialog` that performs the full WebAuthn authentication flow (get options → credentials.get → verify)

## Bug Details

### Bug Condition

The bugs manifest in two distinct scenarios:

**Bug 1**: After a successful email/password login, the app calls `listCredentials()` to determine if the user should be offered biometric registration. The `apiClient` interceptor reads the token from `localStorage`, but there is a race condition where the token is not yet reliably available to the interceptor at the time of the call, resulting in a 401 that is silently caught.

**Bug 2**: When `ReAuthDialog` opens and determines `hasWebAuthn === true`, it renders the fingerprint button but never programmatically invokes the WebAuthn flow. The user must manually click the button.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type { scenario: "post-login" | "reauth-dialog", ...context }
  OUTPUT: boolean
  
  IF input.scenario == "post-login" THEN
    RETURN input.loginSuccessful == true
           AND input.webAuthnSupported == true
           AND input.biometricPromptNotDismissed == true
           AND listCredentials() fails with 401 due to token race condition
  END IF
  
  IF input.scenario == "reauth-dialog" THEN
    RETURN input.dialogOpen == true
           AND input.hasWebAuthn == true
           AND input.credentialLoadingComplete == true
           AND webAuthnFlowNotAutoTriggered == true
  END IF
  
  RETURN false
END FUNCTION
```

### Examples

- **Bug 1 Example**: User logs in with email/password → `login()` stores token in localStorage → `listCredentials()` is called immediately → interceptor reads localStorage but gets stale/missing token → 401 response → error caught silently → biometric prompt never shown → user redirected without prompt
- **Bug 1 Expected**: User logs in → `listCredentials()` uses the fresh token directly → returns empty array → biometric registration prompt modal appears
- **Bug 2 Example**: User opens ReAuthDialog → credentials fetched → `hasWebAuthn` set to true → password input focused → user sees both password form and fingerprint button side by side → must manually click fingerprint button
- **Bug 2 Expected**: User opens ReAuthDialog → credentials fetched → `hasWebAuthn` is true → WebAuthn prompt auto-triggers via `navigator.credentials.get()` → if user cancels, falls back to password form
- **Edge case**: User opens ReAuthDialog with `hasWebAuthn` true, auto-trigger fires, user cancels → password form shown and focused as fallback

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Mouse/keyboard email+password login flow must continue to work exactly as before
- Conditional mediation (passkey autofill) on the login page must continue to authenticate and redirect without showing the biometric registration prompt
- Users who already have WebAuthn credentials must continue to skip the biometric registration prompt after login
- Users on browsers without WebAuthn support must continue to skip the biometric prompt and redirect normally
- Users who previously dismissed the biometric prompt (localStorage flag) must continue to skip it
- ReAuthDialog for users without WebAuthn credentials must continue to show only the password form and focus the password input
- ReAuthDialog on browsers without WebAuthn support must continue to show only the password form
- Password submission in ReAuthDialog must continue to work identically
- WebAuthn button click in ReAuthDialog must continue to work when manually triggered
- Cancel button and Escape key in ReAuthDialog must continue to close the dialog

**Scope:**
All inputs that do NOT involve (1) the post-login `listCredentials()` token availability or (2) the auto-trigger of WebAuthn in ReAuthDialog should be completely unaffected by this fix. This includes:
- All password-based authentication flows
- All conditional mediation flows
- All manual WebAuthn button interactions
- All dialog accessibility features (focus trap, ARIA, keyboard navigation)

## Hypothesized Root Cause

Based on the bug description and code analysis, the most likely issues are:

1. **Token Race Condition (Bug 1)**: In `login/page.tsx`, `handleSubmit` calls `await login(email, password)` which internally calls `apiLogin()`, stores the token in localStorage, and updates zustand state. Immediately after, `listCredentials()` is called. The `apiClient` request interceptor reads `localStorage.getItem("access_token")` synchronously. While the token *should* be in localStorage by the time `listCredentials()` fires (since `login()` awaits the API call and sets it synchronously), there may be a subtle timing issue:
   - The zustand `persist` middleware may trigger an async flush that temporarily clears/overwrites the token
   - Or the axios interceptor may be reading a cached/stale reference
   - The safest fix is to pass the token explicitly rather than relying on the interceptor

2. **Missing Auto-Trigger Effect (Bug 2)**: In `ReAuthDialog.tsx`, the focus management `useEffect` (lines ~100-112) only focuses the password input or WebAuthn button. There is no `useEffect` that calls `handleWebAuthnSubmit()` when `hasWebAuthn` becomes true after credential loading completes. The component was designed to show both options side by side but the requirement is to auto-trigger WebAuthn.

3. **No Fallback State Management (Bug 2)**: When auto-triggering WebAuthn, if the user cancels, the component needs to gracefully fall back to showing the password form. Currently `handleWebAuthnSubmit` sets an error message on cancel but doesn't have a "webAuthnDeclined" state that would prevent re-triggering.

## Correctness Properties

Property 1: Bug Condition - Post-Login Biometric Prompt Appears

_For any_ successful email/password login where WebAuthn is supported, the user has no registered WebAuthn credentials, and the biometric prompt has not been previously dismissed, the fixed `handleSubmit` function SHALL successfully determine the user's credential count (zero) and display the biometric registration prompt modal.

**Validates: Requirements 2.1**

Property 2: Bug Condition - ReAuthDialog Auto-Triggers WebAuthn

_For any_ opening of the ReAuthDialog where the user has registered WebAuthn credentials (`hasWebAuthn` is true) and credential loading has completed, the fixed component SHALL automatically trigger the WebAuthn authentication flow (`navigator.credentials.get()`) without requiring manual button click, and SHALL fall back to the password form if the user cancels or the prompt fails.

**Validates: Requirements 2.2**

Property 3: Preservation - Non-Eligible Users Skip Biometric Prompt

_For any_ login where the user already has WebAuthn credentials, OR WebAuthn is not supported, OR the biometric prompt was previously dismissed, the fixed code SHALL produce the same behavior as the original code — redirecting immediately without showing the biometric registration prompt.

**Validates: Requirements 3.1, 3.2, 3.3**

Property 4: Preservation - ReAuthDialog Without WebAuthn

_For any_ opening of the ReAuthDialog where the user does NOT have registered WebAuthn credentials OR WebAuthn is not supported, the fixed component SHALL produce the same behavior as the original — showing only the password form and focusing the password input.

**Validates: Requirements 3.4, 3.5**

Property 5: Preservation - ReAuthDialog Fallback After Cancel

_For any_ auto-triggered WebAuthn prompt in the ReAuthDialog that the user cancels, the fixed component SHALL allow the user to authenticate via the password form as a fallback, preserving the existing manual authentication flow.

**Validates: Requirements 3.6**

Property 6: Preservation - Conditional Mediation Unaffected

_For any_ conditional mediation (passkey autofill) authentication on the login page, the fixed code SHALL continue to authenticate and redirect without showing the biometric registration prompt.

**Validates: Requirements 3.7**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `apps/web/app/login/page.tsx`

**Function**: `handleSubmit`

**Specific Changes**:
1. **Pass token explicitly to listCredentials**: After `await login(email, password)` completes, retrieve the fresh access token from localStorage and pass it directly to the `listCredentials()` call (or create an overload that accepts a token parameter), bypassing the interceptor's localStorage read. Alternatively, use `apiClient` with a manual `Authorization` header override for this specific call.
   - Simplest approach: read `localStorage.getItem("access_token")` immediately after login and make the credentials check with an explicit header:
     ```typescript
     const freshToken = localStorage.getItem("access_token");
     const { data: creds } = await axios.get(`${API_URL}/auth/webauthn/credentials`, {
       headers: { Authorization: `Bearer ${freshToken}` }
     });
     ```
   - Or add an optional `token` parameter to `listCredentials()` in `lib/webauthn.ts`

2. **Ensure token is available before the call**: Add a microtask yield (`await Promise.resolve()` or `await new Promise(r => setTimeout(r, 0))`) between login and listCredentials to ensure any async side effects from zustand persist have settled. This is a belt-and-suspenders approach alongside the explicit token passing.

---

**File**: `apps/web/components/admin/ReAuthDialog.tsx`

**Function**: Component body (new `useEffect`)

**Specific Changes**:
3. **Add auto-trigger useEffect**: Add a new `useEffect` that fires when `credentials.loading` becomes false and `credentials.hasWebAuthn` is true. This effect should call `handleWebAuthnSubmit()` automatically.
   ```typescript
   useEffect(() => {
     if (!open || credentials.loading || !credentials.hasWebAuthn) return;
     if (webAuthnDeclined) return; // Don't re-trigger after user cancels
     handleWebAuthnSubmit();
   }, [open, credentials.loading, credentials.hasWebAuthn]);
   ```

4. **Add webAuthnDeclined state**: Add a `useState<boolean>(false)` to track whether the user has cancelled the auto-triggered WebAuthn prompt. When cancelled, set this to true so the effect doesn't re-trigger, and show the password form as fallback.

5. **Update handleWebAuthnSubmit cancel handling**: When the user cancels (NotAllowedError or USER_CANCELLED), set `webAuthnDeclined = true` and focus the password input as fallback instead of just showing an error.

6. **Reset webAuthnDeclined on dialog close**: In the existing reset `useEffect` (when `open` changes), reset `webAuthnDeclined` to false so the next dialog open will auto-trigger again.

7. **Adjust focus management**: When auto-triggering, don't focus the password input initially — let the WebAuthn prompt take focus. Only focus the password input after WebAuthn is declined or if `hasWebAuthn` is false.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write unit tests that simulate the post-login flow and ReAuthDialog rendering. Mock the apiClient and WebAuthn APIs to observe the token race condition and missing auto-trigger behavior. Run these tests on the UNFIXED code to observe failures.

**Test Cases**:
1. **Post-Login Token Race Test**: Simulate login → immediately call listCredentials → assert the request includes a valid Authorization header (will fail on unfixed code if token is not available)
2. **Post-Login Prompt Display Test**: Simulate login for user with no WebAuthn credentials → assert biometric prompt modal appears (will fail on unfixed code due to 401)
3. **ReAuthDialog Auto-Trigger Test**: Render ReAuthDialog with hasWebAuthn=true → assert navigator.credentials.get() is called automatically (will fail on unfixed code)
4. **ReAuthDialog No Manual Click Required Test**: Render ReAuthDialog with hasWebAuthn=true → assert WebAuthn flow starts without any user interaction (will fail on unfixed code)

**Expected Counterexamples**:
- `listCredentials()` receives a 401 because the Authorization header is missing or stale
- `navigator.credentials.get()` is never called automatically in ReAuthDialog
- Possible causes: interceptor reads stale localStorage, no auto-trigger effect exists

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  IF input.scenario == "post-login" THEN
    result := handleSubmit_fixed(input)
    ASSERT listCredentials called with valid token
    ASSERT biometricPromptShown == true (when creds.length == 0)
  END IF
  IF input.scenario == "reauth-dialog" THEN
    result := renderReAuthDialog_fixed(input)
    ASSERT navigator.credentials.get() called automatically
    ASSERT onSuccess called if WebAuthn succeeds
    ASSERT passwordForm shown if WebAuthn cancelled
  END IF
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT handleSubmit_original(input) = handleSubmit_fixed(input)
  ASSERT renderReAuthDialog_original(input) = renderReAuthDialog_fixed(input)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain
- It catches edge cases that manual unit tests might miss
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for non-bug-condition inputs (users with existing credentials, unsupported browsers, dismissed prompts, ReAuth without WebAuthn), then write property-based tests capturing that behavior.

**Test Cases**:
1. **Login Redirect Preservation**: Verify that users with existing WebAuthn credentials are redirected immediately after login (no prompt shown) — same behavior before and after fix
2. **Unsupported Browser Preservation**: Verify that browsers without WebAuthn support skip the biometric prompt entirely — same behavior before and after fix
3. **Dismissed Prompt Preservation**: Verify that users with localStorage flag set skip the prompt — same behavior before and after fix
4. **ReAuth Password-Only Preservation**: Verify that ReAuthDialog without WebAuthn credentials shows only password form and focuses input — same behavior before and after fix
5. **Conditional Mediation Preservation**: Verify that passkey autofill login redirects without showing biometric prompt — same behavior before and after fix

### Unit Tests

- Test that `listCredentials()` with explicit token parameter sends correct Authorization header
- Test that `handleSubmit` shows biometric prompt when credentials list is empty
- Test that `handleSubmit` redirects when credentials list is non-empty
- Test that ReAuthDialog auto-triggers WebAuthn when `hasWebAuthn` is true
- Test that ReAuthDialog falls back to password form when WebAuthn is cancelled
- Test that ReAuthDialog does not auto-trigger when `hasWebAuthn` is false
- Test that `webAuthnDeclined` state prevents re-triggering

### Property-Based Tests

- Generate random login scenarios (varying WebAuthn support, credential count, dismissed flag) and verify correct prompt/redirect behavior
- Generate random ReAuthDialog states (varying hasWebAuthn, loading, webAuthnDeclined) and verify correct auto-trigger/fallback behavior
- Generate random sequences of dialog open/close/cancel and verify state resets correctly

### Integration Tests

- Test full login flow → biometric prompt → registration → redirect
- Test full login flow → biometric prompt → dismiss → redirect with localStorage flag
- Test full ReAuthDialog flow → auto-trigger → success → onSuccess callback
- Test full ReAuthDialog flow → auto-trigger → cancel → password form → submit → success
- Test that conditional mediation login bypasses biometric prompt entirely
