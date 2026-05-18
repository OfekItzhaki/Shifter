# Step 375 — Verify Full Platform Mode Re-Auth Flow End-to-End

## Phase

Security — Admin Re-Authentication Gate

## Purpose

Verify the complete end-to-end platform re-authentication flow from user navigation through credential verification to elevated session activation, confirming all components are correctly wired together.

## What was verified

### Complete Flow Trace

The full platform re-auth flow proceeds through these stages:

1. **User navigates to `/platform`** → `PlatformPage` component mounts
2. **Hydration check** → Waits for Zustand store rehydration from localStorage
3. **Auth check** → If not logged in, redirects to `/login`
4. **Elevated mode check** → If `isElevated && elevatedMode === "platform"`, skips re-auth entirely
5. **Re-auth gate activates** → `setShowReAuth(true)` is called, `useEffect` returns early (no stats loaded)
6. **Platform timeout fetched** → `apiClient.get("/platform/settings")` retrieves `platformTimeoutMinutes` (defaults to 15 on error)
7. **ReAuthDialog renders** → Modal with `mode="platform"` blocks all platform content
8. **User submits credentials** → Password via form submit or WebAuthn via button click
9. **API verification** → `POST /auth/re-authenticate` dispatches `ReAuthenticateCommand` via MediatR
10. **Backend verifies** → BCrypt password check or WebAuthn assertion validation
11. **Audit log created** → Every attempt (success/failure) logged with actor_user_id, space_id, method, success
12. **API responds** → `{ "success": true }` (200) or `{ "error": "Authentication failed." }` (401)
13. **Dialog closes** → `handleReAuthSuccess` sets `showReAuth=false`, `platformAuthenticated=true`
14. **Elevated mode activated** → `enterElevatedMode("platform", undefined, platformTimeoutMinutes)` called
15. **Inactivity timer starts** → `useAdminSessionWiring` detects `isElevated=true`, creates `InactivityTimer` with `timeoutDuration * 60 * 1000` ms
16. **Platform tools appear** → `useEffect` re-runs with `platformAuthenticated=true`, loads stats via `getPlatformStats()`

### Cancellation Flow

| Step | Action | Result |
|------|--------|--------|
| 1 | User clicks Cancel button in ReAuthDialog | `onCancel` callback fires |
| 2 | `handleReAuthCancel` executes | `setShowReAuth(false)` + `router.push("/")` |
| 3 | User redirected to home page | Standard (non-admin) view maintained |

### Component Wiring Verification

| Connection | From | To | Verified |
|-----------|------|-----|----------|
| Dialog open trigger | `PlatformPage` useEffect | `setShowReAuth(true)` | ✅ |
| Dialog props | `PlatformPage` render | `<ReAuthDialog open={showReAuth} mode="platform" onSuccess={handleReAuthSuccess} onCancel={handleReAuthCancel} />` | ✅ |
| API call (password) | `ReAuthDialog.handlePasswordSubmit` | `POST /auth/re-authenticate` with `{ password, spaceId }` | ✅ |
| API call (WebAuthn) | `ReAuthDialog.handleWebAuthnSubmit` | `POST /auth/webauthn/login/options` then `POST /auth/re-authenticate` with `{ webAuthnChallengeId, webAuthnAssertionJson, spaceId }` | ✅ |
| Backend dispatch | `AuthController.ReAuthenticate` | `ReAuthenticateCommand` via MediatR | ✅ |
| Password verification | `ReAuthenticateCommandHandler` | `BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)` | ✅ |
| WebAuthn verification | `ReAuthenticateCommandHandler` | `IWebAuthnService.CompleteAuthenticationAsync` | ✅ |
| Audit logging | `ReAuthenticateCommandHandler.LogAttempt` | `IAuditLogger.LogAsync` with action="re_authenticate" | ✅ |
| Success response | `AuthController` | `Ok(new { success = true })` | ✅ |
| Failure response | `AuthController` | `Unauthorized(new { error = "Authentication failed." })` | ✅ |
| Elevated mode entry | `handleReAuthSuccess` | `enterElevatedMode("platform", undefined, platformTimeoutMinutes)` | ✅ |
| Timer activation | `useAdminSessionWiring` useEffect | `new InactivityTimer().start(timeoutMs, callbacks)` when `isElevated` becomes true | ✅ |
| Timer wiring global | `AdminSessionGuard` in `providers.tsx` | `useAdminSessionWiring()` called for all pages | ✅ |
| Settings endpoint | `PlatformController.GetSettings` | Returns `PlatformSettingsResponse(timeoutMinutes)` → serializes as `{ "platformTimeoutMinutes": N }` | ✅ |
| Skip re-auth | `PlatformPage` useEffect | `isElevated && elevatedMode === "platform"` → `setPlatformAuthenticated(true)` | ✅ |

### Requirements Coverage

| Requirement | Description | Status |
|-------------|-------------|--------|
| 1.2 | Platform admin re-auth gate intercepts Super_Admin_Mode entry | ✅ Verified — dialog shown before any platform content |
| 1.3 | Elevation prevented until successful verification | ✅ Verified — `enterElevatedMode` only called in `handleReAuthSuccess` after API returns success |
| 4.3 | WebAuthn success activates admin mode | ✅ Verified — `handleWebAuthnSubmit` calls `onSuccess()` on `response.data?.success` |
| 9.4 | Uses existing `enterAdminMode`/`enterElevatedMode` actions | ✅ Verified — `enterElevatedMode("platform", undefined, platformTimeoutMinutes)` called directly |

## Key decisions

- No code changes required — the existing implementation correctly wires all components end-to-end.
- The `platformTimeoutMinutes` gracefully defaults to 15 if the settings fetch fails (network error, non-platform-admin, etc.).
- The `AdminSessionGuard` is mounted globally in `providers.tsx`, ensuring the inactivity timer starts automatically when any page calls `enterElevatedMode`.

## How it connects

- **PlatformPage** (`app/platform/page.tsx`): Entry point, manages re-auth gate state
- **ReAuthDialog** (`components/admin/ReAuthDialog.tsx`): Modal UI with password/WebAuthn flows
- **AuthController** (`Controllers/AuthController.cs`): `POST /auth/re-authenticate` endpoint
- **ReAuthenticateCommandHandler** (`Application/Auth/Commands/ReAuthenticateCommand.cs`): Backend verification logic
- **PlatformController** (`Controllers/PlatformController.cs`): `GET /platform/settings` for timeout config
- **adminSessionStore** (`lib/store/adminSessionStore.ts`): `enterElevatedMode` state management
- **useAdminSessionWiring** (`lib/hooks/useAdminSessionWiring.ts`): Timer start/stop on elevated mode changes
- **AdminSessionGuard** (`components/admin/AdminSessionGuard.tsx`): Global wiring mounted in `providers.tsx`

## How to run / verify

1. Log in as a platform admin user
2. Navigate to `/platform` — ReAuthDialog should appear immediately
3. Submit valid password — dialog closes, platform stats load
4. Check browser DevTools: `useAdminSessionStore.getState()` should show `isElevated: true, elevatedMode: "platform"`
5. Wait for timeout duration — activity prompt should appear
6. Navigate away and back while still elevated — re-auth should be skipped
7. Open re-auth dialog and click Cancel — should redirect to `/`
8. Try wrong password — should show "Authentication failed" error, dialog stays open

## What comes next

- Task 7.3: Write integration test for rate limiting enforcement

## Git commit

```bash
git add -A && git commit -m "docs(admin-reauth): verify full platform re-auth e2e flow (step 375)"
```
