# Step 321 — Admin Session Activity Wiring & Multi-Tab Sync

## Phase

Admin Session Timeout — Frontend Integration

## Purpose

Connects the InactivityTimer and MultiTabSync modules to the adminSessionStore, completing the activity detection and cross-tab synchronization wiring. This ensures that user activity (clicks, keypresses, scrolls, API calls) resets the inactivity timer, and that session state changes propagate across all open browser tabs.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/hooks/useAdminSessionWiring.ts` | New hook that orchestrates InactivityTimer lifecycle, API call interceptor for activity detection, MultiTabSync channel management, and cross-tab event broadcasting |
| `apps/web/components/admin/AdminSessionGuard.tsx` | Updated to call `useAdminSessionWiring()`, making it the global integration point for timer and sync |
| `apps/web/__tests__/session/adminSessionWiring.test.ts` | Unit tests covering timer start/stop, multi-tab message handling, API interceptor, and session_exit broadcasting |

## Key decisions

- **Hook-based wiring in AdminSessionGuard**: Since AdminSessionGuard is already rendered globally in `providers.tsx`, it's the natural place to wire the timer and sync without adding another provider component.
- **API interceptor for activity reset**: Rather than wrapping every API call, a single Axios request interceptor resets the timer on any outgoing request while in elevated mode. This covers all API activity without modifying individual API functions.
- **Cleanup-based session_exit broadcast**: The sync effect's cleanup function broadcasts `session_exit` before destroying the channel, reading `lastExitContext` from the store to get the mode/groupId after state is cleared.
- **Effect ordering**: The timer, interceptor, and sync effects are independent and keyed on `isElevated`, ensuring proper lifecycle management.

## How it connects

- **InactivityTimer** (task 6.2) already registers click/keypress/scroll DOM listeners internally. This hook starts and stops it based on elevated mode state.
- **MultiTabSync** (task 6.3) is instantiated and subscribed to in this hook, with handlers that call store actions (`resetTimer`, `exitElevatedMode`).
- **adminSessionStore** (task 6.1) provides the state and actions that drive the timer and sync behavior.
- **AdminSessionGuard** (task 9.3) already renders the ActivityPromptModal; now it also runs the wiring hook.
- **Task 9.4** (timeout exit behavior) relies on this wiring to trigger the timeout flow when the timer fires `onTimeout`.

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/session/adminSessionWiring.test.ts
```

All 11 tests should pass, covering:
- Timer starts/stops with elevated mode
- MultiTabSync creation and destruction
- session_exit broadcast on exit
- activity_reset, session_exit, prompt_dismissed message handling
- API interceptor registration/removal
- showPrompt called on timer timeout
- prompt_shown broadcast on timeout

## What comes next

- Task 10.1: Add timeout duration setting to group settings form
- Task 10.2: Add platform timeout setting to platform settings page

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): wire activity listeners and multi-tab sync to session store"
```
