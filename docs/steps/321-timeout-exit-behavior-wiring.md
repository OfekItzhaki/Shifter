# 321 — Timeout Exit Behavior Wiring

## Phase
Admin Session Timeout — Frontend Integration

## Purpose
Wires the timeout exit behavior so that when an elevated mode session is terminated due to timeout or user declining the activity prompt, the system performs all required side effects: clearing state, redirecting, showing a toast notification, and logging the event to the backend.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/store/adminSessionStore.ts` | Added `ExitContext` interface and `lastExitContext` state field. `exitElevatedMode` and `dismissPrompt("no")` now capture mode/groupId/reason before clearing state. Added `clearExitContext` action. |
| `apps/web/lib/hooks/useSessionExitHandler.ts` | New hook that watches `lastExitContext` and performs side effects on timeout/prompt_no exits: clears authStore admin mode, redirects to group page (management) or home (platform), shows toast, sends POST /auth/session-timeout-event. |
| `apps/web/components/admin/SessionTimeoutToast.tsx` | New toast notification component — floating bottom-right, auto-dismisses after 6 seconds, accessible with `role="alert"` and `aria-live="assertive"`. |
| `apps/web/components/admin/AdminSessionGuard.tsx` | Integrated `useSessionExitHandler` hook and renders `SessionTimeoutToast`. |
| `apps/web/lib/hooks/useAdminSessionWiring.ts` | Fixed session_exit broadcast to use `lastExitContext` ref in the cleanup function, ensuring the broadcast happens before the sync channel is destroyed. |

## Key decisions
- **Exit context pattern**: Rather than coupling the store to routing/toast/API concerns, the store captures exit context (mode, groupId, reason, timestamp) and a separate hook handles side effects. This keeps the store pure and testable.
- **Custom toast**: No toast library was added — a lightweight custom component follows the existing OfflineBanner pattern (floating bottom-right, Tailwind styling).
- **Fire-and-forget API call**: The POST to `/auth/session-timeout-event` is best-effort — network failures don't block the user experience.
- **Broadcast in cleanup**: The session_exit broadcast to other tabs happens in the useEffect cleanup function (before sync.destroy()), solving the timing issue where the sync channel was destroyed before the broadcast could fire.

## How it connects
- Depends on: `adminSessionStore` (task 6.1), `ActivityPromptModal` (task 8.1), `AdminSessionGuard` (task 9.3), backend endpoint `POST /auth/session-timeout-event` (task 4.2)
- Used by: The global `AdminSessionGuard` in `providers.tsx` renders the toast and handles exit side effects automatically.
- Next: Task 9.5 (activity event listeners and multi-tab sync wiring)

## How to run / verify
```bash
cd apps/web
npx vitest --run __tests__/session/
```
All 42 tests pass (3 test files: activityPromptModal, adminSessionWiring, inactivityTimer).

## What comes next
- Task 9.5: Wire activity event listeners and multi-tab sync
- Task 10.1: Add timeout duration setting to group settings form
- Task 10.2: Add platform timeout setting to platform settings page

## Git commit
```bash
git add -A && git commit -m "feat(admin-session): wire timeout exit behavior with redirect, toast, and audit event"
```
