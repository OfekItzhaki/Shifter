# Step 596 — Connectivity Store API Wiring

## Phase

Offline Cache Resilience — Integration

## Purpose

Wire the Zustand connectivity store to browser online/offline events and the axios API response interceptor, replacing the legacy module-level `apiOnline` boolean and `api-error` custom event with a centralized, reactive state management approach.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/api/client.ts` | Added response success interceptor calling `setServerRecovered()`; replaced `api-error` custom event dispatch with `setServerUnavailable()` call (only on 5xx/network errors when device is online); deprecated `apiOnline` and `isApiOnline()`; added `initConnectivity()` function for browser event listeners |
| `apps/web/app/providers.tsx` | Imported and called `initConnectivity()` in the root `useEffect` with proper cleanup |

## Key decisions

- **Only 5xx (500–599) and network errors (no response) trigger server-unavailable** — 401/403 are auth issues, not server issues
- **`navigator.onLine` check before `setServerUnavailable()`** — if the device is offline, the offline event listener handles that state; avoids conflicting state transitions
- **`setServerRecovered()` on every successful response** — the store internally no-ops if not in `server-unavailable` state, so this is safe and ensures fast recovery detection
- **Deprecated but kept `apiOnline`/`isApiOnline()`** — avoids breaking any remaining consumers during migration; marked with `@deprecated` JSDoc
- **`initConnectivity()` returns a cleanup function** — follows React effect cleanup pattern for proper listener removal

## How it connects

- Depends on: `connectivityStore.ts` (task 1.1) for state management
- Consumed by: `OfflineBanner` (task 5.1) for banner display, `writeGuard` (task 4.2) for mutation blocking, `backgroundRefresh` (task 7.1) for reconnection refresh
- The `ApiStatusBanner` component (unused, listens to the old `api-error` event) is now effectively dead code

## How to run / verify

1. Run the dev server and open the app
2. In Chrome DevTools → Network → toggle "Offline" — the connectivity store should transition to `offline`
3. Toggle back online — store should transition to `online`
4. Kill the API server while the app is running — next API call should trigger `server-unavailable`
5. Restart the API server — next successful API call should trigger `setServerRecovered()`

## What comes next

- Task 4.2: Write guard interceptor that blocks mutations when disconnected
- Task 5.1: Refactor OfflineBanner to consume the connectivity store

## Git commit

```bash
git add -A && git commit -m "feat(offline): wire connectivity store to browser events and API interceptor"
```
