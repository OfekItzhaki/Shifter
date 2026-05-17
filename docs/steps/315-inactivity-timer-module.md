# 315 — InactivityTimer Module

## Phase

Phase: Admin Session Timeout — Frontend Session Infrastructure

## Purpose

Implements the client-side inactivity countdown timer that tracks elapsed time since the last meaningful user interaction within elevated mode (Management Mode or Super Platform Mode). When the timer reaches zero, it triggers the activity prompt flow. This module handles browser tab visibility changes by reconciling actual elapsed time when the tab regains focus.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/session/inactivityTimer.ts` | `InactivityTimer` class with `start()`, `reset()`, `stop()`, `reconcileAfterVisibilityChange()` methods |
| `apps/web/__tests__/session/inactivityTimer.test.ts` | 19 unit tests covering all timer behaviors |

## Key decisions

- **Class-based design** — The timer maintains internal state (interval ID, timestamps, callbacks) making a class the natural fit over a plain function module.
- **Elapsed-time calculation on every tick** — Instead of decrementing a counter, each tick recalculates remaining time from `Date.now() - lastActivityTimestamp`. This prevents drift from throttled intervals in background tabs.
- **Passive event listeners** — Activity listeners (click, keypress, scroll) use `{ passive: true }` to avoid blocking the main thread.
- **Fail-safe on visibility change** — If elapsed time exceeds the timeout while the tab was hidden, timeout triggers immediately on visibility restore rather than silently extending the session.
- **No `onPrompt` callback** — The design doc's `onTimeout` callback serves as the prompt trigger; the store layer decides whether to show the prompt or exit directly.

## How it connects

- **Consumed by**: `adminSessionStore` (task 9.5) will instantiate and wire the timer to store actions
- **Depends on**: Nothing — standalone module with no imports from the project
- **Callbacks map to store**: `onTick` → updates `remainingMs` in store, `onTimeout` → calls `showPrompt()`
- **Multi-tab sync** (task 6.3) will call `reset()` when activity is broadcast from another tab

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/session/inactivityTimer.test.ts
```

All 19 tests should pass.

## What comes next

- Task 6.3: `MultiTabSync` module (BroadcastChannel + localStorage fallback)
- Task 6.4: Property test for timer initialization and reset
- Task 9.5: Wiring activity listeners and multi-tab sync to the store

## Git commit

```bash
git add -A && git commit -m "feat(admin-session): add InactivityTimer module with unit tests"
```
