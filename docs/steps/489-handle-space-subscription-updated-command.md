# Step 489 — HandleSpaceSubscriptionUpdatedCommand

## Phase

Phase: Space-Level Billing — Application Layer (Webhook Handling)

## Purpose

Handles the `subscription_updated` webhook event for space-level subscriptions. When LemonSqueezy sends a subscription update (period renewal, plan change, auto-renew toggle), this command updates the `SpaceSubscription` entity accordingly and triggers statistics period rotation when the billing period changes.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/HandleSpaceSubscriptionUpdatedCommand.cs` | Command record and MediatR handler for processing space subscription updates from webhooks |

## Key decisions

- **Pre-parsed command parameters**: Unlike the group-level `HandleSubscriptionUpdatedCommand` which receives raw JSON payload, this command accepts pre-parsed fields (`SpaceId`, `VariantId`, `PeriodStart`, `PeriodEnd`, `AutoRenew`). The JSON parsing responsibility stays in the webhook router (`HandleWebhookCommand`) which will be updated in task 6.4.
- **Period change detection**: Compares incoming period dates against current values before calling `UpdatePeriod()`. Only triggers statistics rotation and peak reset when dates actually differ.
- **No permission check**: This is a webhook handler — authentication is handled at the controller level via HMAC signature verification.
- **Graceful handling of missing subscription**: Logs a warning and returns early if no `SpaceSubscription` exists for the given `SpaceId`, rather than throwing.
- **Tier update guard**: Uses `UpdateTier()` which has its own guard clause (only allows updates when Active or Trialing).

## How it connects

- **Upstream**: Will be dispatched by `HandleWebhookCommand` (task 6.4) when a `subscription_updated` event contains `space_id` in metadata.
- **Domain**: Calls `SpaceSubscription.UpdatePeriod()`, `UpdateTier()`, `SetAutoRenew()`, and `ResetPeakForNewPeriod()`.
- **Statistics**: Triggers `IStatisticsPeriodService.OnPeriodRenewedAsync` to close active periods and open new ones for all groups in the space.
- **Sibling commands**: Follows the same pattern as `HandleSpaceSubscriptionCreatedCommand` (task 6.1) and `HandleSpaceSubscriptionCancelledCommand` (task 6.3).

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application
```

The command will be exercised end-to-end once the webhook router (task 6.4) is updated to dispatch space-level events.

## What comes next

- Task 6.3: `HandleSpaceSubscriptionCancelledCommand` — handles cancellation webhooks
- Task 6.4: Update `HandleWebhookCommand` to route space-level events to these new handlers
- Task 6.5: Property tests for webhook handling (idempotency)

## Git commit

```bash
git add -A && git commit -m "feat(billing): add HandleSpaceSubscriptionUpdatedCommand for space webhook handling"
```
