# 546 — Danger Zone Card Component

## Phase

Phase — Space Management Frontend

## Purpose

Implements the `DangerZoneCard` component for the space settings page, providing a visually distinct section for destructive actions: soft-deleting the space and transferring ownership to another member. Both actions require confirmation dialogs before execution.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/spaces/DangerZoneCard.tsx` | New component with red-bordered danger zone section, delete space button with confirmation, transfer ownership dropdown (excludes current owner) with confirmation, success/error toasts |
| `apps/web/app/spaces/settings/page.tsx` | Added DangerZoneCard import and rendered it at the bottom of the cards list, added useAuthStore for currentOwnerId |
| `apps/web/messages/en.json` | Added `spaces.dangerZone.*` translation keys (English) |
| `apps/web/messages/he.json` | Added `spaces.dangerZone.*` translation keys (Hebrew) |

## Key decisions

1. **Permission gate via `isOwner` prop** — Component returns null for non-owners, consistent with other settings cards.
2. **Current owner ID from auth store** — Since the backend doesn't expose `ownerUserId` in `SpaceDetailDto`, we use the authenticated user's ID (which is the owner when `isOwner` is true).
3. **Inline confirmation pattern** — Matches the existing pattern used in `InviteCodeCard` (inline confirm/cancel buttons) rather than a modal dialog.
4. **Transfer target dropdown excludes current owner** — Filters `members` array by `userId !== currentOwnerId` per Requirement 9.4.
5. **Red border/background styling** — Uses `border-2 border-red-300 dark:border-red-700 bg-red-50 dark:bg-red-950/20` to visually distinguish from other cards.

## How it connects

- Uses `softDeleteSpace` and `transferOwnership` from `apps/web/lib/api/spaces.ts` (created in task 13.1)
- Receives `members` from the parent settings page which already fetches them via `getSpaceMembers`
- Follows the same card styling and toast pattern as `ManagementTimeoutCard` and `RoleAssignmentCard`
- Satisfies Requirements 9.1–9.5 from the space-management spec

## How to run / verify

1. Navigate to `/spaces/settings` as a space owner
2. Scroll to the bottom — the red-bordered "Danger Zone" card should be visible
3. Click "Delete Space" — confirmation should appear inline
4. Select a member from the transfer dropdown — "Transfer" button should enable
5. Click "Transfer" — confirmation should appear inline
6. As a non-owner, the card should not render at all

## What comes next

- Task 16.2: Property test for transfer target dropdown (Property 14)
- Task 16.3: Unit tests for DangerZoneCard

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add DangerZoneCard component with delete and transfer ownership"
```
