# 162 — Push Delivery Integration into NotificationService

## Phase

Push Notifications — Backend Infrastructure

## Purpose

Connects the existing in-app notification pipeline to the push notification delivery system. When `NotificationService.NotifySpaceAdminsAsync` persists in-app notifications, it now also dispatches push notifications to all subscribed devices for the notified users. Push delivery failures are isolated — they never affect in-app notification persistence.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Infrastructure/Notifications/NotificationService.cs` | Injected `IPushNotificationSender` and `ILogger<NotificationService>` via constructor. After `SaveChangesAsync`, calls `SendPushToUsersAsync` with a `PushPayload` built from the notification title, body, icon (`/favicon.jpeg`), and URL (`/notifications`). The push call is wrapped in try/catch so failures are logged but never propagate. |

## Key decisions

- **Icon**: Uses `/favicon.jpeg` as the push notification icon — consistent with the app's branding and the design document specification.
- **URL**: Points to `/notifications` as the click-through target. This is a safe default that works for all notification types.
- **Error isolation**: The try/catch around push delivery ensures Requirement 3.7 is satisfied — push failures never affect in-app notification persistence.
- **No fire-and-forget**: Push delivery is awaited (not `Task.Run`) because `PushNotificationSender` already handles all errors internally and never throws. The outer try/catch is defense-in-depth.
- **Logger added**: `ILogger<NotificationService>` was added to log push delivery failures with full context (spaceId, eventType).

## How it connects

- Depends on `IPushNotificationSender` (task 3.1) and its implementation `PushNotificationSender` (task 3.2)
- The DI registration (task 3.4) will wire `IPushNotificationSender` → `PushNotificationSender` in Program.cs
- Existing callers of `INotificationService.NotifySpaceAdminsAsync` (solver worker, schedule publisher) automatically gain push delivery without any changes

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
```

Build should succeed with 0 errors and 0 warnings.

## What comes next

- Task 3.4: Register `PushNotificationSender` in DI (Program.cs) so the constructor injection resolves at runtime
- Task 3.5: Property tests for push failure isolation and tenant isolation

## Git commit

```bash
git add -A && git commit -m "feat(push): integrate push delivery into NotificationService"
```
