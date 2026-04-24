# Step 030 — Group Detail Page UI Fixes

## Phase
Phase 7 — UX Polish

## Purpose
Fix three UX issues in the group detail page that caused confusion or missing functionality:
1. Two "חברים" tabs were shown simultaneously (members-readonly + members-edit), which was confusing.
2. The משימות (tasks) panel was read-only for admins — no way to create task types or slots from the group page.
3. The אילוצים (constraints) panel was read-only for admins — no way to create constraints from the group page.

## What was built

### Modified
- `apps/web/app/groups/[groupId]/page.tsx`
  - **Fix 1 — Merged members tabs**: Removed `members-readonly` and `members-edit` from `ActiveTab` type and tab arrays. Added a single `"members"` tab that renders `renderMembersReadOnly()` for non-admins and `renderMembersEdit()` for admins. Updated `ADMIN_ONLY_TABS`, the members `useEffect` trigger, and the `renderTabPanel()` switch.
  - **Fix 2 — Tasks create forms**: Added state variables and `handleCreateTaskType` / `handleCreateSlot` handlers. `renderTasksPanel()` now shows a "+ סוג משימה" or "+ חלון זמן" toggle button (admin only, context-sensitive per sub-tab) and inline create forms with all required fields.
  - **Fix 3 — Constraints create form**: Added state variables and `handleCreateConstraint` handler. `renderConstraintsPanel()` now shows a "+ אילוץ" toggle button (admin only) and an inline create form with scope, severity, rule type, and JSON payload fields.
  - Imported `createTaskType`, `createTaskSlot` from `@/lib/api/tasks` and `createConstraint` from `@/lib/api/constraints`.

## Key decisions
- Single members tab with conditional rendering keeps the tab bar clean for all users.
- Create forms are toggled inline (not modals) to match the existing admin pages pattern.
- The constraint rule type select auto-populates the payload field with sensible defaults, matching the admin constraints page behavior.

## How it connects
- Calls the same API functions (`createTaskType`, `createTaskSlot`, `createConstraint`) already used by the admin pages.
- Admin mode is gated by `isAdmin === (adminGroupId === groupId)` — unchanged from before.

## How to run / verify
1. Open a group detail page and confirm only one "חברים" tab appears.
2. Enter admin mode — the tab still shows "חברים" but now renders the add-by-email form + remove buttons.
3. Navigate to משימות → confirm "+ סוג משימה" and "+ חלון זמן" buttons appear per sub-tab.
4. Navigate to אילוצים → confirm "+ אילוץ" button appears and the form works.
5. Run `node node_modules/typescript/bin/tsc --noEmit` from `apps/web` — should exit 0.

## What comes next
- Delete / edit actions for task types, slots, and constraints from the group detail page.

## Git commit
```bash
git add -A && git commit -m "fix(group-detail): merge members tabs, add task/constraint create forms"
```
