# 545 — Role Assignment Card Component

## Phase

Phase — Space Management Frontend

## Purpose

Implements the `RoleAssignmentCard` component for the space settings page, allowing the Space Owner to view all space members and assign permission levels (Member, Admin, GroupOwner) via a dropdown per member. This fulfills Requirement 4.6 of the space-management spec.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/spaces/RoleAssignmentCard.tsx` | New component that loads members + permission levels, displays a dropdown per member, calls `assignSpaceRole` API on change, shows success/error toast. Hidden for non-owners. |
| `apps/web/app/spaces/settings/page.tsx` | Added import and rendered `RoleAssignmentCard` in the settings page layout. |
| `apps/web/messages/en.json` | Added `spaces.roleAssignment.*` translation keys (title, description, loading, error, success, level labels). |
| `apps/web/messages/he.json` | Added Hebrew translations for `spaces.roleAssignment.*`. |

## Key decisions

1. **Inline toast instead of floating toast** — Matches the pattern used by `ManagementTimeoutCard` (inline success/error messages below the card) rather than a global floating toast, keeping the UX consistent within the settings page.
2. **SpaceOwner shown as badge, not dropdown** — The Space Owner's role is displayed as a non-editable badge since it cannot be reassigned via this UI.
3. **Optimistic local state update** — After a successful API call, the local state is updated immediately without re-fetching the full member list.
4. **Combined data from two endpoints** — Merges `getSpaceMembers` (for display names/emails) with `getSpacePermissionLevels` (for role data) since the permission levels endpoint only returns userId + level.

## How it connects

- Uses API functions from `lib/api/spaces.ts` (task 13.1): `getSpaceMembers`, `getSpacePermissionLevels`, `assignSpaceRole`
- Rendered inside the space settings page alongside `ManagementTimeoutCard`, `InviteCodeCard`, and `SpaceBillingCard`
- Backend endpoints from task 11.4 (`PUT /spaces/{spaceId}/members/{userId}/role`, `GET /spaces/{spaceId}/members/roles`)

## How to run / verify

1. Navigate to `/spaces/settings` as a Space Owner
2. The "Role Assignment" card should appear showing all members with their current roles
3. Change a member's role via the dropdown — a success toast should appear
4. Non-owners should not see the card at all

## What comes next

- Task 17.2: Unit tests for `RoleAssignmentCard` (renders members, dispatches API, hidden for non-owners)

## Git commit

```bash
git add -A && git commit -m "feat(space-management): role assignment card component"
```
