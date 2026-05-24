# 508 — ReAuth Dialog WebAuthn & Lockout State Management

## Phase

Admin Re-Auth Security — Frontend credential detection and WebAuthn integration

## Purpose

Adds state management for WebAuthn browser support detection, active authentication method switching, WebAuthn loading/error states, and lockout handling with a countdown timer. This enables the ReAuthDialog to:

- Detect whether the browser supports WebAuthn (`navigator.credentials.get`)
- Switch between "webauthn" and "password" views via `activeMethod` state
- Track WebAuthn loading and error states independently from password form state
- Handle 429 responses with `retryAfterSeconds` to implement a lockout countdown
- Disable ALL auth actions (password submit, WebAuthn button) during lockout
- Automatically re-enable actions when the lockout timer expires

## What was built

| File | Change |
|------|--------|
| `apps/web/components/admin/ReAuthDialog.tsx` | Added `webAuthnSupported`, `activeMethod`, `webAuthnLoading`, `webAuthnError`, `isLockedOut`, `lockoutRemainingSeconds` state; lockout countdown timer with `setInterval`; 429 response parsing with `retryAfterSeconds`; disabled all auth actions during lockout; cleanup on unmount/close |
| `apps/web/messages/en.json` | Added `reAuth.lockedOut` key: "Too many attempts. Try again in {minutes} minutes." |
| `apps/web/messages/he.json` | Added `reAuth.lockedOut` key (Hebrew) |
| `apps/web/messages/ru.json` | Added `reAuth.lockedOut` key (Russian) |

## Key decisions

1. **Lockout timer uses `setInterval` with cleanup** — The interval decrements `lockoutRemainingSeconds` every second and auto-clears when reaching 0, setting `isLockedOut = false` and clearing the error message.
2. **Dual cleanup paths** — The interval is cleaned up both when the effect dependencies change (standard React cleanup) and when the dialog closes (`open` becomes false).
3. **429 parsing with fallback** — If the 429 response includes a valid `retryAfterSeconds` number, lockout mode activates. Otherwise, a generic "rate limited" message is shown.
4. **Both auth methods respect lockout** — The `handlePasswordSubmit` and `handleWebAuthnAuthenticate` callbacks both early-return if `isLockedOut` is true, and both buttons are disabled via the `disabled` prop.
5. **Exported `ActiveAuthMethod` type** — Exported for potential use by parent components or tests.

## How it connects

- **Task 3.1** added `credentialCheckLoading` and `hasWebAuthnCredentials` — this task builds on those.
- **Task 3.2** added the WebAuthn biometric flow — this task adds lockout state that applies to both WebAuthn and password flows.
- **Backend task 1.3/1.4** returns 429 with `retryAfterSeconds` — this task parses that response.
- **Task 4.2** will further harden the password form validation and error handling.

## How to run / verify

1. Open the ReAuthDialog in the browser
2. Trigger 5 failed password attempts (backend must be running with lockout logic)
3. Verify the "Too many attempts. Try again in X minutes." error appears
4. Verify both the password input and submit button are disabled
5. Verify the countdown decrements (check React DevTools state)
6. After the timer expires, verify the form re-enables

## What comes next

- Task 4.1: Apply autofill prevention attributes to the password input
- Task 4.2: Harden password form validation and error handling

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): add WebAuthn and lockout state management to ReAuthDialog"
```
