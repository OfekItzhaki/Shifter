# Step 520 — RegenerateConfirmDialog Component

## Phase

Phase 10 — Frontend: Schedule Regeneration UI

## Purpose

Provides a confirmation dialog that explains the regeneration action to the admin before triggering it. Handles API error responses (402 subscription expired, 409 conflict, 403 permission denied) with localized messages.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/schedule/RegenerateConfirmDialog.tsx` | Modal dialog component with confirm/cancel actions, error handling, and loading state |
| `apps/web/messages/en.json` | Added `scheduleRegeneration` i18n namespace with English translations |
| `apps/web/messages/he.json` | Added `scheduleRegeneration` i18n namespace with Hebrew translations |
| `apps/web/messages/ru.json` | Added `scheduleRegeneration` i18n namespace with Russian translations |

## Key decisions

- **Reused existing modal pattern** — follows the same overlay/dialog structure as `ReAuthDialog` and `DraftScheduleModal` (fixed overlay, centered card, RTL support)
- **Separate i18n namespace** — `scheduleRegeneration` keeps regeneration dialog strings isolated from the existing `regeneration.status` keys used by the status indicator
- **Error mapping by HTTP status** — 402 → subscription expired, 409 → already in progress, 403 → permission denied, anything else → generic error
- **onSuccess callback with runId** — allows the parent component (RegenerateButton) to start polling the run status after successful trigger
- **No auto-retry** — on error the dialog stays open so the user can read the message and dismiss manually

## How it connects

- Called by `RegenerateButton` (task 10.1) when the user clicks the regenerate action
- Calls `triggerRegeneration(spaceId, groupId)` from `lib/api/schedule.ts` (task 10.4)
- On success, passes `runId` to `onSuccess` callback which feeds into `RegenerationStatusIndicator` (task 10.3)
- Uses `next-intl` `useTranslations("scheduleRegeneration")` for all user-facing text

## How to run / verify

1. Import `RegenerateConfirmDialog` in a page or component
2. Pass `open={true}`, `spaceId`, `groupId`, `onClose`, and `onSuccess` props
3. Verify the dialog renders with title, description, and two buttons
4. Confirm clicking "Confirm" calls the API and closes on success
5. Simulate 402/409/403 responses and verify the correct error message appears

## What comes next

- Task 10.3: `RegenerationStatusIndicator` — polls run status and navigates to draft review on completion

## Git commit

```bash
git add -A && git commit -m "feat(frontend): add RegenerateConfirmDialog component with i18n"
```
