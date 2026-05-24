# 508 — ReAuth Dialog WebAuthn Biometric Authentication Flow

## Phase

Phase: Admin Re-Auth Security — Frontend WebAuthn Integration

## Purpose

When the user has registered WebAuthn credentials and the browser supports `navigator.credentials.get`, the ReAuth dialog should present biometric authentication as the primary action. This step implements the full WebAuthn assertion ceremony: fetching options from the backend, invoking the browser's credential API, serializing the assertion, and submitting it for verification. It also handles all failure modes (user cancelled, timed out, credential not recognized) with appropriate error messages and retry options.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/admin/ReAuthDialog.tsx` | Added WebAuthn biometric authentication flow: browser support check via `navigator.credentials?.get`, biometric button as primary action with gradient styling, WebAuthn ceremony orchestration (login/options → credentials.get → re-authenticate), error handling for all failure modes, "Use password instead" / "Use biometric instead" toggle links, and `activeMethod` state to switch between views |
| `apps/web/messages/en.json` | Added i18n keys: `webAuthnVerifying`, `webAuthnTimedOut`, `webAuthnNotRecognized`, `usePasswordInstead`, `useBiometricInstead` |
| `apps/web/messages/he.json` | Added Hebrew translations for the new WebAuthn keys |
| `apps/web/messages/ru.json` | Added Russian translations for the new WebAuthn keys |

## Key decisions

- **Browser support check**: Uses `navigator.credentials?.get` existence check (not just `window.PublicKeyCredential`) to determine if WebAuthn is available. If unsupported, the WebAuthn option is hidden entirely.
- **Active method state**: Introduced `activeMethod: "webauthn" | "password"` to toggle between the biometric view and password form. When credentials exist and browser supports WebAuthn, defaults to `"webauthn"`.
- **Primary action styling**: The biometric button uses a larger padding (1rem vs 0.75rem), gradient background, and a fingerprint SVG icon to visually distinguish it as the primary action.
- **Base64url encoding/decoding**: Implemented inline `bufferToBase64url` and `base64urlToBuffer` helpers to serialize/deserialize WebAuthn binary data without external dependencies.
- **Error classification**: Maps `NotAllowedError` → cancelled, `AbortError` → timed out, HTTP 401 → credential not recognized, HTTP 429 → rate limited. All other errors default to "not recognized".
- **Assertion serialization**: The assertion response is JSON-serialized with base64url-encoded binary fields (`authenticatorData`, `clientDataJSON`, `signature`, `userHandle`) matching the backend's expected `WebAuthnAssertionJson` format.
- **60-second timeout**: Passed via `publicKeyOptions.timeout = 60000` to the browser's credential API as specified in the design.
- **Retry available**: After any WebAuthn failure, the biometric button remains clickable and the "Use password instead" link is shown.

## How it connects

- Depends on task 3.1 (credential check) which provides `hasWebAuthnCredentials` state
- Uses `POST /auth/webauthn/login/options` endpoint (returns `{ optionsJson, challengeId }`)
- Submits to `POST /auth/re-authenticate` with `{ webAuthnChallengeId, webAuthnAssertionJson, spaceId }`
- The `activeMethod` state will be extended by task 3.3 (lockout state management)
- Task 4.1 will add autofill prevention attributes to the password input in the password view

## How to run / verify

1. Register a WebAuthn credential in your profile settings
2. Navigate to a page that triggers the ReAuth dialog (admin mode entry)
3. Verify the biometric button appears as the primary action with a fingerprint icon
4. Click the biometric button — the browser should prompt for fingerprint/Face ID/security key
5. On successful assertion: dialog closes and admin mode activates
6. On cancellation: error message "Verification cancelled" appears, retry button remains
7. Click "Use password instead" — switches to the password form
8. From password form, click "Use biometric instead" — switches back to biometric view
9. Without WebAuthn credentials: verify only the password form is shown (no biometric option)
10. In a browser without WebAuthn support: verify only the password form is shown

## What comes next

- Task 3.3: Add state management for WebAuthn and lockout (countdown timer, 429 handling)
- Task 4.1: Apply autofill prevention attributes to the password input

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): implement WebAuthn biometric authentication flow in ReAuthDialog"
```
