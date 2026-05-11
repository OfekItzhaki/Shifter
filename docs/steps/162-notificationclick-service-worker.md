# 162 — Notification Click Handler in Service Worker

## Phase

Push Notifications — Frontend Service Worker

## Purpose

When a user clicks a push notification, the app should respond by navigating to the relevant page. This handler closes the notification, then either focuses an existing app window and navigates it to the target URL, or opens a new window if none exists.

## What was built

| File | Change |
|------|--------|
| `apps/web/public/sw.js` | Added `notificationclick` event listener |

## Key decisions

- **Existing window detection**: Uses `self.clients.matchAll({ type: "window", includeUncontrolled: true })` and checks if the client URL includes `self.location.origin` to find an existing app window.
- **Fallback URL**: Defaults to `"/"` if `event.notification.data.url` is not present.
- **`event.waitUntil()`**: Keeps the service worker alive while the async client matching and navigation completes.

## How it connects

- The `push` event listener (task 6.1) sets `data: { url }` in the notification options — this handler reads that URL on click.
- The URL is set by the backend `PushPayload.Url` field, which points to the relevant page (e.g., `/schedule/my-missions`).

## How to run / verify

1. Subscribe to push notifications in the app.
2. Trigger a notification (e.g., publish a schedule).
3. Click the notification — the app should focus and navigate to the target URL.
4. If no app window is open, a new window should open at the target URL.

## What comes next

- `usePushSubscription` hook (task 7.1) — frontend subscription lifecycle management.
- Push Settings UI (task 8.1) — toggle component for enabling/disabling push.

## Git commit

```bash
git add -A && git commit -m "feat(push): add notificationclick handler to service worker"
```
