# Step 074 — Fix Publish Schedule Button

## Phase
Phase 4 — Scheduling Engine

## Purpose
Pressing "פרסם סידור" (Publish Schedule) did nothing. The API endpoint worked
correctly — the bug was entirely in the frontend. Two issues combined to cause
silent failure:

1. **Stale closure / Zustand hydration lag** — `handlePublish` captured
   `currentSpaceId` from the React closure. On first render, Zustand's `persist`
   middleware hasn't rehydrated from localStorage yet, so `currentSpaceId` is
   `null`. The guard `if (!currentSpaceId || !draftVersion) return;` fired
   silently with no feedback to the user.

2. **Swallowed error / no re-throw** — the `catch` block set
   `scheduleVersionError` but didn't re-throw, so `DraftScheduleModal`'s own
   error handler never fired and the modal showed nothing.

3. **Wrong prop type** — `ScheduleTab.onPublish` was typed as `() => void`
   instead of `() => Promise<void>`, meaning the button click didn't await the
   async function and the `publishSaving` loading state never showed.

## What was built

### Modified files

- **`apps/web/app/groups/[groupId]/page.tsx`**
  - `handlePublish` now reads `currentSpaceId` via
    `useSpaceStore.getState().currentSpaceId` at call time (bypasses stale
    closure) with the React state as fallback.
  - Shows a user-visible error (`"לא ניתן לפרסם — נסה לרענן את הדף"`) instead
    of silently returning when space ID is missing.
  - Re-throws the error after setting `scheduleVersionError` so
    `DraftScheduleModal` can also display it.

- **`apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx`**
  - `onPublish` and `onDiscard` prop types changed from `() => void` to
    `() => Promise<void>` so the button properly awaits the async handler and
    the loading state (`publishSaving`) works correctly.

## Key decisions

- Used `useSpaceStore.getState()` (Zustand's imperative getter) instead of the
  React hook to read the latest store value at call time, avoiding the stale
  closure problem without adding a `useEffect` or extra state.
- Re-throwing the error preserves the existing error display in both the inline
  banner (`scheduleVersionError`) and the modal.

## How it connects

The publish flow: `ScheduleTab` button / `DraftScheduleModal` button →
`handlePublish` in `page.tsx` → `POST /spaces/{id}/schedule-versions/{id}/publish`
→ `PublishVersionCommand` → archives old published version, publishes draft,
notifies all members.

## How to run / verify

1. Trigger a solver run and wait for a draft to appear.
2. Click "פרסם סידור" from either the draft banner or the modal.
3. The button should show "מפרסם..." while saving, then the draft banner
   disappears and the published schedule loads.

## What comes next

- Manual override assignments (urgent double-shift feature)
- Live person status panel (who's on mission / at home)

## Git commit

```bash
git add -A && git commit -m "fix(web): publish button silent failure — stale closure and swallowed error"
```
