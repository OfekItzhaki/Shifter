# 412 — Freeze Deactivation Dialog Integration

## Phase

Feature — Freeze Period Discard (Task 7.3)

## Purpose

Wire the `FreezeDeactivationDialog` into the `EmergencyFreezeBanner` component so that clicking "Deactivate Freeze" opens the dialog instead of immediately deactivating. On confirm, the `deactivateFreeze` API is called with the admin's discard choice. Success refreshes the config state and shows a toast; errors display appropriate messages.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/home-leave/EmergencyFreezeBanner.tsx` | Rewired deactivation flow: opens `FreezeDeactivationDialog` on click, calls `deactivateFreeze` API on confirm, shows success/error messages. Added new props: `spaceId`, `groupId`, `canRollback`, `onDeactivateSuccess`. |
| `apps/web/components/home-leave/HomeLeaveConfigPanel.tsx` | Added `isAdmin` prop, `DeactivateFreezeResponse` import, `handleDeactivateSuccess` callback that refreshes all config state from the API response. Passes new props to `EmergencyFreezeBanner`. |
| `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` | Passes `isAdmin` prop to `HomeLeaveConfigPanel`. |
| `apps/web/messages/en.json` | Added translation keys: `deactivating`, `deactivateSuccess`, `deactivateSuccessDiscard`, `errorPermission`, `errorBadRequest`, `errorServer`. |
| `apps/web/messages/he.json` | Hebrew translations for the new keys. |
| `apps/web/messages/ru.json` | Russian translations for the new keys. |

## Key decisions

1. **Dialog replaces direct deactivation** — The "Deactivate Freeze" button now opens the dialog rather than calling `toggleEmergencyFreeze(false)` directly. This gives the admin the opportunity to choose whether to discard freeze-period changes.
2. **`canRollback` derived from `isAdmin`** — On the frontend, admin mode implies the user has management permissions. The server enforces the real `schedule.rollback` permission check regardless of what the client sends.
3. **Inline success/error messages** — Rather than using a global toast system (which doesn't exist in this project), success and error messages are shown inline within the banner component with auto-dismiss timers.
4. **`onDeactivateSuccess` callback** — The parent `HomeLeaveConfigPanel` receives the full API response and updates all local state (mode, days, freeze status) from it, ensuring the UI is consistent with the server state.

## How it connects

- Depends on `FreezeDeactivationDialog` (task 7.2) and `deactivateFreeze` API function (task 7.1)
- The `EmergencyFreezeBanner` is rendered inside `HomeLeaveConfigPanel`, which is rendered inside `SettingsTab`
- The backend `POST /deactivate-freeze` endpoint (task 4.1) handles the actual deactivation logic

## How to run / verify

1. Navigate to a group's Settings tab with an active emergency freeze
2. Click "Deactivate Freeze" — the deactivation dialog should open
3. If admin has rollback permission and changes exist, the discard toggle should be visible
4. Confirm deactivation — success toast should appear and freeze state should clear
5. Test error cases: remove permissions (403), deactivate when not frozen (400)

## What comes next

- Task 7.4: Unit tests for the frontend deactivation dialog
- Task 8: Final integration verification checkpoint

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): integrate deactivation dialog into EmergencyFreezeBanner"
```
