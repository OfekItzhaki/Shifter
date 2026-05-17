# 301 — Sandbox Discard Flow UI

## Phase

Feature — Draft Simulation Sandbox (Task 8.2)

## Purpose

Implements the discard flow UI for the simulation sandbox. When an admin decides to abandon a simulation, they can discard the draft version, which clears all sandbox state and navigates back to the group schedule view without persisting any changes.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/sandbox/SandboxSettingsPanel.tsx` | Added "Discard Draft" button with confirmation dialog, discard handler that calls the existing `discardVersion` API, exits sandbox, and navigates to group view |
| `apps/web/messages/en.json` | Added `sandbox.discard.*` i18n keys (button, confirmMessage, confirmButton, cancelButton, discarding, error) |
| `apps/web/messages/he.json` | Added Hebrew translations for discard flow |
| `apps/web/messages/ru.json` | Added Russian translations for discard flow |

## Key decisions

1. **Discard button placement**: Added to the `SandboxSettingsPanel` footer alongside the "Run Simulation" button, since this is the primary action area for the sandbox.
2. **Confirmation dialog**: Inline confirmation within the panel (not a modal) to keep the UX lightweight. Shows a warning message with confirm/cancel buttons.
3. **Reuse existing API**: Calls `discardVersion(spaceId, versionId)` which sends `DELETE /spaces/{spaceId}/schedule-versions/{versionId}` — the same endpoint used in the `DraftScheduleModal`.
4. **State cleanup**: On success, calls `exitSandbox()` (which resets all Zustand state) before navigating, ensuring no stale state remains.
5. **Error handling**: On failure, shows an error message but preserves sandbox state so the user can retry or continue working.
6. **No persistence**: The discard flow does NOT persist any sandbox parameter changes to the database (Req 10.3).

## How it connects

- Uses `discardVersion` from `@/lib/api/schedule` (existing API function)
- Uses `exitSandbox` from the sandbox Zustand store (clears all state)
- Uses Next.js `useRouter` for navigation to `/groups/{groupId}`
- Follows the same confirmation pattern used in `DraftScheduleModal`

## How to run / verify

1. Enter the simulation sandbox from a draft schedule
2. Click "Discard Draft" button in the settings panel footer
3. Confirm the discard in the inline confirmation dialog
4. Verify the draft version is discarded (API call succeeds)
5. Verify sandbox state is cleared and user is navigated to the group page

## What comes next

- Task 8.3: Navigation guards (beforeunload warning when sandbox is active)
- Task 9.1: Frontend access control for sandbox entry

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): implement discard flow UI with confirmation dialog"
```
