# 601 — Write Guard UI Controls

## Phase

Offline Cache Resilience — Task 8.2

## Purpose

Apply the `useWriteGuard` hook to critical mutation buttons so users get visual feedback (disabled state + tooltip) when the app is offline or the server is unavailable. The axios interceptor already blocks all mutations at the network level; this is UX polish so users understand why actions are unavailable.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/groups/page.tsx` | Import `useWriteGuard`, disable "Create Group" button when disconnected, wrap in `<span title>` for tooltip |
| `apps/web/components/schedule/RegenerateButton.tsx` | Import `useWriteGuard`, combine with existing `isRegenerationInProgress` disabled state, wrap in `<span title>` for tooltip |
| `apps/web/components/spaces/DangerZoneCard.tsx` | Import `useWriteGuard`, disable "Transfer Ownership" and "Delete Space" buttons when disconnected, wrap in `<span title>` for tooltip |

## Key decisions

- **Tooltip via `<span title>`** — simple native tooltip that works without additional UI library dependencies. The `title` attribute is only set when the button is disabled (empty string otherwise).
- **Combine with existing disabled conditions** — the write guard disabled state is OR'd with any pre-existing disabled logic (e.g., `!transferTarget`, `isRegenerationInProgress`).
- **Only primary action buttons** — confirmation buttons inside the confirm dialogs are not guarded because the user can't reach them if the initial button is disabled.
- **Automatic re-enable** — the `useWriteGuard` hook subscribes to the Zustand connectivity store, so buttons re-enable reactively when connectivity restores without any extra code.

## How it connects

- Depends on `lib/api/writeGuard.ts` (task 4.2) which provides the `useWriteGuard` hook
- Depends on `lib/store/connectivityStore.ts` (task 1.1) which tracks connectivity state
- Complements the axios request interceptor that blocks mutations at the network level

## How to run / verify

1. Start the dev server: `npm run dev` in `apps/web`
2. Open Chrome DevTools → Network → toggle "Offline"
3. Verify the "Create Group" button on `/groups` becomes disabled with a Hebrew tooltip
4. Verify the "Regenerate Schedule" button (if visible) becomes disabled
5. Verify the "Delete Space" and "Transfer Ownership" buttons in Space Settings become disabled
6. Toggle back online — all buttons should re-enable within ~2 seconds

## What comes next

- Task 8.3: Integration tests for the full offline flow

## Git commit

```bash
git add -A && git commit -m "feat(offline): apply useWriteGuard to mutation UI controls"
```
