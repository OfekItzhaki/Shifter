# 489 — HandleSpaceSubscriptionCancelledCommand

## Phase

Space-Level Billing — Application Layer (Webhook Handling)

## Purpose

Handles the `subscription_cancelled` webhook event from LemonSqueezy at the space level. When LemonSqueezy notifies us that a subscription has been cancelled, this command loads the corresponding `SpaceSubscription` and transitions it to the `Canceled` state.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/HandleSpaceSubscriptionCancelledCommand.cs` | Command record and MediatR handler for processing space-level subscription cancellation webhooks |

## Key decisions

1. **Graceful handling of already-canceled/expired subscriptions** — Since webhooks can be retried by LemonSqueezy, the handler logs and skips if the subscription is already in `Canceled` or `Expired` status rather than throwing an exception. This prevents webhook retry loops.
2. **No permission check** — This is a webhook handler invoked by the system (via the webhook controller), not by a user action. No `IPermissionService` call is needed.
3. **No statistics period service call** — Unlike the user-initiated `CancelSpaceSubscriptionCommand` (which triggers `OnTrialExpiredAsync` when canceling a trialing subscription), the webhook handler simply records the cancellation. The expiry job (`ExpireSpaceSubscriptionsCommand`) handles the period closure when the subscription actually expires.
4. **Missing metadata handled gracefully** — If `space_id` is missing or invalid in the webhook metadata, the handler logs a warning and returns (acknowledges the webhook to prevent retries).

## How it connects

- **Upstream**: Dispatched by `HandleWebhookCommand` (task 6.4) when it detects a `subscription_cancelled` event with `space_id` in metadata.
- **Domain**: Calls `SpaceSubscription.Cancel()` which sets status to `Canceled` and records `CanceledAt`.
- **Downstream**: After cancellation, the `ExpireSpaceSubscriptionsCommand` background job will eventually expire the subscription when `CurrentPeriodEnd` passes.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application
```

The command will be wired into the webhook routing in task 6.4. Until then, it can be verified by:
1. Confirming the project builds without errors
2. Confirming the handler follows the same pattern as `HandleSubscriptionCancelledCommand` (group-level)

## What comes next

- Task 6.4: Update `HandleWebhookCommand` to route space-level events (including `subscription_cancelled`) to this handler
- Task 6.5: Property tests for webhook handling (idempotency)

## Git commit

```bash
git add -A && git commit -m "feat(billing): add HandleSpaceSubscriptionCancelledCommand webhook handler"
```
