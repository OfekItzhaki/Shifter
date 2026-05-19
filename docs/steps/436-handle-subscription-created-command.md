# Step 436 — HandleSubscriptionCreatedCommand

## Phase

LemonSqueezy Billing Integration — Application Layer Webhook Handlers

## Purpose

Implements the `subscription_created` webhook event handler that activates or starts a trial for a `GroupSubscription` when LemonSqueezy confirms a new subscription. This is the entry point for converting a completed checkout into an active subscription in the system.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Billing/Commands/HandleSubscriptionCreatedCommand.cs` | MediatR command and handler for `subscription_created` webhook events |

### Handler logic

1. **Extract metadata** — Parses `space_id` and `group_id` from webhook metadata dictionary
2. **Parse payload** — Extracts subscription status, subscription ID, customer ID, period dates, and trial end date from the LemonSqueezy JSON payload
3. **Look up subscription** — Queries `GroupSubscriptions` by space and group
4. **Guard: missing subscription** — Logs warning and returns if no subscription exists
5. **Guard: already activated** — Treats as no-op if subscription already has Active/Trialing status with a stored LemonSqueezy ID
6. **Status "active"** — Calls `Activate()` with tier ID, LemonSqueezy IDs, and period dates
7. **Status "on_trial"** — Calls `StartTrial()` with LemonSqueezy IDs and trial end date
8. **Unknown status** — Logs warning and skips

## Key decisions

- Uses `StartTrial()` domain method (not just `UpdateStatus`) to properly store LemonSqueezy IDs alongside the trial state
- Tier ID is derived from `variant_id` (falling back to `product_id`, then default "pro") — this matches LemonSqueezy's product/variant model
- Period dates use `current_period_start`/`current_period_end` when available, falling back to `renews_at`
- The handler is dispatched from `HandleWebhookCommand` which already handles idempotency (event ID dedup)

## How it connects

- **Upstream**: `HandleWebhookCommand` dispatches this command for `subscription_created` events
- **Domain**: Uses `GroupSubscription.Activate()` and `GroupSubscription.StartTrial()` methods
- **Database**: Persists changes via `AppDbContext.SaveChangesAsync()`
- **Sibling handlers**: Follows the same JSON parsing pattern as `HandleSubscriptionUpdatedCommand` and `HandleSubscriptionCancelledCommand`

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --filter "SubscriptionCreated"
```

## What comes next

- Property tests for subscription creation (task 5.6)
- API layer webhook controller wiring (task 7.1)

## Git commit

```bash
git add -A && git commit -m "feat(billing): implement HandleSubscriptionCreatedCommand webhook handler"
```
