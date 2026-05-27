# Implementation Plan: Offline Cache Resilience

## Overview

This plan implements a stale-while-revalidate caching layer in the Shifter web app by enhancing the existing service worker (`public/sw.js`), adding a Zustand connectivity store, refactoring the `OfflineBanner` component, introducing a write guard interceptor, and wiring background refresh logic. All work is in the `apps/web` frontend — no backend changes required.

## Tasks

- [x] 1. Create connectivity store and types
  - [x] 1.1 Create `lib/store/connectivityStore.ts` with Zustand store
    - Define `ConnectivityStatus` type (`"online" | "offline" | "server-unavailable"`)
    - Implement `ConnectivityState` interface with `status`, `lastOnlineAt`, `isConnected` derived boolean
    - Implement actions: `goOffline`, `goOnline`, `setServerUnavailable`, `setServerRecovered`
    - Follow existing store patterns (`authStore.ts`, `spaceStore.ts`)
    - _Requirements: 2.1, 2.3, 3.1, 3.3, 3.5_

  - [ ]* 1.2 Write property test for connectivity state machine (Property 4)
    - **Property 4: Connectivity state machine correctness**
    - Verify that for any sequence of events, the store is always in exactly one valid state
    - Verify offline takes priority over server-unavailable when device is offline
    - **Validates: Requirements 2.1, 2.3, 3.1, 3.3, 3.5**

  - [ ]* 1.3 Write unit tests for connectivity store
    - Test specific transitions: online→offline, online→server-unavailable, server-unavailable→online
    - Test `isConnected` derived value is correct for each state
    - Test `lastOnlineAt` is updated on transitions to non-online states
    - _Requirements: 2.1, 2.3, 3.1, 3.3, 3.5_

- [x] 2. Enhance service worker with stale-while-revalidate and per-user caching
  - [x] 2.1 Extend `public/sw.js` with per-user cache and stale-while-revalidate strategy
    - Add `CACHED_API_PATTERNS` array for the 5 target endpoints (groups, members, tasks, schedule-versions, billing/subscription)
    - Implement `SET_CURRENT_USER` message handler to set active user ID
    - Implement `CLEAR_USER_CACHE` message handler to delete user's cache instance
    - Implement stale-while-revalidate: return cached response immediately, fetch in background
    - Add `X-Cache-Timestamp` and `X-Cache-Size` headers to cached responses
    - Post `CACHE_UPDATED` message to all clients when fresh data differs from cached
    - Return 503 with `{"error": "offline"}` body when no cache exists and network fails
    - Use per-user cache naming: `shifter-api-{userId}`
    - Preserve existing static-cache-first and network-first-for-schedule logic
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 4.1, 4.2, 4.3, 4.4_

  - [x] 2.2 Implement LRU eviction in service worker
    - Track total cache size per user using `X-Cache-Size` headers
    - When inserting a new entry would exceed 50MB, evict least-recently-used entries
    - Use `X-Cache-Timestamp` to determine LRU ordering
    - _Requirements: 8.4_

  - [ ]* 2.3 Write property test for stale-while-revalidate logic (Property 1)
    - **Property 1: Stale-while-revalidate serves cached response first**
    - Mock Cache API and fetch; verify cached response is always returned before network response
    - **Validates: Requirements 1.1, 4.1, 4.2, 8.3**

  - [ ]* 2.4 Write property test for cache miss with network failure (Property 3)
    - **Property 3: Cache miss with network failure returns empty-state indicator**
    - Verify 503 with `{"error": "offline"}` is returned when no cache and network fails
    - **Validates: Requirements 1.6, 4.3**

  - [ ]* 2.5 Write property test for per-user cache isolation (Property 5)
    - **Property 5: Per-user cache isolation**
    - Verify that cached data for user A is never accessible when current user is user B
    - **Validates: Requirements 1.5, 7.1, 7.3**

  - [ ]* 2.6 Write property test for cache cleared on logout (Property 6)
    - **Property 6: Cache cleared on logout**
    - Verify that after CLEAR_USER_CACHE message, the user's cache has zero entries
    - **Validates: Requirements 7.2**

  - [ ]* 2.7 Write property test for LRU eviction (Property 8)
    - **Property 8: LRU eviction enforces storage limit**
    - Verify total size stays ≤ 50MB after eviction and most-recently-used entries are preserved
    - **Validates: Requirements 8.4**

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Integrate connectivity store with API client and browser events
  - [x] 4.1 Wire connectivity store to browser online/offline events and API interceptor
    - Add response interceptor to `lib/api/client.ts` that calls `setServerUnavailable()` on 5xx/network errors (when device is online)
    - Add response interceptor that calls `setServerRecovered()` on successful API responses (when in server-unavailable state)
    - Add `online`/`offline` event listeners that call `goOffline()`/`goOnline()` on the connectivity store
    - Remove or deprecate the existing `apiOnline` module-level boolean and `api-error` custom event in favor of the store
    - _Requirements: 2.1, 2.3, 3.1, 3.3_

  - [x] 4.2 Create write guard interceptor at `lib/api/writeGuard.ts`
    - Implement axios request interceptor that rejects non-GET requests when `connectivityStore.isConnected` is false
    - Reject with a descriptive error (e.g., `{ code: "OFFLINE_WRITE_BLOCKED" }`)
    - Implement `useWriteGuard()` React hook returning `{ isDisabled: boolean; tooltipText: string }`
    - Register the interceptor in the API client setup
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [ ]* 4.3 Write property test for write guard (Property 7)
    - **Property 7: Write guard blocks mutations when disconnected**
    - Verify all non-GET methods are rejected when status is offline or server-unavailable
    - Verify GET requests pass through regardless of connectivity state
    - **Validates: Requirements 6.1, 6.2**

  - [ ]* 4.4 Write unit tests for write guard
    - Test POST/PUT/PATCH/DELETE are blocked when offline
    - Test GET requests pass through when offline
    - Test `useWriteGuard` hook returns correct `isDisabled` and `tooltipText`
    - Test re-enabling when connectivity restores
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 5. Refactor OfflineBanner and add cache lifecycle hook
  - [x] 5.1 Refactor `components/shell/OfflineBanner.tsx` to use connectivity store
    - Replace `useServiceWorker` dependency with `useConnectivityStore` subscription
    - Render amber warning banner for `offline` state with text "אתה לא מחובר לאינטרנט"
    - Render red error banner for `server-unavailable` state with text "השרת אינו זמין כרגע, נסה שוב מאוחר יותר"
    - Keep the existing update-available toast unchanged (still from `useServiceWorker`)
    - Dismiss banner within 2 seconds of state returning to `online`
    - _Requirements: 2.2, 2.4, 3.2, 3.4, 3.5_

  - [x] 5.2 Create `lib/hooks/useCacheLifecycle.ts` hook
    - On mount: send `SET_CURRENT_USER` message to SW with `authStore.userId`
    - On logout (subscribe to authStore changes): send `CLEAR_USER_CACHE` message to SW
    - Listen for `CACHE_UPDATED` messages from SW via `navigator.serviceWorker.onmessage`
    - On `CACHE_UPDATED`: invalidate relevant React Query keys using the URL from the message
    - _Requirements: 1.3, 5.2, 7.2_

  - [x] 5.3 Integrate `useCacheLifecycle` into `app/providers.tsx`
    - Call `useCacheLifecycle()` inside the Providers component
    - Ensure it runs after auth state is available
    - _Requirements: 1.3, 7.2_

  - [ ]* 5.4 Write unit tests for OfflineBanner component
    - Test renders amber banner when status is `offline`
    - Test renders red banner when status is `server-unavailable`
    - Test renders nothing when status is `online`
    - Test banner dismisses on state transition to online
    - _Requirements: 2.2, 2.4, 3.2, 3.4_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Implement background refresh on reconnection
  - [x] 7.1 Create `lib/cache/backgroundRefresh.ts`
    - Subscribe to connectivity store transitions (offline→online, server-unavailable→online)
    - Re-fetch all cached endpoint patterns for the current space (from `spaceStore`)
    - Execute fetches silently — do NOT set loading states in React Query
    - On failure: retain existing cache, schedule retry after 30 seconds, max 3 retries
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ]* 7.2 Write property test for cache update notification (Property 2)
    - **Property 2: Successful network fetch updates cache and notifies on difference**
    - Verify cache is updated on successful fetch and CACHE_UPDATED is posted when data differs
    - **Validates: Requirements 1.2, 1.3, 5.2**

  - [ ]* 7.3 Write unit tests for background refresh
    - Test refresh triggers on online transition
    - Test refresh does not show loading spinners
    - Test retry logic after failure (30s delay, max 3 retries)
    - Test refresh stops retrying after connectivity changes again
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [x] 8. Wire everything together and final integration
  - [x] 8.1 Wire background refresh initialization in `app/providers.tsx`
    - Initialize background refresh subscription alongside cache lifecycle
    - Pass current space ID from `spaceStore` to background refresh config
    - Ensure cleanup on unmount
    - _Requirements: 5.1, 8.2_

  - [x] 8.2 Apply `useWriteGuard` hook to mutation UI controls
    - Add disabled state and tooltip to key mutation buttons (create group, add member, submit schedule, etc.)
    - Use the `useWriteGuard` hook to conditionally disable and show tooltip
    - Ensure controls re-enable automatically when connectivity restores
    - _Requirements: 6.1, 6.3, 6.4_

  - [ ]* 8.3 Write integration tests for full offline flow
    - Test: SW returns cached response when offline
    - Test: Write guard blocks mutations when offline
    - Test: Background refresh fires on reconnection
    - Test: Logout clears user cache via SW message
    - Test: React Query invalidates on CACHE_UPDATED message
    - _Requirements: 1.1, 4.1, 5.1, 6.2, 7.2_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The project uses Vitest for testing and fast-check for property-based tests (both already installed)
- All new files go under `apps/web/` following existing directory conventions
- The existing `useServiceWorker` hook remains for SW registration and update-available toast; connectivity detection moves to the new store

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "2.5", "2.6"] },
    { "id": 3, "tasks": ["2.7", "4.1"] },
    { "id": 4, "tasks": ["4.2", "5.1", "5.2"] },
    { "id": 5, "tasks": ["4.3", "4.4", "5.3", "5.4"] },
    { "id": 6, "tasks": ["7.1"] },
    { "id": 7, "tasks": ["7.2", "7.3", "8.1"] },
    { "id": 8, "tasks": ["8.2", "8.3"] }
  ]
}
```
