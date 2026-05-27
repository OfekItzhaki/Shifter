# 600 — Cache Lifecycle Providers Integration

## Phase

Offline Cache Resilience — Task 5.3

## Purpose

Integrates the `useCacheLifecycle` hook into the app's root `Providers` component so that the service worker is informed of the current user on mount, cache is cleared on logout, and React Query keys are invalidated when the SW reports fresh data.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/providers.tsx` | Added import and call to `useCacheLifecycle()` inside the `Providers` component |

## Key decisions

- The hook is called unconditionally (not wrapped in a condition) because it internally subscribes to `authStore.userId` and handles the null case gracefully.
- Placed after the `useAuthStore` selector so auth state is already available in the render cycle.
- No additional props or configuration needed — the hook is self-contained.

## How it connects

- **Depends on:** `useCacheLifecycle` hook (step 599), `authStore`, service worker cache layer (step 594)
- **Enables:** Per-user cache isolation at runtime, automatic cache invalidation on fresh data, cache cleanup on logout
- **Requirements:** 1.3 (UI updates with fresh data), 7.2 (cache cleared on logout)

## How to run / verify

1. Run the dev server: `pnpm --filter web dev`
2. Log in — verify no console errors related to the cache lifecycle hook
3. Open DevTools → Application → Service Workers — confirm `SET_CURRENT_USER` message is sent
4. Log out — confirm `CLEAR_USER_CACHE` message is sent to the SW

## What comes next

- Task 5.4: Unit tests for OfflineBanner component
- Task 7.1: Background refresh on reconnection
- Task 8.1: Wire background refresh initialization in providers

## Git commit

```bash
git add -A && git commit -m "feat(offline-cache): integrate useCacheLifecycle into Providers component"
```
