# 598 — Offline Banner Connectivity Store Refactor

## Phase

Offline Cache Resilience — UI Layer

## Purpose

Refactors the `OfflineBanner` component to consume the new Zustand `connectivityStore` for connectivity state instead of relying on `useServiceWorker`'s `isOffline` boolean. This enables two distinct banner variants (device offline vs. server unavailable) with appropriate Hebrew text and visual styling, while preserving the existing update-available toast.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/shell/OfflineBanner.tsx` | Refactored to use `useConnectivityStore` for offline/server-unavailable banners; keeps `useServiceWorker` only for update-available toast |

## Key decisions

- **Two-banner approach**: amber for device offline ("אתה לא מחובר לאינטרנט"), red for server unavailable ("השרת אינו זמין כרגע, נסה שוב מאוחר יותר") — matches requirements 2.2 and 3.2.
- **2-second dismiss delay**: When connectivity returns to `online`, the banner stays visible for up to 2 seconds before dismissing, satisfying requirements 2.4 and 3.4.
- **Kept `useServiceWorker` for update toast**: The update-available toast still uses `useServiceWorker` since that logic is unrelated to connectivity state.
- **`role="alert"` on banners**: Ensures screen readers announce connectivity changes.

## How it connects

- Depends on `lib/store/connectivityStore.ts` (task 1.1) for connectivity state
- Depends on `lib/hooks/useServiceWorker.ts` for update-available toast (unchanged)
- Used by the app shell layout — rendered globally

## How to run / verify

1. Run the dev server and toggle browser offline mode in DevTools → Network
2. Verify amber banner appears with "אתה לא מחובר לאינטרנט"
3. Kill the API server while online → verify red banner with "השרת אינו זמין כרגע, נסה שוב מאוחר יותר"
4. Restore connectivity → verify banner dismisses within 2 seconds
5. Verify update-available toast still works when a new SW version is detected

## What comes next

- Task 5.2: `useCacheLifecycle` hook
- Task 5.4: Unit tests for OfflineBanner component

## Git commit

```bash
git add -A && git commit -m "feat(offline): refactor OfflineBanner to use connectivity store with dual banners"
```
