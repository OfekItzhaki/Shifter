# 229 — WebAuthn Controller & Frontend Integration

## Phase

Biometric Login — Phase 3 (API Controller) + Phase 4 (Frontend)

## Purpose

Wire the WebAuthn backend commands/queries to HTTP endpoints via a thin MediatR-dispatching controller, and build the frontend integration: a utility module for WebAuthn browser API calls, a biometric login button on the login page, and credential management UI on the profile page.

## What was built

### API Layer (Phase 3)

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/WebAuthnController.cs` | 7 endpoints under `/auth/webauthn` — register options/complete, login options/complete, list/delete/patch credentials. Follows existing AuthController patterns with MediatR dispatch, rate limiting, and proper auth attributes. |

### Frontend (Phase 4)

| File | Description |
|------|-------------|
| `apps/web/lib/webauthn.ts` | Utility module: feature detection, base64url↔ArrayBuffer helpers, `registerCredential()`, `authenticateWithBiometric()`, `listCredentials()`, `deleteCredential()`, `updateCredentialNickname()`. Handles `NotAllowedError` gracefully. |
| `apps/web/app/login/page.tsx` | Added "התחבר עם ביומטרי" button above the email+password form. Only shown when WebAuthn is supported. Handles success (token storage + redirect) and failure (dismissible error). |
| `apps/web/app/profile/page.tsx` | Added `BiometricSection` component: shows enable prompt when no credentials exist, lists registered credentials with inline nickname editing, delete with confirmation, and "add another device" button. |

### DI Verification (Task 5.2)

Confirmed `Program.cs` already registers:
- `Fido2NetLib.IFido2` singleton with config from `appsettings.json`
- `IWebAuthnService` → `Fido2Service` scoped
- `IMemoryCache` via `AddMemoryCache()`

## Key decisions

- **Controller is thin**: No business logic — all 7 endpoints dispatch to MediatR commands/queries
- **Feature detection on client**: `isWebAuthnSupported()` checks `window.PublicKeyCredential` existence; all biometric UI is hidden if unsupported
- **User cancellation handling**: `NotAllowedError` from the browser API is caught and surfaced as a non-alarming message (not an error state)
- **Token storage matches existing flow**: Biometric login stores tokens in localStorage + cookie identically to the email+password flow
- **Hebrew-first UI**: All biometric labels are in Hebrew ("התחבר עם ביומטרי", "כניסה ביומטרית", "הפעל כניסה ביומטרית")
- **Inline nickname editing**: Click credential name to edit, Enter to save, Escape to cancel
- **Delete confirmation**: Two-step delete (click trash → confirm/cancel) to prevent accidental deletion

## How it connects

- Controller dispatches to commands/queries created in Phase 2 (step 228)
- Frontend utility calls the controller endpoints via the existing `apiClient` (axios instance with auth interceptors)
- Login page biometric button uses the same token storage pattern as `useAuthStore.login()`
- Profile page credential management uses the same card styling as other profile sections

## How to run / verify

1. **API build**: `cd apps/api/Jobuler.Api && dotnet build` — should succeed with 0 errors
2. **Frontend type check**: Open the 3 modified files in VS Code — no TypeScript errors
3. **Manual test (login page)**: Navigate to `/login` on a WebAuthn-capable device — biometric button should appear above the form
4. **Manual test (profile page)**: Navigate to `/profile` — "כניסה ביומטרית" section should appear between push notifications and export data

## What comes next

- Final checkpoint (Task 8): End-to-end verification of the full registration and authentication flows
- Integration tests with mocked Fido2 responses
- Potential i18n key extraction for biometric-related strings

## Git commit

```bash
git add -A && git commit -m "feat(biometric): add WebAuthn controller and frontend integration (Phase 3+4)"
```
