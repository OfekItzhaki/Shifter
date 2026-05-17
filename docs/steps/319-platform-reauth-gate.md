# 319 — Platform Re-Authentication Gate

## Phase

Admin Session Timeout — Frontend Integration Wiring

## Purpose

Wire the `ReAuthDialog` component into the super platform mode entry flow so that super-admins must re-authenticate before accessing platform tools. This prevents unauthorized use of unattended sessions at the platform level.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/platform/page.tsx` | Added re-authentication gate: imports `ReAuthDialog` and `adminSessionStore`, shows the dialog before platform content loads, fetches `platformTimeoutMinutes` from `GET /platform/settings`, calls `enterElevatedMode('platform', null, timeout)` on success, redirects to home on cancel. |

## Key decisions

- **Gate before content**: The re-auth dialog is shown as a full-page gate (early return) rather than an overlay on top of platform content. This ensures no platform data is fetched or rendered until authentication succeeds.
- **Skip re-auth if already elevated**: If the user is already in elevated platform mode (e.g. synced from another tab), the re-auth step is skipped to avoid redundant prompts.
- **Fetch timeout in parallel**: The platform timeout setting is fetched while the re-auth dialog is displayed, so there's no additional delay after successful authentication.
- **Default fallback**: If the settings endpoint fails (e.g. network error), the default 15-minute timeout is used.
- **Cancel navigates home**: Cancelling the re-auth dialog navigates to `/` (application home), keeping the user in standard view as required.

## How it connects

- Depends on `ReAuthDialog` (task 7.1) and `adminSessionStore` (task 6.1)
- Depends on `GET /platform/settings` endpoint (task 4.4)
- Works alongside task 9.1 (management mode re-auth gate) and task 9.3 (activity prompt wiring)
- The elevated mode state set here is consumed by the inactivity timer (task 6.2) and multi-tab sync (task 6.3)

## How to run / verify

1. Navigate to `/platform` as a platform admin
2. The `ReAuthDialog` should appear before any platform stats are shown
3. Enter correct credentials → platform content loads, elevated mode is active in `adminSessionStore`
4. Cancel the dialog → redirected to home page `/`
5. If already in elevated platform mode (from another tab), the dialog should not appear

## What comes next

- Task 9.3: Wire `ActivityPromptModal` to `adminSessionStore`
- Task 9.4: Wire timeout exit behavior (redirect + toast on timeout)
- Task 9.5: Wire activity event listeners and multi-tab sync

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): wire ReAuthDialog into platform mode entry flow"
```
