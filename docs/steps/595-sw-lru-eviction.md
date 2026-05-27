# Step 595 — Service Worker LRU Eviction

## Phase

Offline Cache Resilience

## Purpose

Enforces a 50MB per-user cache storage limit in the service worker by evicting least-recently-used entries when the limit is exceeded. This prevents unbounded cache growth and ensures the caching layer does not degrade device storage.

## What was built

| File | Description |
|------|-------------|
| `apps/web/public/sw.js` | Added `evictIfNeeded` function and `MAX_CACHE_SIZE_BYTES` constant. Modified `putWithMetadata` to call `evictIfNeeded` after every cache write. |

### `evictIfNeeded(cache)` function

- Iterates all cache entries using `cache.keys()`
- Reads `X-Cache-Size` and `X-Cache-Timestamp` headers from each cached response
- Sums total cache size; if within 50MB limit, returns early
- Sorts entries by timestamp ascending (oldest = least recently used)
- Evicts oldest entries one by one until total size is ≤ 50MB

## Key decisions

- **LRU via `X-Cache-Timestamp`**: The `putWithMetadata` function already stamps each entry with the current time on write. This timestamp serves as the "last used" marker since stale-while-revalidate updates the timestamp on every successful revalidation.
- **Eviction after every write**: Calling `evictIfNeeded` after `putWithMetadata` ensures the limit is never exceeded for more than one write cycle.
- **Simple iteration over `cache.keys()`**: For the expected number of cached endpoints (5 patterns × a few spaces), iterating all entries is efficient. No secondary index is needed.

## How it connects

- Depends on `putWithMetadata` (task 2.1) which sets `X-Cache-Timestamp` and `X-Cache-Size` headers
- Called after every cache write in both `staleWhileRevalidate` (cache miss path) and `revalidateInBackground` (background refresh path)
- Property test (task 2.7) will validate that total size stays ≤ 50MB and MRU entries are preserved

## How to run / verify

1. Open the app in Chrome, log in, and navigate to trigger cached endpoint requests
2. Open DevTools → Application → Cache Storage → `shifter-api-{userId}`
3. Observe entries have `X-Cache-Timestamp` and `X-Cache-Size` headers
4. To test eviction: temporarily lower `MAX_CACHE_SIZE_BYTES` to a small value (e.g., 1024) and reload — oldest entries should be evicted

## What comes next

- Task 2.7: Property test for LRU eviction (validates total size ≤ 50MB and MRU preservation)
- Task 4.1: Wire connectivity store to browser events and API interceptor

## Git commit

```bash
git add -A && git commit -m "feat(offline-cache): LRU eviction in service worker (50MB per-user limit)"
```
