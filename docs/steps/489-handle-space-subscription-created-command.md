# 489 — HandleSpaceSubscriptionCreatedCommand

## Phase

Space-Level Billing — Application Layer (Webhook Handling)

## Purpose

Handles the `subscription_created` webhook event for space-level subscriptions. When LemonSqueezy sends a webhook indicating a new subscription was created, this command activates the corresponding `SpaceSubscription` entity and triggers statistics period rotation.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/HandleSpaceSubscriptionCreatedCommand.cs` | MediatR command + handler that processes subscription_created webhooks for space subscriptions |

## Key decisions

- **No permission check**: This is a webhook handler dispatched internally after signature verification — no user context exists.
- **Idempotency**: If the subscription is already active with a LemonSqueezy ID, the handler returns early (no-op).
- **Graceful skip on missing data**: If `space_id` is missing from metadata or no `SpaceSubscription` exists, the handler logs a warning and returns (acknowledges webhook to prevent retries).
- **Period date fallback**: If `current_period_start` is missing, defaults to `DateTime.UtcNow`. If `current_period_end` is missing, falls back to `renews_at`, then to `periodStart + 1 month`.
- **Tier from variant_id**: The tier ID is extracted from the `variant_id` field in the webhook payload attributes, matching the checkout metadata pattern.
- **Statistics period rotation**: After activation, `IStatisticsPeriodService.OnSubscriptionActivatedAsync` is called to close existing periods and open new ones for all groups in the space.

## How it connects

- Dispatched by `HandleWebhookCommand` (task 6.4 will add the routing logic for space-level events).
- Uses `SpaceSubscription.Activate()` from the domain entity (task 1.1).
- Calls `IStatisticsPeriodService.OnSubscriptionActivatedAsync` (task 3.1/3.3).
- Follows the same pattern as the existing `HandleSubscriptionCreatedCommand` (group-level).

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application
```

The command will be wired into the webhook dispatch flow in task 6.4.

## What comes next

- Task 6.2: `HandleSpaceSubscriptionUpdatedCommand` — handles period/tier updates from webhooks.
- Task 6.3: `HandleSpaceSubscriptionCancelledCommand` — handles cancellation webhooks.
- Task 6.4: Update `HandleWebhookCommand` to route space-level events to these handlers.

## Git commit

```bash
git add -A && git commit -m "feat(billing): add HandleSpaceSubscriptionCreatedCommand webhook handler"
```
