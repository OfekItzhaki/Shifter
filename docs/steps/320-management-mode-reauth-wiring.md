# 320 — Wire ReAuthDialog into Management Mode Entry Flow

## Phase

Phase 4 — Frontend Integration Wiring (Admin Session Timeout)

## Purpose

Intercepts the existing management mode toggle button on the group page so that entering management mode requires re-authentication via the `ReAuthDialog` component. On successful re-authentication, the system activates both the legacy `authStore.enterAdminMode` and the new `adminSessionStore.enterElevatedMode` with the group's configured timeout duration. If the user has no credentials configured, the button is disabled with a tooltip message.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/page.tsx` | Added `ReAuthDialog` import, `useState` for dialog visibility, credential checking logic, `handleAdminModeToggle`/`handleReAuthSuccess`/`handleReAuthCancel` handlers, disabled state with tooltip on the toggle button, and `ReAuthDialog` rendering |
| `apps/web/lib/api/groups.ts` | Added `managementTimeoutMinutes` field to `GroupWithMemberCountDto` interface |
| `apps/web/messages/en.json` | Added `noCredentialsTooltip` translation key under `groups` namespace |
| `apps/web/messages/he.json` | Added `noCredentialsTooltip` translation key under `groups` namespace |
| `apps/web/messages/ru.json` | Added `noCredentialsTooltip` translation key under `groups` namespace |

## Key decisions

- **Dual store activation**: On successful re-auth, both `authStore.enterAdminMode(groupId)` (legacy, controls tab visibility and admin UI) and `adminSessionStore.enterElevatedMode('management', groupId, timeoutMinutes)` (new, controls inactivity timer) are called. This maintains backward compatibility while enabling the new timeout feature.
- **Credential check at page level**: A `useEffect` checks credential availability on mount so the button can be disabled immediately without waiting for the dialog to open.
- **System invariant**: All registered users have a password, so `hasCredentials` is effectively always `true`. The disabled state is a defensive guard for edge cases (e.g., future OAuth-only users).
- **Timeout from group data**: The `managementTimeoutMinutes` value comes from the group API response (default 15), matching Requirement 3.7.

## How it connects

- **Depends on**: `ReAuthDialog` (step 316), `adminSessionStore` (step 314), backend `managementTimeoutMinutes` in group response (step 313)
- **Used by**: Task 9.4 (timeout exit behavior) and Task 9.5 (activity event listeners) will build on the elevated mode state set here
- **Maintains**: Existing `authStore.enterAdminMode`/`exitAdminMode` flow for backward compatibility with admin-only tabs

## How to run / verify

1. TypeScript compiles without errors: `npx tsc --noEmit` in `apps/web/`
2. Navigate to a group page as the group owner
3. Click the admin mode toggle — the `ReAuthDialog` should appear
4. Enter correct password → management mode activates with inactivity timer
5. Cancel the dialog → remain in standard view
6. Exit admin mode → no re-auth required

## What comes next

- Task 9.2: Wire `ReAuthDialog` into super platform mode entry flow
- Task 9.4: Wire timeout exit behavior (redirect + toast on timeout)
- Task 9.5: Wire activity event listeners and multi-tab sync

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): wire ReAuthDialog into management mode entry flow"
```
