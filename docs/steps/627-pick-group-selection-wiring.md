# 627 — Pick Group Selection & Last-Group Memory Wiring

## Phase

Feature: Shift Picker Lite (Task 8.2)

## Purpose

Wire the group selection flow and last-group memory persistence into the `/pick` route page, ensuring that selecting a group stores it in localStorage, navigating back returns to the group selector, and switching groups updates the stored memory.

## What was built

This task was already fully implemented as part of task 8.1 (`apps/web/app/pick/page.tsx`). Verification confirmed all sub-tasks are satisfied:

| Sub-task | Implementation |
|----------|---------------|
| Store group ID via `setLastGroup` on selection, transition to `slot-browser` | `handleGroupSelect` callback calls `setLastGroup(groupId)`, sets `selectedGroupId`, `selectedGroupName`, resets `activeTab` to `"slots"`, and sets `phase` to `"slot-browser"` |
| Back button sets phase to `group-select` | `handleBack` callback sets `phase` to `"group-select"`, wired to `PickerHeader.onBack` |
| Switching group updates `Last_Group_Memory` | Re-entering group selector and selecting a new group triggers `handleGroupSelect` again, which calls `setLastGroup` with the new group ID |

## Key decisions

- **No additional code changes needed** — task 8.1 already implemented the full orchestration logic including group selection wiring.
- The `handleGroupSelect` callback resets `activeTab` to `"slots"` when switching groups, ensuring a clean state.
- The back button is always visible in `PickerHeader` (even in `group-select` phase it shows the app title), providing consistent navigation.

## How it connects

- **Requirement 2.4**: Group selection stores ID in `Last_Group_Memory` and navigates to slot browsing view ✓
- **Requirement 3.3**: Back button allows returning to `Group_Selector` from slot browsing view ✓
- **Requirement 3.4**: Switching groups updates `Last_Group_Memory` to the new group ID ✓
- Depends on: `pickLastGroup.ts` (task 1.1), `GroupSelector` (task 6.1), `PickerHeader` (task 5.1)
- Consumed by: task 8.3 (wiring tab content into the slot-browser phase)

## How to run / verify

1. Navigate to `/pick` while authenticated
2. Select a group → verify localStorage key `shifter-pick-last-group` is set to the group ID
3. Verify the view transitions to the slot-browser phase (tabs visible)
4. Tap the back button → verify the group selector reappears
5. Select a different group → verify localStorage updates to the new group ID
6. Refresh the page → verify it skips directly to the last selected group

## What comes next

- Task 8.3: Wire `SlotBrowserTab` and `MyShiftsTab` into the slot-browser phase tab panels.

## Git commit

```bash
git add -A && git commit -m "feat(pick): verify group selection and last-group memory wiring (task 8.2)"
```
