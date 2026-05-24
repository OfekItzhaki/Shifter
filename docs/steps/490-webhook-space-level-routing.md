# 490 — Webhook Space-Level Event Routing

## Phase

Space-Level Billing — Application Layer (Webhook Handling)

## Purpose

Updates the `HandleWebhookCommand` handler to route webhook events to either space-level or group-level handlers based on the presence of `space_id` (without `group_id`) in the webhook metadata. This enables the new space-level billing model while maintaining full backward compatibility with existing group-level subscriptions.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/HandleWebhookCommand.cs` | Updated handler with space-level vs group-level routing logic, payload parsing for `HandleSpaceSubscriptionUpdatedCommand` |

## Key decisions

- **Routing heuristic**: Space-level events are identified by having `space_id` in metadata WITHOUT `group_id`. Group-level events have both `space_id` AND `group_id`. This matches how checkouts are created — space checkouts include only `space_id`, group checkouts include both.
- **Idempotency preserved**: The `WebhookEventLog` check remains at the top of the handler, applying to both space and group events identically.
- **Payload parsing for Updated command**: Since `HandleSpaceSubscriptionUpdatedCommand` takes pre-parsed parameters (SpaceId, VariantId, PeriodStart, PeriodEnd, AutoRenew), the routing layer parses the JSON payload. If parsing fails, it logs an error and skips (no crash).
- **Graceful fallback for period dates**: Uses `current_period_start` → `created_at` → `DateTime.UtcNow` for period start, and `current_period_end` → `renews_at` → `periodStart + 1 month` for period end.
- **Auto-renew detection**: Derived from the `cancelled` attribute in the LemonSqueezy payload (if `cancelled == true`, auto-renew is false).
- **Payment success no-op for space**: Space-level `subscription_payment_success` events are acknowledged but no handler is dispatched (logged for visibility).

## How it connects

- Dispatches to `HandleSpaceSubscriptionCreatedCommand` (task 6.1) for space-level `subscription_created` events.
- Dispatches to `HandleSpaceSubscriptionUpdatedCommand` (task 6.2) for space-level `subscription_updated` events.
- Dispatches to `HandleSpaceSubscriptionCancelledCommand` (task 6.3) for space-level `subscription_cancelled` events.
- Falls through to existing group-level handlers (`HandleSubscriptionCreatedCommand`, `HandleSubscriptionUpdatedCommand`, `HandleSubscriptionCancelledCommand`, `HandlePaymentSuccessCommand`) for backward compatibility.
- Called by `LemonSqueezyWebhookController` after signature verification.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application
dotnet test Jobuler.Tests --filter "FullyQualifiedName~WebhookSignatureAndIdempotencyPropertyTests"
dotnet test Jobuler.Tests --filter "FullyQualifiedName~SubscriptionEventHandlerPropertyTests"
```

All 25 existing tests pass with the updated routing logic.

## What comes next

- Task 6.5: Property tests for webhook handling (Properties 2, 8) — validates idempotency for space-level events.
- Task 10.2: Post-migration group billing rejection — returns 410 Gone for group-level billing ops after migration.

## Git commit

```bash
git add -A && git commit -m "feat(billing): route space-level webhook events in HandleWebhookCommand"
```
