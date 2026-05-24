# 519 — triggerRegeneration API Client Function

## Phase

Schedule Regeneration — Frontend API Layer

## Purpose

Adds the `triggerRegeneration` client function to the frontend schedule API module, enabling the UI to trigger a schedule regeneration run via the backend endpoint. This function is the bridge between the frontend regeneration button/dialog and the backend `POST /spaces/{spaceId}/schedule-runs/regenerate` endpoint.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/api/schedule.ts` | Added `triggerRegeneration(spaceId, groupId)` function that POSTs to `/spaces/${spaceId}/schedule-runs/regenerate` with `{ groupId }` body and returns `{ runId: string }` |

## Key decisions

- **Reuse existing `apiClient`**: Follows the same pattern as `triggerSolve` and other schedule API functions — uses the shared axios instance with auth interceptors.
- **Reuse existing `getRunStatus`**: No new polling function needed. The existing `getRunStatus(spaceId, runId)` already polls `GET /schedule-runs/{runId}` and returns the run status, which the regeneration status indicator will use.
- **Minimal function**: The function only handles the trigger POST. Error handling (402, 403, 409) is left to the calling component, consistent with how other API functions work in this codebase.

## How it connects

- Called by `RegenerateConfirmDialog` (task 10.2) when the admin confirms regeneration
- Returns `runId` which is passed to `RegenerationStatusIndicator` (task 10.3) for polling via `getRunStatus`
- Hits the backend endpoint created in task 5.1 (`ScheduleRunsController.Regenerate`)

## How to run / verify

```bash
# TypeScript compilation check
cd apps/web && npx tsc --noEmit
```

The function can also be verified by importing it in a component and calling it with valid space/group IDs — it should return a `{ runId }` response on success or throw axios errors (402/403/409) on failure.

## What comes next

- Task 10.1: `RegenerateButton` component that triggers the confirmation dialog
- Task 10.2: `RegenerateConfirmDialog` that calls `triggerRegeneration` on confirm
- Task 10.3: `RegenerationStatusIndicator` that polls `getRunStatus` with the returned `runId`

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): add triggerRegeneration API client function"
```
