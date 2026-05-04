# Step 097 — "Can't Make It" Quick-Action

## Phase
Phase 9 — Quality & Polish

## Purpose
Admins needed a fast way to handle the situation where someone on the published schedule can't make their shift. Previously they had to navigate to the member's profile, add a presence window manually, then go back and re-run the solver. This step adds a one-click flow directly from the schedule table.

## What was built

### `CantMakeItModal.tsx` (new)
- Modal triggered by clicking the ⚠ icon next to a person's name in the schedule table.
- Pre-fills start time to now and end time to end of today.
- Admin can adjust the time range and add an optional note.
- Checkbox: "Re-run solver after saving" (checked by default).
- On save: calls `POST /spaces/{spaceId}/people/{personId}/presence` with state `at_home`.
- On success: closes and calls `onSaved(triggerRerun)` so the parent can trigger a solver re-run.
- Fully localised (Hebrew/English/Russian via locale detection).

### `ScheduleTaskTable.tsx` (updated)
- `TaskAssignment` interface gains optional `personId: string` field.
- Slot map now tracks `personIds[]` alongside `people[]` (names).
- New props: `isAdmin`, `spaceId`, `onPersonBlocked`.
- When `isAdmin` is true, each person cell shows a ⚠ icon on hover.
- Clicking the icon opens `CantMakeItModal` for that person.
- After the modal saves, calls `onPersonBlocked(personId, triggerRerun)`.

### `ScheduleTab.tsx` (updated)
- New props: `spaceId`, `onTriggerSolver`.
- Passes `isAdmin`, `spaceId`, and `onPersonBlocked` to `ScheduleTaskTable`.
- `onPersonBlocked` calls `onTriggerSolver()` if the admin checked "re-run solver".

### `page.tsx` (updated)
- Passes `spaceId={currentSpaceId}` and `onTriggerSolver={handleTriggerSolver}` to `ScheduleTab`.

## How it works end-to-end
1. Admin sees the published schedule.
2. Hovers over a person's name → ⚠ icon appears.
3. Clicks ⚠ → modal opens pre-filled with "blocked from now until end of today".
4. Admin adjusts the range (e.g. "blocked until tomorrow 12:00") and adds a note.
5. Clicks Save → presence window is created via the API.
6. If "re-run solver" was checked → solver is triggered immediately.
7. The solver excludes the blocked person from their shifts and reassigns to others, minimising changes to the rest of the schedule.

## Key decisions
- Uses `at_home` state (not `blocked`) — `at_home` means "not available for shifts" which is the correct semantic for "can't make it". `blocked` is for hard unavailability like medical leave.
- The solver already handles `at_home` as a blocking state in `add_availability_constraints`.
- The modal is self-contained and doesn't require any new API endpoints.

## Git commit

```bash
git add -A && git commit -m "feat(schedule): cant-make-it quick action — block person and optionally re-run solver"
```
