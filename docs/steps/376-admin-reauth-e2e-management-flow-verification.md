# Step 376 — Admin Re-Auth Gate: Management Mode E2E Flow Verification

## Phase

Security — Admin Re-Authentication Gate

## Purpose

Verify the complete end-to-end management mode re-authentication flow is correctly wired from frontend to backend, confirming all acceptance criteria for task 7.1 are met.

## Verification Summary

### ✅ Complete Flow: User clicks admin toggle → ReAuthDialog opens → user submits password → API verifies → dialog closes → admin mode activates → elevated session timer starts

**Frontend trigger** (`apps/web/app/groups/[groupId]/page.tsx`):
1. User clicks the admin mode toggle button → `handleAdminModeToggle()` is called
2. If `isAdmin` is false and `hasCredentials !== false`, sets `showReAuthDialog = true`
3. `<ReAuthDialog open={showReAuthDialog} onSuccess={handleReAuthSuccess} onCancel={handleReAuthCancel} mode="management" spaceId={currentSpaceId} />` renders

**ReAuthDialog** (`apps/web/components/admin/ReAuthDialog.tsx`):
4. Dialog opens, fetches credential availability (password always true, WebAuthn checked via `listCredentials()`)
5. User types password and submits → `handlePasswordSubmit()` calls `apiClient.post("/auth/re-authenticate", { password, spaceId })`

**Backend** (`apps/api/Jobuler.Api/Controllers/AuthController.cs`):
6. `POST /auth/re-authenticate` endpoint (with `[Authorize]` and `[EnableRateLimiting("auth")]`) extracts `CurrentUserId` from JWT claims
7. Dispatches `ReAuthenticateCommand(userId, password, null, null, spaceId, ipAddress)` via MediatR

**Handler** (`apps/api/Jobuler.Application/Auth/Commands/ReAuthenticateCommand.cs`):
8. `ReAuthenticateCommandValidator` validates: UserId not empty, either password or WebAuthn provided
9. Handler checks password length ≤ 128 (early rejection if exceeded)
10. Loads active user from DB
11. Calls `BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)`
12. Logs audit entry via `IAuditLogger` with actor_user_id, space_id, method="password", success
13. Returns `ReAuthenticateResult(verified)`

**Response** (AuthController):
14. If `result.Success` → returns `200 OK` with `{ "success": true }`
15. If not → returns `401 Unauthorized` with `{ "error": "Authentication failed." }`

**Frontend success path** (group page):
16. ReAuthDialog receives 200 with `{ success: true }` → calls `onSuccess()`
17. `handleReAuthSuccess()`:
    - Sets `showReAuthDialog = false` (dialog closes)
    - Reads `group?.managementTimeoutMinutes ?? 15`
    - Calls `enterAdminMode(groupId)` → sets `adminGroupId = groupId` in auth store
    - Calls `enterElevatedMode("management", groupId, timeoutMinutes)` → starts elevated session timer
    - Sets `isAdmin = true`

### ✅ Exiting admin mode does NOT require re-auth

In `handleAdminModeToggle()`:
```typescript
if (isAdmin) {
  // Exiting admin mode — no re-auth needed
  exitAdminMode();
  setIsAdmin(false);
}
```
- `exitAdminMode()` simply sets `adminGroupId = null` in the auth store
- No dialog is shown, no API call is made
- This is confirmed by the integration test: "exits admin mode directly without showing ReAuthDialog"

### ✅ All admin role levels (management, admin, super-admin) are gated

The system gates admin access at two levels:

1. **Group-level management mode** — Gated by the ReAuthDialog on the group detail page. The `enterAdminMode(groupId)` action is intercepted by the re-auth gate. The `mode="management"` is passed to ReAuthDialog and `enterElevatedMode("management", groupId, timeout)` is called on success.

2. **Platform super-admin mode** — Gated by the ReAuthDialog on the platform page (`apps/web/app/platform/page.tsx`). When a user navigates to `/platform`, the page shows `<ReAuthDialog mode="platform">` before any content loads. On success, `enterElevatedMode("platform", undefined, platformTimeoutMinutes)` is called.

3. **Admin role within a group** — The `ADMIN_ONLY_TABS` (tasks, constraints, roles, settings) are only visible when `isAdmin === true`, which requires passing through the re-auth gate. Any user with admin privileges for a group must re-authenticate before accessing admin-only functionality.

The backend `POST /auth/re-authenticate` endpoint is role-agnostic — it verifies the user's identity regardless of their admin level. The frontend gates ALL privilege escalation paths through the same ReAuthDialog component.

## What was verified (files reviewed)

| File | Verification |
|------|-------------|
| `apps/web/app/groups/[groupId]/page.tsx` | Admin toggle → ReAuthDialog wiring, `handleReAuthSuccess` calls both stores, exit doesn't require re-auth |
| `apps/web/components/admin/ReAuthDialog.tsx` | Credential fetch, password/WebAuthn submission, error handling, focus trap, ARIA attributes |
| `apps/web/lib/store/authStore.ts` | `enterAdminMode(groupId)` sets `adminGroupId`, `exitAdminMode()` clears it, `adminGroupId` not persisted |
| `apps/web/lib/store/adminSessionStore.ts` | `enterElevatedMode(mode, groupId, timeout)` starts timer, `exitElevatedMode(reason)` clears state |
| `apps/api/Jobuler.Api/Controllers/AuthController.cs` | `[Authorize]`, `[EnableRateLimiting("auth")]`, correct response shapes |
| `apps/api/Jobuler.Application/Auth/Commands/ReAuthenticateCommand.cs` | Password length check, BCrypt verify, WebAuthn verify, audit logging |
| `apps/api/Jobuler.Application/Auth/Validators/ReAuthenticateCommandValidator.cs` | UserId required, password OR WebAuthn required |
| `apps/web/app/platform/page.tsx` | Platform re-auth gate, redirect on cancel, skip if already elevated |
| `apps/web/app/groups/[groupId]/types.ts` | `ADMIN_ONLY_TABS` restricts tab visibility |
| `apps/web/__tests__/admin/groupPageReAuthIntegration.test.tsx` | Existing integration tests cover all scenarios |

## Key decisions

- No code changes were needed — the flow is fully wired and correct
- The existing integration test suite (`groupPageReAuthIntegration.test.tsx`) already validates all the scenarios required by task 7.1
- The "all admin role levels are gated" requirement is satisfied by the architecture: both group management mode and platform super-admin mode go through the same ReAuthDialog component

## How it connects

- **Admin Session Timeout spec** — The elevated session timer started here is managed by the admin-session-timeout feature
- **WebAuthn spec** — The biometric re-auth path uses the WebAuthn infrastructure
- **Group settings** — The `managementTimeoutMinutes` is configurable per group in the settings tab

## How to run / verify

```bash
# Run the existing integration tests that validate this flow
cd apps/web
npx vitest run __tests__/admin/groupPageReAuthIntegration.test.tsx
```

Manual verification:
1. Log in → navigate to a group → click "Enter Admin Mode" → ReAuthDialog appears
2. Submit correct password → dialog closes → admin tabs appear → session timer starts
3. Click "Exit Admin Mode" → exits immediately without dialog
4. Navigate to `/platform` → ReAuthDialog appears before content loads

## What comes next

- Task 7.2: Verify full platform mode re-auth flow end-to-end
- Task 7.3: Write integration test for rate limiting enforcement

## Git commit

```bash
git add -A && git commit -m "docs(security): verify admin reauth gate management mode e2e flow"
```
