# 643 — Linked Group UI (Frontend)

## Phase
Space-First Onboarding — Task 14

## Purpose
Implements the frontend UI for parent-child group linking: a dropdown selector in group settings (admin only), parent/child relationship indicators in the group list view, an explicit unlink action, and single-level hierarchy validation in the UI.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/groups/LinkedGroupSelector.tsx` | Enhanced dropdown component for selecting/removing a parent group. Now includes explicit unlink button, disabled state for groups that are already parents (single-level enforcement), and a message when the current group cannot become a child. |
| `apps/web/app/groups/page.tsx` | Updated group list view to show parent/child relationship indicators (icons + text) on each group card. |
| `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` | Removed `as any` casts for `parentGroupId` now that the type is properly defined. |
| `apps/web/lib/query/hooks/useGroups.ts` | Added `parentGroupId` field to `GroupDto` interface. |
| `apps/web/lib/api/groups.ts` | Added `parentGroupId` field to `GroupWithMemberCountDto` interface. |
| `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs` | Added `ParentGroupId` to the `GroupDto` record and included it in the query projection. |
| `apps/web/messages/en.json` | Added `childOf`, `parentOf` keys to `groups` and `unlink`, `cannotBeChild`, `alreadyParent` keys to `groups.linkedGroup`. |
| `apps/web/messages/he.json` | Same translation keys in Hebrew. |
| `apps/web/messages/ru.json` | Same translation keys in Russian. |

## Key decisions
- Single-level hierarchy is enforced in the UI by disabling groups that are already parents in the dropdown (shown with "(already a parent)" suffix).
- If the current group is already a parent of other groups, the selector shows a warning message instead of the dropdown.
- Parent/child indicators use small colored text with directional arrows (up arrow for child, down arrow for parent) on group cards.
- The unlink button is shown inline next to the "Linked to" text when a parent is set.

## How it connects
- Uses `linkParentGroup()` and `unlinkParentGroup()` from `lib/api/spaces.ts` (Task 9).
- Backend validation (Task 7) enforces same-space and single-level rules server-side.
- The `LinkedGroupSelector` is already integrated into `SettingsTab.tsx` (admin-only gated).
- The `parentGroupId` field is now returned by the `GET /spaces/{spaceId}/groups` endpoint.

## How to run / verify
1. Navigate to a group's Settings tab as an admin — the "Parent Group" selector should appear.
2. Select a parent group — it should link successfully.
3. Click "Unlink" — it should remove the parent relationship.
4. Groups that are already parents of other groups should appear disabled in the dropdown.
5. Navigate to the groups list — parent/child indicators should appear on linked group cards.

## What comes next
- Task 15: Update onboarding store for per-space state.
- Task 16: Solver integration for parent schedule cascading.

## Git commit
```bash
git add -A && git commit -m "feat(space-onboarding): linked group UI with parent/child indicators and unlink action"
```
