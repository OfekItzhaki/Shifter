# 625 — GroupSelector Component

## Phase

Shift Picker Lite — UI Components

## Purpose

Implements the `GroupSelector` component for the `/pick` route. This component displays a list of self-service group cards that the member can tap to select a group for shift browsing. It handles the empty state when no self-service groups are available.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/pick/GroupSelector.tsx` | Client component that renders group cards with name and member count, or an empty-state message |

## Key decisions

- **Card-based layout**: Each group is rendered as a full-width button card with the group name on the start side and member count on the end side, matching the existing card patterns in `LoadingCard` and `ErrorRetry`.
- **44x44px minimum tap targets**: Cards use `min-h-[44px]` with padding to ensure comfortable touch interaction on mobile.
- **16px body text**: Group names use `text-base` (16px) for readability without zooming.
- **i18n via next-intl**: All user-visible strings (`noGroups`, `memberCount`) are resolved from the `pick` namespace — no hardcoded Hebrew text.
- **Empty state pattern**: Follows the same visual pattern as `ErrorRetry` — centered icon, message text, rounded card container.
- **Semantic HTML**: Uses `<button>` elements for group cards to ensure keyboard accessibility and proper focus management.

## How it connects

- Consumed by the `PickPage` route component (task 8.1) during the `group-select` phase.
- Receives filtered/sorted groups from `filterSelfServiceGroups` utility (task 2.1).
- Calls `onSelect(groupId, groupName)` which triggers `setLastGroup` and phase transition to `slot-browser`.
- Uses `GroupWithMemberCountDto` from `@/lib/api/groups`.
- Uses `pick.noGroups` and `pick.memberCount` i18n keys added in task 4.1.

## How to run / verify

```bash
# Type-check
cd apps/web && npx tsc --noEmit
```

The component will be visually testable once wired into the PickPage route (task 8.1).

## What comes next

- Task 7.1: PickerTabs component
- Task 8.1: PickPage route wiring (consumes GroupSelector)

## Git commit

```bash
git add -A && git commit -m "feat(pick): implement GroupSelector component"
```
