# Step 373 — Platform Page Re-Auth Integration Verification

## Phase

Security — Admin Re-Authentication Gate

## Purpose

Verify that the platform page (`apps/web/app/platform/page.tsx`) correctly integrates the re-authentication gate, ensuring all acceptance criteria for platform-level admin elevation are met.

## What was verified

| # | Requirement | Status | Evidence |
|---|-------------|--------|----------|
| 1 | Accessing platform page shows ReAuthDialog before content loads | ✅ | When `!platformAuthenticated` and not already elevated, `setShowReAuth(true)` is called and the effect returns early before loading stats. The render path shows `<ReAuthDialog>` when `showReAuth` is true, blocking all platform content. |
| 2 | On success: `enterElevatedMode("platform", undefined, platformTimeoutMinutes)` is called | ✅ | `handleReAuthSuccess` callback calls exactly `enterElevatedMode("platform", undefined, platformTimeoutMinutes)` after closing the dialog and setting `platformAuthenticated = true`. |
| 3 | On cancel: user is redirected away from platform page | ✅ | `handleReAuthCancel` calls `router.push("/")` to redirect to home. |
| 4 | Platform timeout is fetched from `GET /platform/settings` | ✅ | `apiClient.get("/platform/settings")` is called when re-auth dialog opens. Response field `platformTimeoutMinutes` is read (camelCase matches ASP.NET Core default serialization of `PlatformSettingsResponse` record). Falls back to 15 on error. |
| 5 | If already in elevated platform mode, re-auth is skipped | ✅ | `if (isElevated && elevatedMode === "platform") { setPlatformAuthenticated(true); }` bypasses the re-auth dialog entirely. |

## Key decisions

- No code changes required — the existing implementation satisfies all acceptance criteria.
- The backend `GET /platform/settings` endpoint exists in `PlatformController.cs` and returns `PlatformSettingsResponse(timeoutMinutes)` which serializes to `{ "platformTimeoutMinutes": N }` via default camelCase JSON serialization.
- The `useEffect` dependency array correctly includes `platformAuthenticated`, `isElevated`, and `elevatedMode` to handle state transitions without stale closures.

## How it connects

- **ReAuthDialog** (`components/admin/ReAuthDialog.tsx`): Renders the modal with password/WebAuthn flows, calls `onSuccess`/`onCancel` callbacks.
- **adminSessionStore** (`lib/store/adminSessionStore.ts`): `enterElevatedMode("platform", undefined, timeout)` sets `isElevated=true`, `elevatedMode="platform"`, starts the inactivity timer.
- **PlatformController** (`Controllers/PlatformController.cs`): `GET /platform/settings` returns the configurable timeout value from `PlatformSettings` table.

## How to run / verify

1. Log in as a platform admin user
2. Navigate to `/platform` — the ReAuthDialog should appear immediately
3. Submit valid credentials — platform stats should load
4. Navigate away and back — if still in elevated mode, re-auth should be skipped
5. Open re-auth dialog and click Cancel — should redirect to `/`

## What comes next

- Task 4.3: Verify password form keyboard submission
- Task 4.4: Verify error handling and recovery

## Git commit

```bash
git add -A && git commit -m "docs(admin-reauth): verify platform page re-auth integration (step 373)"
```
