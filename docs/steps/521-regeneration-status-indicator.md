# Step 521 — RegenerationStatusIndicator Component

## Phase

Phase: Schedule Regeneration — Frontend Components

## Purpose

Provides real-time feedback to the admin after triggering a schedule regeneration. The component polls the schedule run status endpoint every 3 seconds and displays the current state (queued, running, completed, failed). On completion, it shows a "Review Draft" button that navigates the admin to the draft review panel. On failure, it displays the localized error message from the backend.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/schedule/RegenerationStatusIndicator.tsx` | Main component — polls run status, renders phase-specific UI with spinner/success/error icons, "Review Draft" button, and dismiss action |
| `apps/web/__tests__/schedule/regenerationStatusIndicator.test.tsx` | 20 unit tests covering all states, polling behavior, callbacks, accessibility, and dismiss functionality |
| `apps/web/messages/en.json` | Added `reviewDraft` and `dismiss` i18n keys under `regeneration.status` |
| `apps/web/messages/he.json` | Added Hebrew translations for `reviewDraft` ("סקירת טיוטה") and `dismiss` ("סגור") |
| `apps/web/messages/ru.json` | Added Russian translations for `reviewDraft` ("Просмотр черновика") and `dismiss` ("Закрыть") |

## Key decisions

- **Polling interval of 3 seconds** — balances responsiveness with API load. Polling stops immediately on terminal states (completed/failed).
- **`onCompleted` + `onDismiss` props** — separation of concerns: `onCompleted(resultVersionId)` handles navigation to the draft review panel, while `onDismiss` lets the parent component clean up the indicator.
- **"Review Draft" button on completed state** — gives the admin an explicit action to navigate to the draft, rather than auto-navigating (which could be disorienting).
- **Inline styles for phase colors** — avoids Tailwind dynamic class string issues while keeping the component self-contained.
- **`useRef` for `onCompleted` callback** — prevents the polling effect from re-running when the parent re-renders with a new callback reference.

## How it connects

- Called by the schedule management panel (task 10.1) after `RegenerateConfirmDialog` (task 10.2) successfully triggers a regeneration and returns a `runId`.
- Uses `getRunStatus(spaceId, runId)` from `lib/api/schedule.ts` (task 10.4) to poll the backend.
- The `onCompleted` callback receives the `resultVersionId` which the parent uses to navigate to the draft review panel (Req 4.4).
- Backend endpoint: `GET /spaces/{spaceId}/schedule-runs/{runId}` returns `{ status, resultVersionId, errorSummary }`.

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/schedule/regenerationStatusIndicator.test.tsx
```

All 20 tests should pass, covering:
- Queued/running/completed/failed state rendering
- Polling start/stop behavior
- `onCompleted` callback invocation with `resultVersionId`
- "Review Draft" button click behavior
- Dismiss button visibility and callback
- Accessibility attributes (role, aria-live, aria-label)

## What comes next

- Task 10.1 integration: wire `RegenerationStatusIndicator` into the schedule management panel, passing `runId` from the confirm dialog's `onSuccess` callback.
- The parent component should handle `onCompleted` by navigating to the draft review panel route.

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): RegenerationStatusIndicator component with polling and draft review"
```
