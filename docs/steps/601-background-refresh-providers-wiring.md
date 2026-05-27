# 601 — Background Refresh Providers Wiring

## Phase

Offline Cache Resilience — Final Integration

## Purpose

Wire the background refresh subscription into the app's root `Providers` component so that cached endpoints are silently re-fetched whenever connectivity is restored. Without this wiring, the `initBackgroundRefresh` function exists but is never activated.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/providers.tsx` | Import `initBackgroundRefresh` from `@/lib/cache/backgroundRefresh`; call it inside the existing `useEffect` alongside `initConnectivity`; invoke its cleanup function on unmount |

## Key decisions

- **No parameter passing** — `initBackgroundRefresh` internally reads `spaceStore.currentSpaceId` when a connectivity transition occurs, so no prop drilling is needed.
- **Shared useEffect** — Both `initConnectivity` and `initBackgroundRefresh` are app-level subscriptions that should live for the entire component lifetime, so they share the same `useEffect([], [])` block.
- **Cleanup composition** — Both cleanup functions are called in the useEffect teardown to prevent memory leaks on HMR or unmount.

## How it connects

- Depends on `lib/cache/backgroundRefresh.ts` (task 7.1) which subscribes to the connectivity store.
- Depends on `lib/store/connectivityStore.ts` (task 1.1) for state transitions.
- Depends on `lib/store/spaceStore.ts` for the current space ID.
- Works alongside `useCacheLifecycle()` (task 5.3) which handles SW user messages and React Query invalidation.

## How to run / verify

1. Start the dev server: `pnpm dev`
2. Open the app and navigate to a space
3. Simulate going offline (DevTools → Network → Offline)
4. Go back online — background refresh should silently re-fetch cached endpoints (visible in Network tab)
5. No loading spinners should appear during the refresh

## What comes next

- Task 8.2: Apply `useWriteGuard` hook to mutation UI controls
- Task 8.3: Integration tests for the full offline flow

## Git commit

```bash
git add -A && git commit -m "feat(offline-cache): wire background refresh initialization in providers"
```
