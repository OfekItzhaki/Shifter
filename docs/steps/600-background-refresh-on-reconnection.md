# 600 — Background Refresh on Reconnection

## Phase

Offline Cache Resilience — Background Data Refresh

## Purpose

When a user regains connectivity (device comes back online or server recovers), the app needs to silently refresh all cached endpoints so the UI shows up-to-date data without requiring a manual page reload. This module subscribes to connectivity store transitions and triggers raw API calls that flow through the service worker, updating the cache and broadcasting `CACHE_UPDATED` messages to all tabs.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/cache/backgroundRefresh.ts` | Background refresh module that subscribes to connectivity store and silently re-fetches cached endpoints on reconnection |

## Key decisions

- **Raw `apiClient.get()` calls instead of React Query's `refetchQueries()`** — Using React Query would show loading spinners. Raw calls go through the axios pipeline → service worker fetch handler → cache update → `CACHE_UPDATED` postMessage, which triggers React Query invalidation via the `useCacheLifecycle` hook.
- **`Promise.allSettled` for parallel fetches** — All endpoints are fetched in parallel; partial failures trigger retry logic without blocking successful fetches.
- **Retry logic: 30s delay, max 3 attempts** — On any failure, waits 30 seconds and retries. After 3 total attempts, stops until the next connectivity transition.
- **Cancels pending retries on new connectivity transitions** — If the user goes offline again and back online, the old retry cycle is cancelled and a fresh refresh starts.
- **Only refreshes 3 key endpoints** — groups, schedule-versions, and billing/subscription. These are the space-level endpoints most critical for offline viewing.

## How it connects

- **Depends on:** `connectivityStore` (subscribes to status transitions), `spaceStore` (reads `currentSpaceId`), `apiClient` (makes GET requests)
- **Used by:** `app/providers.tsx` (task 8.1 will wire `initBackgroundRefresh()` into the provider tree)
- **Triggers:** Service worker cache updates via the normal fetch pipeline, which then posts `CACHE_UPDATED` messages handled by `useCacheLifecycle`

## How to run / verify

```bash
# TypeScript compilation check
cd apps/web && npx tsc --noEmit lib/cache/backgroundRefresh.ts
```

Manual verification:
1. Open the app, navigate to a space
2. Go offline (DevTools → Network → Offline)
3. Come back online
4. Observe network tab: 3 GET requests fire silently (groups, schedule-versions, billing/subscription)
5. No loading spinners appear in the UI

## What comes next

- Task 7.2: Property test for cache update notification (Property 2)
- Task 7.3: Unit tests for background refresh
- Task 8.1: Wire `initBackgroundRefresh()` into `app/providers.tsx`

## Git commit

```bash
git add -A && git commit -m "feat(offline-cache): background refresh on reconnection"
```
