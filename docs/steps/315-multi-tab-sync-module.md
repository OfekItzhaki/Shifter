# Step 315 — Multi-Tab Sync Module

## Phase

Admin Session Timeout — Frontend Session Infrastructure

## Purpose

Provides cross-tab synchronization for admin session state so that activity resets, session exits, and prompt events propagate consistently across all open browser tabs. This prevents scenarios where a user is active in one tab but gets timed out in another.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/session/multiTabSync.ts` | `MultiTabSync` class with `broadcast()`, `subscribe()`, `unsubscribe()`, and `destroy()` methods. Uses `BroadcastChannel` as primary transport with `localStorage` + `storage` event fallback. |
| `apps/web/lib/session/multiTabSync.test.ts` | 14 unit tests covering BroadcastChannel path, localStorage fallback, subscribe/unsubscribe, error isolation, and destroy cleanup. |

## Key decisions

- **BroadcastChannel primary, localStorage fallback**: BroadcastChannel is the modern, efficient API for same-origin tab communication. The localStorage fallback ensures compatibility with older browsers or restricted environments (private browsing).
- **Fire-and-forget localStorage pattern**: The fallback writes to localStorage and immediately removes the key — only the `storage` event trigger matters, not persistent storage.
- **Error isolation between handlers**: One handler throwing does not break delivery to other subscribers.
- **Graceful degradation**: If both BroadcastChannel and localStorage are unavailable, each tab operates independently without crashing.
- **Factory function exported**: `createMultiTabSync()` provides a clean instantiation API alongside the class export for testing.

## How it connects

- **Consumed by**: `adminSessionStore` (task 9.5) will use this module to broadcast and receive activity resets, session exits, and prompt events across tabs.
- **Depends on**: No internal dependencies — standalone utility module.
- **Requirements**: 11.1 (activity sync across tabs), 11.2 (session exit propagation), 11.3 (prompt deduplication across tabs).

## How to run / verify

```bash
cd apps/web
npx vitest run lib/session/multiTabSync.test.ts
```

All 14 tests should pass.

## What comes next

- Task 6.4–6.7: Property-based tests for timer and multi-tab sync behavior
- Task 9.5: Wire MultiTabSync into the adminSessionStore for cross-tab event propagation

## Git commit

```bash
git add -A && git commit -m "feat(admin-session): multi-tab sync module with BroadcastChannel and localStorage fallback"
```
