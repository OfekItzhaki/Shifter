# 316 — ReAuthDialog Component

## Phase

Admin Session Timeout — Frontend Re-Authentication Dialog

## Purpose

Provides a modal dialog that prompts users to re-authenticate (via password or WebAuthn/fingerprint) before entering elevated privilege modes (Management Mode or Super Platform Mode). This prevents unauthorized use of unattended sessions.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/admin/ReAuthDialog.tsx` | Full re-authentication dialog component with password and WebAuthn support, focus trap, ARIA attributes, loading states, and error handling |
| `apps/web/messages/en.json` | English translation keys for the dialog (reAuth namespace) |
| `apps/web/messages/he.json` | Hebrew translation keys for the dialog (reAuth namespace) |

## Key decisions

- **Credential detection**: All registered users are assumed to have a password (registration requires one). WebAuthn availability is detected by querying the user's registered credentials via `listCredentials()`.
- **WebAuthn flow**: The component handles the full WebAuthn assertion flow inline (get options → navigator.credentials.get → send assertion to `/auth/re-authenticate`) rather than reusing the login flow, since re-authentication uses a different endpoint.
- **Base64url helpers duplicated**: The base64url conversion helpers are duplicated from `webauthn.ts` to avoid tight coupling between the dialog and the login-specific WebAuthn module.
- **Focus management**: Submit button receives initial focus (not the password input) per the design spec. Focus trap cycles through focusable elements within the dialog.
- **Error handling**: Generic error messages that don't reveal the cause of failure. Rate limiting (429) gets a distinct message. Password field is cleared on failure.
- **RTL support**: Dialog uses `direction: rtl` and password input uses `direction: ltr` with `text-align: left` for proper bidirectional text handling.

## How it connects

- **Consumed by**: Task 9.1 (management mode entry wiring) and Task 9.2 (platform mode entry wiring) will render this dialog before activating elevated modes.
- **Depends on**: `apiClient` for API calls, `listCredentials()` from `lib/webauthn.ts` for WebAuthn detection, `next-intl` for translations.
- **Backend endpoint**: `POST /auth/re-authenticate` (implemented in Task 4.1).

## How to run / verify

1. The component compiles without TypeScript errors.
2. Translation keys exist in both `en.json` and `he.json` under the `reAuth` namespace.
3. Integration testing will be done when wired into the management mode entry flow (Task 9.1).

## What comes next

- Task 7.2: Property test for credential method display correctness
- Task 8.1: ActivityPromptModal component
- Task 9.1: Wire ReAuthDialog into management mode entry flow

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): create ReAuthDialog component"
```
