# 593 — Connectivity Store

## Phase

Offline Cache Resilience — Task 1.1

## Purpose

Provides a Zustand store that tracks the app's connectivity state (online, offline, or server-unavailable). This is the foundational piece for the offline cache resilience feature — all other components (status banner, write guard, background refresh) subscribe to this store to determine behavior.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/store/connectivityStore.ts` | Zustand store with `ConnectivityStatus` type, `ConnectivityState` interface, derived `isConnected` getter, and four actions (`goOffline`, `goOnline`, `setServerUnavailable`, `setServerRecovered`) |

## Key decisions

- **No persist middleware** — connectivity is ephemeral runtime state; it should always start as "online" on page load and be determined by actual network conditions.
- **Offline takes priority** — `setServerUnavailable()` is a no-op when already offline, because device-level disconnection is a stronger signal than server errors.
- **`setServerRecovered()` only transitions from `server-unavailable`** — prevents accidentally overriding an offline state when a cached response happens to succeed.
- **`lastOnlineAt` records the moment of disconnection** — set when transitioning away from online (to offline or server-unavailable), useful for UI display and background refresh scheduling.
- **Derived `isConnected` getter** — uses Zustand's `get()` pattern (same as `authStore.isAdminMode`) so consumers get a simple boolean.

## How it connects

- **Status Banner** (task 5.1) subscribes to `status` to show the correct Hebrew-language banner.
- **Write Guard** (task 4.2) reads `isConnected` to block mutation requests.
- **API interceptor** (task 4.1) calls `setServerUnavailable()` / `setServerRecovered()` / `goOffline()` / `goOnline()`.
- **Background Refresh** (task 7.1) subscribes to transitions back to online to trigger re-fetches.

## How to run / verify

```bash
# The store is pure logic — verify via the property tests (task 1.2) and unit tests (task 1.3)
cd apps/web
npx vitest run --reporter=verbose lib/store/connectivityStore
```

## What comes next

- Task 1.2: Property test for the connectivity state machine
- Task 1.3: Unit tests for specific transitions
- Task 4.1: Wire the store to browser events and API interceptor

## Git commit

```bash
git add -A && git commit -m "feat(offline-cache): connectivity store with state machine"
```
