# 617 — Admin Overrides Tab Component

## Phase
Self-Service Scheduling UI

## Purpose
Implements the admin override panel that allows group admins to manually assign or remove members from shift slots. This is the final admin-facing tab in the self-service scheduling feature, enabling exception handling without direct API usage.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/groups/[groupId]/tabs/AdminOverridesTab.tsx` | Full admin overrides tab component with assign/remove flows, permission gating, loading/error states |

## Key decisions

- **Permission prop pattern**: Uses `hasSchedulePublishPermission` boolean prop passed from the parent page, consistent with how `isAdmin` and `hasBillingPermission` are used elsewhere in the codebase.
- **Inline assign picker**: Instead of a modal for assignment, uses an inline dropdown that appears below the slot — reduces modal fatigue and keeps context visible.
- **Modal for remove confirmation**: Uses the existing `Modal` component for destructive remove actions, consistent with the cancel dialog pattern in `MyShiftsTab`.
- **Local state tracking**: Tracks assigned members per slot in local state (`slotAssignments`) since the `getAvailableSlots` API returns fill counts but not individual assignments. Assignments made during the session are tracked and displayed.
- **Optimistic UI updates**: After successful assign/remove, updates both the local assignments list and the slot fill count without a full refetch.
- **Member filtering**: The assign picker filters out members already assigned to the slot, preventing duplicate assignment attempts.

## How it connects

- Uses `getAvailableSlots`, `adminAssignMember`, `adminRemoveMember` from `lib/api/selfService.ts`
- Uses `getSelfServiceErrorMessage` from `lib/utils/selfServiceErrors.ts` for error display
- Uses `formatSlotDate`, `formatTime24h`, `HEBREW_DAY_NAMES` from `lib/utils/selfServiceFormat.ts`
- Uses `GroupMemberDto` from `lib/api/groups.ts` for the member picker
- Uses the shared `Modal` component for remove confirmation
- Uses i18n keys from `selfService.adminOverrides.*` namespace
- Will be lazy-loaded and rendered by the group detail page when the admin-overrides tab is active

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

The component can be visually verified by navigating to a self-service group's admin-overrides tab in the browser (requires backend running with self-service scheduling enabled).

## What comes next

- Integration into the group detail page tab rendering (wiring the lazy import and passing props)
- Unit tests for the AdminOverridesTab component

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): admin overrides tab component"
```
