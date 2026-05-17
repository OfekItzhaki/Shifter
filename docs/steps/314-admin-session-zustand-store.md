# 314 — Admin Session Zustand Store

## Phase

Admin Session Timeout — Frontend Session Module

## Purpose

Creates the `adminSessionStore` Zustand store that manages elevated privilege mode state (Management Mode and Super Platform Mode) on the frontend. This store tracks whether the user is in an elevated session, the inactivity timer state, and the activity prompt visibility. It resets on page load to ensure no stale elevated state persists.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/store/adminSessionStore.ts` | Zustand store with state (`isElevated`, `elevatedMode`, `elevatedGroupId`, `timeoutDuration`, `remainingMs`, `isPromptVisible`, `promptCountdownMs`) and actions (`enterElevatedMode`, `exitElevatedMode`, `resetTimer`, `showPrompt`, `dismissPrompt`) |

## Key decisions

- **No persistence**: The store does not use `zustand/middleware/persist`. Elevated mode resets on page load, matching the security requirement that sessions are ephemeral (Req 7.1).
- **Timeout captured at entry**: `timeoutDuration` is set when entering elevated mode and remains fixed for the session duration, matching Req 3.7 (active sessions use the timeout in effect when they started).
- **Default 15 minutes**: Matches the backend default for both group and platform timeouts.
- **Prompt countdown**: Fixed at 60 seconds (Req 6.2).
- **Exit reason parameter**: `exitElevatedMode` accepts a reason (`manual`, `timeout`, `prompt_no`, `sync`) for downstream logic (redirects, toast, audit event) to be wired in task 9.4.

## How it connects

- **InactivityTimer module** (task 6.2) will call `resetTimer()` on activity events and `showPrompt()` when the timer expires.
- **MultiTabSync module** (task 6.3) will call `exitElevatedMode('sync')` and `resetTimer()` based on cross-tab messages.
- **ActivityPromptModal** (task 8.1) will read `isPromptVisible` and `promptCountdownMs`, and call `dismissPrompt()`.
- **Integration wiring** (task 9.x) will call `enterElevatedMode()` after successful re-authentication and handle redirects/toasts on exit.

## How to run / verify

```bash
# TypeScript compilation check
cd apps/web && npx tsc --noEmit --strict lib/store/adminSessionStore.ts
```

The store has no runtime side effects — it will be exercised by property tests in tasks 6.4–6.7 and integration wiring in task 9.

## What comes next

- Task 6.2: `InactivityTimer` module that drives `remainingMs` countdown and calls `showPrompt()`
- Task 6.3: `MultiTabSync` module for cross-tab coordination
- Task 6.4–6.7: Property tests validating timer and state cleanup properties

## Git commit

```bash
git add -A && git commit -m "feat(admin-session): create adminSessionStore Zustand store"
```
