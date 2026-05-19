# 435 — Handle Subscription Cancelled Command

## Phase

LemonSqueezy Billing Integration — Application Layer Webhook Handlers

## Purpose

Implements the MediatR handler for `subscription_cancelled` webhook events from LemonSqueezy. When a subscription is cancelled, this handler marks the subscription as Canceled, records the cancellation timestamp, and determines whether to deactivate the group immediately or defer to the `ExpireSubscriptionsJob`.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Billing/Commands/HandleSubscriptionCancelledCommand.cs` | Added `HandleSubscriptionCancelledCommandHandler` implementing the full subscription cancellation logic |

## Key decisions

1. **Lookup by LemonSqueezy subscription ID** — The handler queries `GroupSubscriptions` by `LemonSqueezySubscriptionId` (extracted from the webhook payload `data.id`), consistent with the pattern used by the updated and payment success handlers.
2. **Idempotent no-op for already-canceled subscriptions** — If the subscription status is already Canceled or Expired, the handler returns early without any modification (requirement 5.4).
3. **Uses existing `Cancel()` domain method** — Leverages the `GroupSubscription.Cancel()` method which sets both Status=Canceled and CanceledAt=DateTime.UtcNow in a single atomic operation.
4. **Immediate vs deferred deactivation** — If `CurrentPeriodEnd` is null or in the past, the group is deactivated immediately. If in the future, the group remains active and the existing `ExpireSubscriptionsJob` handles deactivation when the period ends.
5. **Graceful handling of missing data** — If no subscription or group is found, the handler logs a warning and returns without error, ensuring the webhook always gets a 200 response.

## How it connects

- Dispatched by `HandleWebhookCommandHandler` when event type is `subscription_cancelled`
- Uses `GroupSubscription.Cancel()` domain method (defined in task 1.1)
- Uses `Group.Deactivate()` for immediate group deactivation
- Deferred expiration handled by `ExpireSubscriptionsCommand` (existing job, no changes needed)
- Relies on `AppDbContext` for persistence (registered in DI via task 2.5)

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The handler is invoked automatically when a `subscription_cancelled` webhook arrives through the `HandleWebhookCommand` dispatch pipeline.

## What comes next

- Task 5.5: Implement HandlePaymentSuccessCommand
- Task 5.6: Property tests for subscription event handlers

## Git commit

```bash
git add -A && git commit -m "feat(billing): implement HandleSubscriptionCancelledCommand handler"
```
