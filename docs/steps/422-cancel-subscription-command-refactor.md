# Step 422 — Cancel Subscription Command Refactor

## Phase

Subscription Cancellation & Renewal

## Purpose

Refactor the existing `CancelSubscriptionCommand` to add authorization checks, audit logging, trialing-specific handling, and FluentValidation. This ensures only authorized users can cancel subscriptions, all cancellations are audited, and trialing subscriptions are immediately deactivated.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Billing/Commands/CancelSubscriptionCommand.cs` | Refactored command: added `ActorUserId` parameter, permission check via `IPermissionService.RequirePermissionAsync` for `BillingManage`, guard against already-canceled/expired subscriptions, trialing logic (immediate group deactivation), audit log entry, removed `ClosePeriodAsync` call |
| `Jobuler.Application/Billing/Validators/CancelSubscriptionValidator.cs` | New FluentValidation validator ensuring `SpaceId`, `GroupId`, and `ActorUserId` are not empty |

## Key decisions

- **Permission check in handler** — per architecture rules, permission checks happen in the Application layer via `IPermissionService`, not in controllers. Space owners implicitly pass via existing `PermissionService` logic.
- **Guard before Cancel()** — the handler checks status before calling `Cancel()` on the entity, providing a clear error message. The domain `Cancel()` method also throws, but the handler guard gives better context.
- **Trialing immediate deactivation** — when a trialing subscription is canceled, the group is immediately deactivated (no grace window), matching the state machine design.
- **Removed ClosePeriodAsync** — expiry is now handled by the background `ExpireSubscriptionsJob` (task 5.2), not inline after cancellation.
- **Audit log** — uses the existing `IAuditLogger.LogAsync` pattern with action `subscription.cancel`, entity type `group_subscription`.

## How it connects

- Depends on: `BillingManage` permission constant (task 1.2), `GroupSubscription.Cancel()` domain method (task 1.1), `Group.Deactivate()` method
- Used by: `BillingController` cancel endpoint (task 5.1)
- Related: `ExpireSubscriptionsJob` (task 5.2) handles the actual expiry after the grace period

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --filter "FullyQualifiedName~Billing"
```

Build should succeed with 0 errors. No billing-specific tests exist yet (added in later tasks).

## What comes next

- Task 3.2: `RenewSubscriptionCommand` handler
- Task 3.3: `ExpireSubscriptionsCommand` handler
- Task 5.1: BillingController cancel/renew endpoints

## Git commit

```bash
git add -A && git commit -m "feat(billing): refactor CancelSubscriptionCommand with auth, audit, and trialing handling"
```
