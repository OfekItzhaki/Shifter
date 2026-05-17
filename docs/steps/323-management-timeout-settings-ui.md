# Step 323 — Management Timeout Duration Setting in Group Settings UI

## Phase

Phase — Admin Session Timeout (Frontend Configuration)

## Purpose

Adds a `managementTimeoutMinutes` number input to the existing group settings page so group admins can configure how long management mode stays active before prompting for inactivity. The value is validated client-side (integer between 5 and 120) and included in the existing PATCH request to the group settings endpoint.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/useGroupPageState.ts` | Added `managementTimeoutMinutes` / `setManagementTimeoutMinutes` state (default 15) |
| `apps/web/app/groups/[groupId]/page.tsx` | Destructured new state, initialized from group data, added validation guard in `handleSaveSettings`, passed props to `SettingsTab` |
| `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` | Added `managementTimeoutMinutes` and `onManagementTimeoutChange` props, rendered number input with inline validation error |
| `apps/web/lib/api/groups.ts` | Extended `updateGroupSettings` function signature to accept optional `managementTimeoutMinutes` parameter |
| `apps/web/messages/en.json` | Added `managementTimeout`, `managementTimeoutDesc`, `managementTimeoutError`, `minutes` keys to `groups.settings_tab`; added `timeoutOutOfRange` to `errors` |
| `apps/web/messages/he.json` | Added Hebrew translations for the same keys |
| `apps/web/messages/ru.json` | Added Russian translations for the same keys |

## Key decisions

- The timeout input uses a standard number input with `min=5` and `max=120` HTML attributes for basic browser-level enforcement, plus explicit JS validation for the error message display.
- Client-side validation prevents the save request from being sent with invalid values, showing an inline error below the input and also blocking the save handler.
- The setting is placed after the "Allow members to view stats" toggle and before the "Minimum rest between shifts" section, grouping security-related settings together.
- The value is included in the same PATCH request as other settings (solver horizon, auto-publish, etc.) rather than a separate API call.

## How it connects

- **Requirement 3.2**: Timeout duration is configurable per group via the settings form.
- **Requirement 3.5**: Validation error is displayed if value is out of range.
- **Requirement 8.1**: The value is sent via the existing `PATCH /spaces/{spaceId}/groups/{groupId}/settings` endpoint.
- **Requirement 8.2**: The current value is read from the group data returned by the GET endpoint.
- Depends on tasks 4.3 (backend PATCH accepts `managementTimeoutMinutes`) and 1.3 (domain entity).
- The frontend inactivity timer (task 6.2) reads this value when entering management mode.

## How to run / verify

1. Navigate to a group's Settings tab as an admin.
2. The "Management Mode Timeout" section should appear with a number input showing the current value (default 15).
3. Change the value to something outside [5, 120] — a red validation error should appear inline.
4. Attempting to save with an invalid value should be blocked with an error message.
5. Set a valid value (e.g., 30) and click "Save Settings" — the value should persist.
6. Reload the page — the saved value should be displayed.

```bash
cd apps/web && npx tsc --noEmit
```

TypeScript compiles with zero errors.

## What comes next

- Task 10.2: Add platform timeout setting to platform settings page.
- Task 11: Final checkpoint — ensure all tests pass.

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): add management timeout duration setting to group settings UI"
```
