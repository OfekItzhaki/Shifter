# Step 599 — Cache Lifecycle Hook

## Phase

Offline Cache Resilience — Task 5.2

## Purpose

Bridges the React app with the service worker's per-user cache layer. Ensures the SW knows which user is active (for cache partitioning), clears the user's cache on logout, and invalidates React Query keys when the SW detects fresh data from background revalidation.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/hooks/useCacheLifecycle.ts` | React hook that manages SW ↔ app cache communication |

## Key decisions

- **Zustand subscription via `useAuthStore` selector** — watches `userId` changes to detect login/logout transitions without extra event wiring.
- **`useRef` for previous userId** — tracks the previous user ID to detect logout (userId goes from non-null to null) and send `CLEAR_USER_CACHE` with the correct userId.
- **URL-to-query-key mapping** — parses the `CACHE_UPDATED` message URL with regex to determine which React Query keys to invalidate, matching the patterns in `lib/query/keys.ts`.
- **Graceful SSR/unsupported-browser guard** — checks for `navigator.serviceWorker` availability before any SW communication, making the hook safe to call in any environment.
- **Two separate `useEffect` hooks** — one for userId-dependent SW messages (SET_CURRENT_USER, CLEAR_USER_CACHE), one for the message listener (stable, no deps).

## How it connects

- Consumes `useAuthStore` (userId) from `lib/store/authStore.ts`
- Imports `queryClient` from `lib/query/queryClient.ts` for invalidation
- Communicates with `public/sw.js` via `postMessage` (SET_CURRENT_USER, CLEAR_USER_CACHE)
- Listens for `CACHE_UPDATED` messages posted by the SW's `revalidateInBackground` function
- Will be integrated into `app/providers.tsx` in task 5.3

## How to run / verify

1. Import and call `useCacheLifecycle()` in a component (or wait for task 5.3 integration)
2. Log in → verify SW receives `SET_CURRENT_USER` message (check SW console)
3. Log out → verify SW receives `CLEAR_USER_CACHE` message
4. Trigger a background revalidation (navigate to a cached endpoint after data changes) → verify React Query invalidates the correct key

## What comes next

- Task 5.3: Integrate `useCacheLifecycle` into `app/providers.tsx`
- Task 7.1: Background refresh on reconnection (complements this hook's invalidation)

## Git commit

```bash
git add -A && git commit -m "feat(offline-cache): add useCacheLifecycle hook for SW-app cache coordination"
```
