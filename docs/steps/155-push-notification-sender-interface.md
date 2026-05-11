# 155 — IPushNotificationSender Interface

## Phase
Push Notifications — Backend Infrastructure

## Purpose
Define the application-layer abstraction for sending web push notifications. This interface decouples the notification delivery logic from the infrastructure implementation (VAPID signing, RFC 8291 encryption, HTTP delivery). Other application services (e.g., `NotificationService`) depend on this interface without knowing the delivery details.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Notifications/IPushNotificationSender.cs` | Interface with `SendPushToUserAsync` and `SendPushToUsersAsync` methods, plus the `PushPayload` record |

## Key decisions
- Placed in the existing `Jobuler.Application.Notifications` namespace alongside `INotificationService`
- `PushPayload` is a record with optional `Icon`, `Url`, and `Tag` fields (nullable) — keeps the contract simple while supporting all notification scenarios
- Both methods accept `CancellationToken` with a default value, matching the project's existing async patterns
- No return value (Task, not Task<T>) — push delivery is fire-and-forget from the caller's perspective; failures are handled internally by the implementation

## How it connects
- **Upstream**: `NotificationService` (Infrastructure) will inject this interface to dispatch push notifications after persisting in-app notifications
- **Downstream**: `PushNotificationSender` (Infrastructure, task 3.2) will implement this interface using WebPush NuGet package
- **Sibling**: Lives alongside `INotificationService` which handles in-app notification persistence

## How to run / verify
```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore --verbosity quiet
```
Build should succeed with exit code 0.

## What comes next
- Task 3.2: Implement `PushNotificationSender` in Infrastructure layer
- Task 3.3: Integrate push delivery into `NotificationService`
- Task 3.4: Register in DI container

## Git commit
```bash
git add -A && git commit -m "feat(push): define IPushNotificationSender interface in Application layer"
```
