# 158 — Create Push Subscription Command

## Phase

Push Notifications — Backend Application Layer

## Purpose

Implements the MediatR command and handler for creating (registering) a push subscription. This is the core write operation that persists a user's push subscription for a specific space, enabling push notification delivery to their device.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Notifications/CreatePushSubscriptionCommand.cs` | MediatR `IRequest` record and handler implementing upsert semantics |

## Key decisions

- **Upsert semantics**: If a subscription with the same (userId, spaceId, endpoint) already exists, the handler returns success without creating a duplicate. This makes the endpoint idempotent.
- **Domain validation**: The handler relies on `PushSubscription.Create()` for input validation (HTTPS endpoint, non-empty p256dh/auth). No separate FluentValidation in the handler — the domain entity is the source of truth for invariants.
- **Void return (`IRequest`)**: The controller returns 201 Created regardless of whether the subscription was newly created or already existed. No need to distinguish between the two cases at the API level.
- **Follows existing pattern**: Matches the style of `DismissNotificationCommand` — same file contains both the record and handler, uses `AppDbContext` directly.

## How it connects

- **Domain**: Uses `PushSubscription.Create()` factory method (task 1.1) for entity creation and validation
- **Persistence**: Uses `AppDbContext.PushSubscriptions` DbSet (task 1.2) for querying and persisting
- **Controller**: Will be dispatched by `PushSubscriptionsController.Subscribe()` (task 4.1)
- **Validation**: FluentValidation (task 4.2) will add an additional validation layer at the API boundary

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj
dotnet build Jobuler.Api/Jobuler.Api.csproj
```

Both should compile without errors.

## What comes next

- Task 2.2: `DeletePushSubscriptionCommand` (already exists)
- Task 2.3: `GetPushSubscriptionStatusQuery` (already exists)
- Task 2.4: Property tests for subscription commands (FsCheck)
- Task 4.1: `PushSubscriptionsController` that dispatches this command

## Git commit

```bash
git add -A && git commit -m "feat(push): create push subscription command and handler"
```
