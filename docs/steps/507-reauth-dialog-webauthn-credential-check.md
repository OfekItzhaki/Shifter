# 507 — ReAuth Dialog WebAuthn Credential Check

## Phase

Phase: Admin Re-Auth Security — Frontend Credential Detection

## Purpose

When the ReAuth dialog opens, it needs to determine whether the user has registered WebAuthn credentials so it can decide which authentication method to present (biometric vs password-only). This step adds the API call to `GET /auth/webauthn/credentials` with a 5-second timeout, a loading indicator while the check is in progress, and graceful fallback to the password form on any failure.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/admin/ReAuthDialog.tsx` | Refactored credential detection to call `GET /auth/webauthn/credentials` with `AbortController` (5s timeout), added `credentialCheckLoading` and `hasWebAuthnCredentials` state, loading spinner, disabled buttons during check, and fallback logic on timeout/error/empty list |

## Key decisions

- **AbortController with setTimeout**: Used a 5-second `setTimeout` that calls `controller.abort()` rather than relying on axios timeout config, giving precise control over the abort behavior.
- **Axios CanceledError handling**: Axios throws `CanceledError` (not `AbortError`) when a request is aborted via signal, so we check for both error names plus `controller.signal.aborted`.
- **Silent fallback**: On any failure (timeout, network error, non-success status), the dialog falls back to the password form and only logs to the console — no user-facing error for the credential check itself.
- **Empty credentials list**: Treated as "no registered credentials" — `hasWebAuthnCredentials` stays `false`, showing password form only.
- **All actions disabled during loading**: Close button, cancel button, backdrop click, and Escape key are all disabled while the credential check is in progress.
- **Inline CSS spinner**: Used an SVG spinner with a `@keyframes spin` animation injected via `<style>` tag to avoid external dependencies.

## How it connects

- Depends on the existing `GET /auth/webauthn/credentials` backend endpoint (implemented in `WebAuthnController.cs`)
- The `hasWebAuthnCredentials` state will be consumed by task 3.2 (WebAuthn biometric authentication flow) to decide whether to show the biometric button
- The `credentialCheckLoading` state ensures no auth actions can be triggered before the check completes

## How to run / verify

1. Open the app and navigate to a page that triggers the ReAuth dialog (admin mode entry)
2. With WebAuthn credentials registered: verify the dialog shows a loading spinner briefly, then resolves
3. Without WebAuthn credentials: verify the dialog shows loading, then falls back to password form
4. Simulate network failure (disconnect network): verify the dialog falls back to password form after logging an error
5. Simulate slow response (throttle to >5s): verify the dialog aborts and falls back to password form with a console warning

## What comes next

- Task 3.2: Implement WebAuthn biometric authentication flow (uses `hasWebAuthnCredentials` to show biometric button)
- Task 3.3: Add state management for WebAuthn and lockout (extends the state added here)

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): add WebAuthn credential check API call to ReAuthDialog"
```
