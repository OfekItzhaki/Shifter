# 434 — HandlePaymentSuccessCommand

## Phase

LemonSqueezy Billing Integration — Application Layer Webhook Handlers

## Purpose

Implements the handler for `subscription_payment_success` webhook events from LemonSqueezy. When a payment succeeds, the billing period is updated and, if the subscription was in PastDue status, it transitions back to Active.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Billing/Commands/HandlePaymentSuccessCommand.cs` | Added `HandlePaymentSuccessCommandHandler` to the existing command record file. Parses the LemonSqueezy webhook payload, looks up the subscription by LemonSqueezy subscription ID, updates the billing period (which resets PeakMemberCount when period start changes), and transitions PastDue → Active. |

## Key decisions

- **Subscription lookup by LemonSqueezy subscription ID**: The payment success payload contains a `subscription_id` in `data.attributes`, which is used to find the matching `GroupSubscription` entity. This is consistent with how `HandleSubscriptionUpdatedCommand` and `HandleSubscriptionCancelledCommand` look up subscriptions.
- **Period date extraction**: Uses `current_period_start` and `current_period_end` from the payload attributes, with fallbacks to `renews_at` for end date and the existing subscription values if not provided.
- **PeakMemberCount reset via UpdatePeriod**: The `GroupSubscription.UpdatePeriod` method already handles resetting `PeakMemberCount` to 0 when the period start changes — no additional logic needed in the handler.
- **PastDue → Active transition**: Only transitions to Active if the current status is PastDue. Other statuses are left unchanged after the period update.

## How it connects

- Dispatched by `HandleWebhookCommandHandler` when event type is `subscription_payment_success`
- Uses `GroupSubscription.UpdatePeriod()` and `GroupSubscription.UpdateStatus()` domain methods
- Follows the same pattern as `HandleSubscriptionUpdatedCommand` and `HandleSubscriptionCancelledCommand`
- Validates requirements 6.1, 6.2, 6.3, 6.4

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The handler will be exercised by property tests in task 5.6 (Properties 13 and 14).

## What comes next

- Task 5.6: Property-based tests for subscription event handlers (including payment success scenarios)
- Task 7.1: LemonSqueezyWebhookController wiring that dispatches to this handler

## Git commit

```bash
git add -A && git commit -m "feat(billing): implement HandlePaymentSuccessCommand handler"
```
