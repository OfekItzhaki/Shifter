# 162 — Push Notification Sender DI Registration

## Phase
Push Notifications — Backend Infrastructure

## Purpose
Register the `PushNotificationSender` implementation in the dependency injection container so that the `IPushNotificationSender` interface can be resolved throughout the application. Also register a named HttpClient for push service requests via `IHttpClientFactory`.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Program.cs` | Added `builder.Services.AddScoped<IPushNotificationSender, PushNotificationSender>()` alongside other application services |
| `apps/api/Jobuler.Api/Program.cs` | Added named HttpClient `"WebPush"` registration with 30s timeout for push service requests |

## Key decisions
- Registered as **Scoped** (same as `INotificationService`) since it depends on `AppDbContext` which is also scoped.
- Named HttpClient `"WebPush"` registered for future use when `PushNotificationSender` migrates from `new WebPushClient()` to `IHttpClientFactory`. The 30-second timeout is appropriate for push service round-trips.
- Placed the service registration next to `INotificationService` since they're closely related.
- Placed the HttpClient registration after the VAPID configuration section since they're both Web Push infrastructure.

## How it connects
- **Depends on**: Task 3.1 (`IPushNotificationSender` interface), Task 3.2 (`PushNotificationSender` implementation), Task 1.3 (VAPID configuration)
- **Enables**: Task 3.3 (NotificationService can now inject `IPushNotificationSender`)
- The `NotificationService` will inject `IPushNotificationSender` to dispatch push notifications after persisting in-app notifications.

## How to run / verify
```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```
Build should succeed with 0 errors and 0 warnings.

## What comes next
- Task 3.3: Integrate push delivery into `NotificationService` (inject `IPushNotificationSender`)
- Task 3.5: Property tests for `PushNotificationSender`

## Git commit
```bash
git add -A && git commit -m "feat(push): register PushNotificationSender in DI with named HttpClient"
```
