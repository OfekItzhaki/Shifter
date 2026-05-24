# 544 — ManagementTimeoutCard Component

## Phase

Phase — Space Management Frontend

## Purpose

Implements the `ManagementTimeoutCard` component for the space settings page (`/spaces/settings`). This card allows the Space Owner to view and update the space-level management timeout value (5–120 minutes). The management timeout controls how long admin sessions remain active before requiring re-authentication across all groups in the space.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/spaces/ManagementTimeoutCard.tsx` | New card component with number input, save button, client-side validation [5, 120], API call, and success/error feedback |
| `apps/web/app/spaces/settings/page.tsx` | Integrated ManagementTimeoutCard into the settings page (rendered before billing card) |
| `apps/web/lib/api/spaces.ts` | Added `managementTimeoutMinutes` field to `SpaceDetailDto` interface |
| `apps/api/Jobuler.Application/Spaces/Queries/GetSpaceDetailQuery.cs` | Added `ManagementTimeoutMinutes` to the backend `SpaceDetailDto` record and query handler |
| `apps/web/messages/en.json` | Added `spaces.managementTimeout.*` translation keys (English) |
| `apps/web/messages/he.json` | Added `spaces.managementTimeout.*` translation keys (Hebrew) |
| `apps/web/messages/ru.json` | Added `spaces.managementTimeout.*` translation keys (Russian) |

## Key decisions

1. **Permission gating via `isOwner` prop**: The component returns `null` when `isOwner` is false, matching the pattern used by `SpaceBillingCard`.
2. **Client-side validation**: Validates that the input is an integer in [5, 120] before calling the API, providing immediate feedback.
3. **Inline success/error messages**: Uses simple text messages below the input rather than a floating toast, keeping the UX lightweight and consistent with the card's contained layout.
4. **Backend DTO extension**: Added `ManagementTimeoutMinutes` to the existing `SpaceDetailDto` so the settings page can display the current value without an extra API call.

## How it connects

- Uses `updateManagementTimeout` from `@/lib/api/spaces` (already defined in task 13.1)
- Reads `managementTimeoutMinutes` from `SpaceDetailDto` returned by `getSpaceDetail`
- Rendered inside the space settings page alongside other owner-only cards
- The backend `UpdateManagementTimeoutCommand` (task 7.1) handles the actual persistence

## How to run / verify

1. Navigate to `/spaces/settings` as a space owner
2. The "Management Mode Timeout" card should appear with the current value
3. Enter a value outside [5, 120] → validation error appears
4. Enter a valid value and click Save → success message appears
5. As a non-owner, the card should not be visible

## What comes next

- Task 14.2: Unit tests for ManagementTimeoutCard (renders current value, validation, API call, hidden for non-owners)
- Task 15.1: HomeLeaveConfigCard component

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add ManagementTimeoutCard component to space settings"
```
