# 160 — Push Notification Sender Implementation

## Phase

Push Notifications — Backend Infrastructure

## Purpose

Implements the `IPushNotificationSender` interface in the Infrastructure layer, providing the actual Web Push delivery logic. This component encrypts payloads per RFC 8291, signs requests with VAPID JWTs per RFC 8292, and handles push service error responses (410 Gone, 429 Rate Limit) gracefully without ever throwing exceptions.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Notifications/PushNotificationSender.cs` | Implementation of `IPushNotificationSender` using the WebPush NuGet library |

## Key decisions

- **WebPush NuGet library**: Uses the `WebPush` (v1.0.12) package which handles VAPID signing and RFC 8291 payload encryption internally via `WebPushClient.SendNotificationAsync`.
- **No IHttpClientFactory**: The WebPush library manages its own HTTP client internally. Using `WebPushClient` directly is the idiomatic approach for this library.
- **Batch query for multi-user delivery**: `SendPushToUsersAsync` queries all subscriptions for the target user IDs in a single DB call, then iterates to send.
- **Expired subscription cleanup**: Subscriptions returning 410 Gone are collected and deleted in a single `ExecuteDeleteAsync` batch at the end.
- **Never throws**: The outer try/catch ensures no exception escapes the sender. All errors are logged and swallowed per requirement 3.7.
- **VapidDetails from IOptions**: VAPID keys are loaded from `IOptions<VapidSettings>` which reads from environment variables.

## How it connects

- **Upstream**: `NotificationService` (task 3.3) will inject `IPushNotificationSender` and call it after persisting in-app notifications.
- **DI Registration**: Task 3.4 will register `IPushNotificationSender` → `PushNotificationSender` in Program.cs.
- **Domain**: Queries `PushSubscription` entities from `AppDbContext.PushSubscriptions`.
- **Configuration**: Depends on `VapidSettings` being registered in DI (done in task 1.3).

## How to run / verify

```bash
cd apps/api/Jobuler.Infrastructure
dotnet build --no-restore
```

Build should succeed with 0 errors and 0 warnings.

## What comes next

- Task 3.3: Integrate push delivery into `NotificationService`
- Task 3.4: Register `PushNotificationSender` in DI (Program.cs)
- Task 3.5: Property tests for push delivery behavior

## Git commit

```bash
git add -A && git commit -m "feat(push): implement PushNotificationSender with VAPID and RFC 8291 encryption"
```
