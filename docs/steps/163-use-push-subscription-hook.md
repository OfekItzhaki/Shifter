# 163 — usePushSubscription Hook

## Phase

Push Notifications — Frontend Hook Layer

## Purpose

Provides a React hook that manages the full push subscription lifecycle on the frontend. It checks browser support, handles permission requests, creates/removes PushManager subscriptions, and syncs subscription state with the backend API. This is the core frontend abstraction that the Push Settings UI consumes.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/hooks/usePushSubscription.ts` | React hook exposing `isSupported`, `permission`, `isSubscribed`, `isLoading`, `subscribe()`, and `unsubscribe()`. Includes `urlBase64ToUint8Array` utility. |

## Key decisions

- **No new dependencies**: Uses native Push API and Service Worker API — no npm packages needed.
- **`urlBase64ToUint8Array` co-located**: The utility is exported from the same file since it's only used by this hook (and potentially tests). Keeps the module self-contained.
- **Graceful error handling**: All async operations wrapped in try/catch. Failures log to console but never throw to the caller, keeping the UI stable.
- **Backend-first status check**: On mount, the hook queries the backend for subscription status rather than relying solely on PushManager state. This ensures consistency if the user cleared browser data.
- **Unsubscribe order**: Backend DELETE is called before `pushSubscription.unsubscribe()` to ensure the server record is removed even if the local unsubscribe fails.

## How it connects

- **Consumed by**: `PushNotificationSettings` component (task 8.1) which renders the toggle UI.
- **Depends on**: `apiClient` from `@/lib/api/client` for authenticated API calls.
- **Backend endpoints**: `GET /spaces/{spaceId}/push-subscriptions/status`, `POST /spaces/{spaceId}/push-subscriptions`, `DELETE /spaces/{spaceId}/push-subscriptions`.
- **Environment**: Reads `NEXT_PUBLIC_VAPID_PUBLIC_KEY` for the VAPID application server key.
- **Service Worker**: Relies on the service worker being registered (handled by `useServiceWorker` hook at app root).

## How to run / verify

1. Ensure `NEXT_PUBLIC_VAPID_PUBLIC_KEY` is set in `.env.local`
2. The hook is a client-side React hook — verify via the Push Settings UI or by importing in a test component
3. TypeScript compilation: `npx tsc --noEmit` from `apps/web`

## What comes next

- Task 7.2: Unit tests for the hook (mock PushManager, test state transitions, test `urlBase64ToUint8Array`)
- Task 8.1: PushNotificationSettings component that consumes this hook

## Git commit

```bash
git add -A && git commit -m "feat(push): create usePushSubscription hook with subscription lifecycle management"
```
