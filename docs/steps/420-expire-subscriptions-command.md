# 420 — ExpireSubscriptionsCommand (Batch Expiry)

## Phase

Subscription Cancellation & Renewal — Application Layer

## Purpose

Implements the batch expiry command that transitions canceled subscriptions past their billing period end to `Expired` status and deactivates their associated groups. This is invoked by a background job (no user-facing permission check needed).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/ExpireSubscriptionsCommand.cs` | MediatR command + handler that queries canceled subscriptions past period end, expires them, deactivates groups, and writes audit logs — all in a single transaction |

## Key decisions

- **Single transaction**: All subscription expirations, group deactivations, and audit log entries are saved in one `SaveChangesAsync` call to ensure atomicity.
- **System actor**: Uses `Guid.Empty` as the actor for audit logs since this is a system-initiated batch operation.
- **Trialing subscriptions**: Handles canceled trialing subscriptions (where `CurrentPeriodEnd == null` and `TrialEndsAt < now`) separately from regular canceled subscriptions.
- **Direct audit log creation**: Adds `AuditLog` entries directly to the DbContext instead of using `IAuditLogger` (which calls `SaveChangesAsync` per entry), preserving the single-transaction guarantee.

## How it connects

- Depends on `GroupSubscription.Expire()` domain method (task 1.1)
- Depends on `Group.Deactivate()` domain method (existing)
- Will be invoked by `ExpireSubscriptionsJob` background service (task 5.2)
- Works alongside `CancelSubscriptionCommand` (task 3.1) and `RenewSubscriptionCommand` (task 3.2)

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

## What comes next

- Task 3.4: Extend `GetSubscriptionQuery` response with cancellation/expiry fields
- Task 5.2: Wire up `ExpireSubscriptionsJob` background service that dispatches this command

## Git commit

```bash
git add -A && git commit -m "feat(billing): implement ExpireSubscriptionsCommand batch expiry handler"
```
