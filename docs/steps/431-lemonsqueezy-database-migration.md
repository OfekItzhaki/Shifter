# 431 — LemonSqueezy Database Migration

## Phase
LemonSqueezy Billing Integration — Infrastructure Layer

## Purpose
Migrate the database schema from Stripe to LemonSqueezy column names and create the `webhook_event_logs` table for idempotent webhook processing. This ensures the EF Core model matches the actual database schema after the domain entity rename (task 1.1) and supports the new `WebhookEventLog` entity (task 1.2).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/BillingConfiguration.cs` | Updated `GroupSubscriptionConfiguration` to map `LemonSqueezySubscriptionId` → `lemonsqueezy_subscription_id` and `LemonSqueezyCustomerId` → `lemonsqueezy_customer_id`. Added `WebhookEventLogConfiguration` with unique index on `event_id` and index on `processed_at`. |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Added `DbSet<WebhookEventLog> WebhookEventLogs` to expose the new entity for queries. |
| `infra/migrations/067_lemonsqueezy_billing_migration.sql` | SQL migration that renames `stripe_subscription_id` → `lemonsqueezy_subscription_id`, `stripe_customer_id` → `lemonsqueezy_customer_id`, drops the old Stripe index, creates a new index on the LemonSqueezy column, and creates the `webhook_event_logs` table with appropriate indexes. |

## Key decisions
- Used raw SQL migration (project convention) rather than EF Core code-first migrations.
- The unique index on `event_id` in `webhook_event_logs` also serves as the concurrency guard — if two duplicate webhooks arrive simultaneously, the database constraint ensures only one row is inserted.
- Kept the `uuid_generate_v4()` default for the `id` column consistent with all other tables in the project.
- Dropped the old `idx_group_subscriptions_stripe` index and created `idx_group_subscriptions_lemonsqueezy` to maintain query performance on subscription lookups.

## How it connects
- **Depends on**: Task 1.1 (domain entity rename) and Task 1.2 (WebhookEventLog entity)
- **Enables**: Task 5.1 (HandleWebhookCommand) which queries `WebhookEventLogs` for idempotency checks
- **Enables**: All webhook handlers that look up subscriptions by `LemonSqueezySubscriptionId`

## How to run / verify
1. Apply the migration against your local PostgreSQL:
   ```bash
   psql -U jobuler -d jobuler -f infra/migrations/067_lemonsqueezy_billing_migration.sql
   ```
2. Verify columns were renamed:
   ```sql
   SELECT column_name FROM information_schema.columns WHERE table_name = 'group_subscriptions' AND column_name LIKE 'lemonsqueezy%';
   ```
3. Verify the new table exists:
   ```sql
   SELECT * FROM information_schema.tables WHERE table_name = 'webhook_event_logs';
   ```
4. Build the API: `dotnet build` from `apps/api/` — should succeed with no errors.

## What comes next
- Task 2.5: Register LemonSqueezy services in DI
- Task 5.1: HandleWebhookCommand (uses `WebhookEventLogs` DbSet for idempotency)

## Git commit
```bash
git add -A && git commit -m "feat(billing): database migration from Stripe to LemonSqueezy columns + webhook_event_logs table"
```
