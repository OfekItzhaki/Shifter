# Step 302 — Sandbox Navigation Guards

## Phase

Phase: Draft Simulation Sandbox (Task 8.3)

## Purpose

Prevents admins from accidentally losing unsaved simulation sandbox changes by navigating away. Implements two layers of protection:
1. Browser-native `beforeunload` dialog for tab close/refresh
2. In-app confirmation dialog for Next.js App Router navigation (intercepting `history.pushState` and `popstate` events)

When the sandbox is inactive, all listeners are cleaned up automatically.

## What was built

| File | Description |
|------|-------------|
| `apps/web/hooks/useSandboxNavigationGuard.ts` | Custom hook that subscribes to sandbox `isActive` state, adds `beforeunload` listener, monkey-patches `history.pushState`/`replaceState` to intercept in-app navigation, and handles browser back/forward via `popstate` |
| `apps/web/components/sandbox/SandboxNavigationGuardDialog.tsx` | Modal dialog component with warning icon, localized message, and Confirm/Cancel buttons |
| `apps/web/components/sandbox/SandboxSettingsPanel.tsx` | Updated to import and use the navigation guard hook + render the dialog |
| `apps/web/messages/en.json` | Added `sandbox.navigationGuard.*` i18n keys (English) |
| `apps/web/messages/he.json` | Added `sandbox.navigationGuard.*` i18n keys (Hebrew) |
| `apps/web/messages/ru.json` | Added `sandbox.navigationGuard.*` i18n keys (Russian) |

## Key decisions

1. **Monkey-patching `history.pushState`** — Next.js App Router doesn't expose `router.events` like the Pages Router. The standard approach for intercepting client-side navigation in App Router is to patch `history.pushState` since that's what Next.js calls internally for route transitions.

2. **Same-path navigation allowed** — Hash changes and query parameter updates on the same path are not blocked, only cross-path navigations trigger the guard.

3. **`exitSandbox()` before redirect on confirm** — When the user confirms leaving, we exit the sandbox first (clearing state) then navigate. This prevents the guard from re-triggering during the navigation.

4. **Hook placed in `SandboxSettingsPanel`** — Since this component is always rendered when the sandbox is active, it's the natural home for the navigation guard. The dialog renders as a fixed overlay above everything.

5. **Browser tab close/refresh handled by design** — The non-persisted Zustand store naturally discards state on tab close (Req 7.4). The `beforeunload` listener just shows the browser's native warning as a courtesy.

## How it connects

- Subscribes to `useSandboxStore.isActive` (task 4.1)
- Calls `exitSandbox()` when user confirms leaving (task 4.1)
- Rendered inside `SandboxSettingsPanel` (task 6.2)
- Uses `next-intl` for localized dialog text (existing i18n infrastructure)
- Satisfies Requirements 7.3 (warn on navigation) and 7.4 (discard on tab close)

## How to run / verify

1. Enter the simulation sandbox from a draft schedule
2. Make some changes (add a task, toggle a member, etc.)
3. Try to navigate to another page via the app's navigation — the confirmation dialog should appear
4. Click "Stay" — navigation is blocked, sandbox state preserved
5. Click "Leave" — sandbox exits and navigation proceeds
6. Try closing the browser tab — the browser's native "Leave site?" dialog should appear
7. When sandbox is inactive (not entered), no warnings should appear

## What comes next

- Task 8.1: Publish flow UI (uses `exitSandbox` on success, bypasses guard)
- Task 12.3: Unit tests for navigation guard behavior

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): implement navigation guards with beforeunload and in-app dialog"
```
