# 162 — Service Worker Push Event Handler

## Phase
Push Notifications — Frontend Service Worker

## Purpose
Add a `push` event listener to the existing service worker so that incoming Web Push messages from the backend are displayed as native browser notifications. This is the client-side counterpart to the `PushNotificationSender` on the backend.

## What was built

| File | Change |
|------|--------|
| `apps/web/public/sw.js` | Added `self.addEventListener("push", ...)` handler that parses JSON payload and calls `showNotification` |

## Key decisions

- **Graceful error handling**: The handler guards against `event.data` being null and wraps `event.data.json()` in a try/catch so malformed payloads never throw.
- **Title required**: If the parsed payload has no `title`, the handler returns early since `showNotification` requires a title.
- **Default icon**: Falls back to `/favicon.jpeg` when no icon is provided in the payload.
- **`data: { url }`**: The URL is stored in the notification's data field so the existing `notificationclick` handler can navigate to it.
- **`event.waitUntil`**: Keeps the service worker alive until `showNotification` resolves.

## How it connects

- The backend `PushNotificationSender` encrypts and sends a JSON payload (title, body, icon, url, tag, timestamp) to the push service.
- The push service delivers it to this service worker's `push` event.
- The `notificationclick` handler (task 6.2) reads `data.url` to navigate on click.

## How to run / verify

1. Register a push subscription via the frontend settings UI.
2. Trigger a notification from the backend (e.g., publish a schedule).
3. Observe a native browser notification with the correct title, body, and icon.
4. Alternatively, use Chrome DevTools → Application → Service Workers → Push to send a test payload:
   ```json
   {"title":"Test","body":"Hello from push","url":"/schedule"}
   ```

## What comes next

- Task 6.2: `notificationclick` handler (already present in the file).
- Task 7.1: `usePushSubscription` hook for managing subscriptions from the frontend.

## Git commit

```bash
git add -A && git commit -m "feat(push): add push event listener to service worker"
```
