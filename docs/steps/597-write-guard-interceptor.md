# 597 — Write Guard Interceptor

## Phase

Offline Cache Resilience — Write Operation Guard

## Purpose

Prevents mutation API requests (POST, PUT, PATCH, DELETE) from being sent when the app is disconnected (offline or server-unavailable). Also provides a React hook for UI controls to disable mutation buttons and show a Hebrew tooltip explaining why the action is unavailable.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/writeGuard.ts` | Axios request interceptor (`writeGuardInterceptor`) that blocks non-GET requests when disconnected, plus `useWriteGuard()` React hook for UI controls |
| `apps/web/lib/api/client.ts` | Registered the write guard interceptor in the API client request pipeline |

## Key decisions

- **Interceptor throws synchronously** — Axios request interceptors that throw an error prevent the request from being sent. This is simpler than returning `Promise.reject()` and achieves the same result.
- **Error code `OFFLINE_WRITE_BLOCKED`** — Attached to the thrown error so callers can distinguish write-guard rejections from network errors.
- **GET requests always pass through** — The interceptor explicitly checks the method and returns early for GET, ensuring read operations are never blocked regardless of connectivity state.
- **Zustand hook pattern for `useWriteGuard`** — Uses the selector pattern (`useConnectivityStore((state) => state.status)`) for reactive subscriptions, consistent with the project's existing Zustand usage.
- **Hebrew tooltip text** — "לא ניתן לבצע פעולה זו ללא חיבור לשרת" as specified in requirements.

## How it connects

- Depends on `connectivityStore` (created in step 593) for connectivity state
- Registered in `client.ts` (the central API client) so all mutation requests go through the guard
- The `useWriteGuard()` hook will be consumed by UI components (task 8.2) to disable buttons
- Property tests (task 4.3) and unit tests (task 4.4) will validate this interceptor

## How to run / verify

```bash
# Type-check
cd apps/web && npx tsc --noEmit

# The interceptor is automatically active — any non-GET request while
# connectivityStore.status !== "online" will be rejected with OFFLINE_WRITE_BLOCKED
```

## What comes next

- Task 4.3: Property test for write guard (Property 7)
- Task 4.4: Unit tests for write guard
- Task 8.2: Apply `useWriteGuard` hook to mutation UI controls

## Git commit

```bash
git add -A && git commit -m "feat(offline): write guard interceptor blocks mutations when disconnected"
```
