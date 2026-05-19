# 427 — LemonSqueezy GroupSubscription Entity Migration

## Phase

LemonSqueezy Billing Integration — Domain Layer

## Purpose

Replace Stripe-specific identifiers on the `GroupSubscription` domain entity with LemonSqueezy equivalents, and add new methods (`UpdateStatus`, `UpdatePeriod`) needed for webhook-driven subscription state management.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Domain/Billing/GroupSubscription.cs` | Renamed `StripeSubscriptionId` → `LemonSqueezySubscriptionId`, `StripeCustomerId` → `LemonSqueezyCustomerId`; updated `Activate` method parameters; added `UpdateStatus` and `UpdatePeriod` methods |
| `Jobuler.Application/Billing/Commands/ActivateSubscriptionCommand.cs` | Renamed command record properties and handler call to use LemonSqueezy identifiers |
| `Jobuler.Infrastructure/Persistence/Configurations/BillingConfiguration.cs` | Updated EF Core property mapping to reference new C# property names (column names unchanged until migration task) |
| `Jobuler.Tests/Billing/SubscriptionApplicationPropertyTests.cs` | Updated `Activate` call to use LemonSqueezy test values |
| `Jobuler.Tests/Billing/SubscriptionLifecyclePropertyTests.cs` | Updated `Activate` call to use LemonSqueezy test values |
| `Jobuler.Tests/Billing/SubscriptionLifecycleIntegrationTests.cs` | Updated `Activate` call to use LemonSqueezy test values |

## Key decisions

- **Column names kept as-is** — The EF Core mapping still points to `stripe_subscription_id` / `stripe_customer_id` columns. The actual column rename happens in the database migration task (2.4) to keep this change non-breaking against the existing database.
- **`UpdateStatus` is intentionally simple** — No guard logic; the Application layer handlers are responsible for deciding when to call it based on webhook event semantics.
- **`UpdatePeriod` resets `PeakMemberCount`** — Only when `periodStart` differs from the current value, matching the billing requirement that a new billing period resets usage tracking.

## How it connects

- The `UpdateStatus` and `UpdatePeriod` methods will be called by the webhook event handlers (tasks 5.2–5.5).
- The `ActivateSubscriptionCommand` is called by the subscription-created webhook handler (task 5.2).
- The database migration (task 2.4) will rename the actual columns to match the new property names.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --filter "FullyQualifiedName~Billing"
```

All 21 billing tests pass.

## What comes next

- Task 1.2: Create `WebhookEventLog` domain entity
- Task 2.4: Database migration to rename columns

## Git commit

```bash
git add -A && git commit -m "feat(billing): migrate GroupSubscription from Stripe to LemonSqueezy identifiers"
```
