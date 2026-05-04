# Step 107 — Notification Bell Portal Fix

## Phase
Phase 9 — Polish & Hardening

## Purpose
The notification dropdown was being clipped by the sidebar's stacking context. The sidebar uses `overflowY: auto` which, combined with `position: fixed` on the sidebar itself, creates a new stacking context that traps any `position: fixed` children — even with a high `z-index`. The dropdown appeared behind page content instead of floating above everything.

## Root cause
`AppShell`'s sidebar style:
```ts
sidebar: { ..., position: "fixed", overflowY: "auto", zIndex: 30 }
```
Any `position: fixed` element rendered *inside* a `position: fixed` + `overflow: auto` ancestor is positioned relative to that ancestor's stacking context, not the viewport. `z-index: 9999` had no effect because the dropdown never escaped the sidebar's stacking context.

## What was built

### `apps/web/components/shell/NotificationBell.tsx` (modified)
- Imported `createPortal` from `react-dom`
- Added `buttonRef` (on the bell button) and `dropdownRef` (on the dropdown panel) — replacing the old single wrapper `ref`
- Added `mounted` state to guard against SSR portal rendering
- Added `dropdownPos` state — on open, reads `buttonRef.current.getBoundingClientRect()` to anchor the dropdown just below the bell button
- Renders the dropdown via `createPortal(..., document.body)` so it escapes all stacking contexts and sits directly on `<body>`
- `z-index: 99999` on the portal ensures it floats above everything
- Outside-click handler updated to check both `buttonRef` and `dropdownRef` (since they're no longer in the same DOM subtree)
- Added `direction: ltr` overrides on the header and list rows (the outer `direction: rtl` scroll trick is preserved on the container)

## Key decisions
- **Portal over z-index hacks**: Increasing z-index further would not have helped — the stacking context was the constraint. A portal is the correct, permanent fix.
- **Dynamic positioning via `getBoundingClientRect`**: Anchors the dropdown to the actual bell button position rather than hardcoding pixel offsets, making it resilient to layout changes.
- **`mounted` guard**: Prevents `document.body` access during SSR, which would throw in Next.js.

## How it connects
- `NotificationBell` is rendered inside `AppShell`'s sidebar logo area
- The portal renders into `document.body`, completely outside the sidebar DOM tree
- No changes to `AppShell`, query hooks, or notification API

## How to run / verify
1. Start the dev server
2. Open any page — click the bell icon in the sidebar
3. The notification dropdown should appear on top of all page content, including tables, modals, and sticky headers
4. Clicking outside the dropdown closes it
5. Dismiss and "mark all read" buttons still work

## What comes next
- Continue with the schedule-table-autoschedule-role-constraints spec tasks

## Git commit

```bash
git add -A && git commit -m "fix(shell): render notification dropdown via portal to escape sidebar stacking context"
```
