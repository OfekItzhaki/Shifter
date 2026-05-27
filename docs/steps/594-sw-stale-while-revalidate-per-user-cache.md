# 594 — Service Worker Stale-While-Revalidate Per-User Cache

## Phase

Offline Cache Resilience

## Purpose

Extends the existing service worker (`public/sw.js`) with a stale-while-revalidate caching strategy for key API endpoints, partitioned by user ID. This enables users to see their data instantly from cache while fresh data loads in the background, and to view cached data when offline or when the server is unavailable.

## What was built

| File | Description |
|------|-------------|
| `apps/web/public/sw.js` | Enhanced with per-user cache, stale-while-revalidate strategy, message handlers, and cache metadata headers |

### Changes to `sw.js`:

1. **`CACHED_API_PATTERNS` array** — 5 regex patterns matching the target endpoints (groups, members, tasks, schedule-versions, billing/subscription)
2. **`currentUserId` state** — Module-level variable tracking the active user for cache partitioning
3. **`SET_CURRENT_USER` message handler** — Sets the active user ID when the app sends a message
4. **`CLEAR_USER_CACHE` message handler** — Deletes the user's entire cache instance on logout
5. **`staleWhileRevalidate()` function** — Returns cached response immediately, fetches in background
6. **`revalidateInBackground()` function** — Compares fresh vs cached response bodies, posts `CACHE_UPDATED` to all clients when data differs
7. **`putWithMetadata()` function** — Stores responses with `X-Cache-Timestamp` and `X-Cache-Size` headers
8. **`getUserCacheName()` helper** — Returns `shifter-api-{userId}` cache name
9. **503 offline response** — Returns `{"error": "offline"}` when no cache exists and network fails
10. **Preserved existing logic** — Static cache-first, network-first for schedule, HTML offline page, push notifications

## Key decisions

- **Per-user cache via separate Cache instances** (`shifter-api-{userId}`) rather than key prefixing — makes logout cleanup a single `caches.delete()` call
- **Activate event preserves per-user caches** — Only cleans up old versioned caches, not `shifter-api-*` caches
- **Background revalidation is fire-and-forget** — Network failures during revalidation are silently ignored since the cached response was already served
- **Response body text comparison** for change detection — Simple and reliable for JSON API responses
- **Cache metadata stored as response headers** — `X-Cache-Timestamp` and `X-Cache-Size` enable future LRU eviction without extra storage

## How it connects

- **Upstream:** The app's `useCacheLifecycle` hook (task 5.2) will send `SET_CURRENT_USER` and `CLEAR_USER_CACHE` messages
- **Downstream:** The `CACHE_UPDATED` postMessage will be consumed by the cache lifecycle hook to invalidate React Query keys
- **LRU eviction** (task 2.2) will use the `X-Cache-Timestamp` and `X-Cache-Size` headers stored by `putWithMetadata`
- **Connectivity store** (task 1.1) and **background refresh** (task 7.1) will trigger re-fetches that flow through this SW logic

## How to run / verify

1. Build the web app: `cd apps/web && npm run build`
2. Serve the built app and open in Chrome
3. Open DevTools → Application → Service Workers — verify the SW is active
4. Navigate to a page that fetches one of the cached endpoints (e.g., groups list)
5. Open DevTools → Application → Cache Storage — verify a `shifter-api-{userId}` cache exists with entries
6. Toggle DevTools → Network → Offline — verify cached data still renders
7. Go back online — verify background revalidation updates the cache

## What comes next

- Task 2.2: LRU eviction logic using `X-Cache-Timestamp` and `X-Cache-Size` headers
- Task 2.3–2.6: Property-based tests for the SW caching logic
- Task 5.2: `useCacheLifecycle` hook that sends messages to the SW

## Git commit

```bash
git add -A && git commit -m "feat(offline-cache): stale-while-revalidate per-user cache in service worker"
```
