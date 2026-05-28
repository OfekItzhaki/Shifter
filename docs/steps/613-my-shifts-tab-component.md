# 613 — My Shifts Tab Component

## Phase

Self-Service Scheduling UI — Member Tab Components

## Purpose

Implements the `MyShiftsTab` component that allows group members to view their shift requests for the current scheduling cycle, grouped by status (approved, pending, cancelled). Members can cancel approved shifts within the cancellation window by providing a reason.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/groups/selfService/MyShiftsTab.tsx` | Main component: fetches shift requests, groups by status, renders shift cards with status badges, cancel dialog with reason validation, shift count indicator, and under-scheduled warning |

## Key decisions

- **Status grouping order**: Approved → Pending → Cancelled/Rejected, matching the design doc requirement (Property 7)
- **Cancellation window check**: Client-side check compares shift start datetime against `cancellationCutoffHours` to determine if the cancel button should be shown
- **Validation before API call**: Uses `validateCancellationReason` from the shared validation utilities to enforce 1-500 character limit before calling the cancel endpoint
- **Error handling**: Uses `getSelfServiceErrorMessage` for consistent error display; shows retry button on fetch failure
- **Component decomposition**: Split into `ShiftSection` and `ShiftCard` sub-components for readability
- **Follows existing patterns**: Uses `useTranslations` from next-intl, same loading skeleton and error state patterns as `LiveStatusPanel`

## How it connects

- Consumes `getMyShiftRequests` and `cancelShiftRequest` from `lib/api/selfService.ts`
- Uses formatting utilities from `lib/utils/selfServiceFormat.ts` (`formatSlotDate`, `formatTime24h`, `HEBREW_DAY_NAMES`)
- Uses validation from `lib/utils/selfServiceValidation.ts` (`validateCancellationReason`)
- Uses error mapping from `lib/utils/selfServiceErrors.ts` (`getSelfServiceErrorMessage`)
- Uses i18n keys from `selfService.myShifts.*` namespace
- Uses the shared `Modal` component for the cancellation dialog
- Will be lazy-loaded by the group detail page when the "my-shifts" tab is active (wired in task 15.1)

## How to run / verify

- The component has no TypeScript diagnostics
- Will be visually testable once wired into the group detail page tab navigation (task 15.1)
- Property tests for grouping and display logic will be added in task 6.2

## What comes next

- Task 6.2: Property tests for my shifts display logic (Properties 7, 8)
- Task 7.1: WaitlistTab component
- Task 15.1: Wire this tab into the group detail page tab navigation

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): implement MyShiftsTab component"
```
