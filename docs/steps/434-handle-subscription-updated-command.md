# 434 — Handle Subscription Updated Command

## Phase

LemonSqueezy Billing Integration — Application Layer Webhook Handlers

## Purpose

Implements the MediatR handler for `subscription_updated` webhook events from LemonSqueezy. When LemonSqueezy reports a subscription status or period change, this handler maps the incoming status to the internal `SubscriptionStatus` enum, updates period dates, and reactivates groups when a subscription transitions to Active.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Billing/Commands/HandleSubscriptionUpdatedCommand.cs` | Added `HandleSubscriptionUpdatedCommandHandler` implementing the full subscription update logic |

## Key decisions

1. **Lookup by LemonSqueezy subscription ID** — The handler queries `GroupSubscriptions` by `LemonSqueezySubscriptionId` (extracted from the webhook payload `data.id`), not by metadata space/group IDs. This is more reliable for update events since the subscription already exists.
2. **No-op detection** — If both the mapped status and period dates match the current stored values, the handler returns early without any DB write or audit log entry (requirement 10.3).
3. **Period date parsing** — Prefers `current_period_start`/`current_period_end` fields if present, falls back to `created_at`/`renews_at` for compatibility with different LemonSqueezy payload shapes.
4. **Group reactivation** — Only triggers when transitioning TO Active FROM Trialing/PastDue/Canceled/Expired, and only if the group is currently deactivated. Uses the existing `Group.Reactivate()` method.
5. **Unrecognized status handling** — Logs a warning and returns without modifying any state, per requirement 4.3.

## How it connects

- Dispatched by `HandleWebhookCommandHandler` when event type is `subscription_updated`
- Uses `GroupSubscription.UpdateStatus()` and `GroupSubscription.UpdatePeriod()` domain methods (task 1.1)
- Uses `Group.Reactivate()` to restore group active state
- Relies on `AppDbContext` for persistence (registered in DI via task 2.5)

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The handler is invoked automatically when a `subscription_updated` webhook arrives through the `HandleWebhookCommand` dispatch pipeline.

## What comes next

- Task 5.4: Implement HandleSubscriptionCancelledCommand
- Task 5.5: Implement HandlePaymentSuccessCommand
- Task 5.6: Property tests for subscription event handlers

## Git commit

```bash
git add -A && git commit -m "feat(billing): implement HandleSubscriptionUpdatedCommand handler"
```
