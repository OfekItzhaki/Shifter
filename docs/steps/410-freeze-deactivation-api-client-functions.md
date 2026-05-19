# 410 — Freeze Deactivation API Client Functions

## Phase

Feature — Freeze Period Discard (Frontend Layer)

## Purpose

Provides typed API client functions for the frontend to communicate with the freeze deactivation backend endpoints. These functions enable the deactivation dialog to fetch the count of freeze-period changes and to submit the deactivation request with an optional discard flag.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/homeLeave.ts` | Added `FreezePeriodChangesCountDto` and `DeactivateFreezeResponse` interfaces, plus `getFreezePeriodChangesCount()` and `deactivateFreeze()` functions |

## Key decisions

- **Placed in existing module** — Both functions were added to the existing `lib/api/homeLeave.ts` module alongside other home-leave API calls, following the established pattern.
- **Typed response DTOs** — `FreezePeriodChangesCountDto` mirrors the backend `FreezePeriodChangesCountResult` (overrideCount, manualAssignmentCount, swapCount, totalCount). `DeactivateFreezeResponse` mirrors `DeactivateFreezeResult` (discardPerformed, discardVersionId, discardedChangeCount, config).
- **Consistent patterns** — Both functions use the same `apiClient` instance and destructuring pattern as all other functions in the module.

## How it connects

- **Backend endpoints** — `GET .../freeze-period-changes-count` (task 1.3) and `POST .../deactivate-freeze` (task 4.1) are already implemented in `HomeLeaveConfigController`.
- **Frontend consumers** — The upcoming `FreezeDeactivationDialog` component (task 7.2) will call these functions to display change counts and submit the admin's deactivation choice.

## How to run / verify

```bash
# TypeScript compilation check
cd apps/web && npx tsc --noEmit
```

No runtime test needed — these are thin API wrappers. Integration is verified when the dialog component (task 7.2) is wired up.

## What comes next

- Task 7.2: `FreezeDeactivationDialog` component that uses these functions
- Task 7.3: Integration into `EmergencyFreezeBanner`

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): add API client functions for freeze deactivation"
```
