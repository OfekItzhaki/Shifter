# 419 — Subscription Expired Status & Lifecycle Methods

## Phase

Subscription Cancellation & Renewal — Domain Layer

## Purpose

Extends the `GroupSubscription` domain entity with the `Expired` status and lifecycle transition methods (`Expire()`, `Renew()`), and adds a `Reactivate()` method to the `Group` entity. These are the foundational domain primitives for the full cancel → expire → renew workflow.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Billing/GroupSubscription.cs` | Added `Expired` to `SubscriptionStatus` enum |
| `apps/api/Jobuler.Domain/Billing/GroupSubscription.cs` | Added guard to `Cancel()` — throws if already `Canceled` or `Expired` |
| `apps/api/Jobuler.Domain/Billing/GroupSubscription.cs` | Added `Expire()` method — transitions from `Canceled` only |
| `apps/api/Jobuler.Domain/Billing/GroupSubscription.cs` | Added `Renew(DateTime, DateTime)` method — transitions from `Canceled`/`Expired` to `Active`, clears `CanceledAt`, sets period dates |
| `apps/api/Jobuler.Domain/Groups/Group.cs` | Added `Reactivate()` method — sets `IsActive = true`, calls `Touch()` |

## Key decisions

- `Cancel()` now throws `InvalidOperationException` if the subscription is already `Canceled` or `Expired`, enforcing the state machine at the domain level.
- `Expire()` only transitions from `Canceled` — this ensures only subscriptions that were explicitly canceled can expire (active subscriptions auto-renew externally via LemonSqueezy).
- `Renew()` accepts period dates as parameters, allowing the application layer to decide whether to preserve existing dates (within-period renewal) or create new ones (post-expiry renewal).
- `Reactivate()` is the inverse of `Deactivate()` — simple and symmetric.

## How it connects

- The `Expire()` method will be called by the `ExpireSubscriptionsCommand` handler (task 3.3) via a background job.
- The `Renew()` method will be called by the `RenewSubscriptionCommand` handler (task 3.2).
- The `Reactivate()` method will be called during renewal to restore group access (task 3.2).
- The `Cancel()` guard prevents double-cancellation (requirement 1.3).
- Property tests in tasks 1.3–1.7 will validate these state transitions.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain
dotnet build  # full solution
```

All projects build successfully with no new warnings.

## What comes next

- Task 1.2: Add `BillingManage` permission constant
- Tasks 1.3–1.7: Property-based tests for the domain state transitions
- Task 3.1: Refactor `CancelSubscriptionCommand` to use the new guard

## Git commit

```bash
git add -A && git commit -m "feat(billing): add Expired status and lifecycle methods to GroupSubscription"
```
