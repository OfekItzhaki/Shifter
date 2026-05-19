# 421 — Renew Subscription Command

## Phase

Subscription Cancellation & Renewal — Application Layer

## Purpose

Implements the `RenewSubscriptionCommand` handler that allows space owners to renew a canceled or expired subscription, restoring the group to active status with a new or preserved billing period.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/RenewSubscriptionCommand.cs` | Command record, handler with permission check, renewal logic (within-period vs expired), group reactivation, and audit logging |
| `apps/api/Jobuler.Application/Billing/Validators/RenewSubscriptionValidator.cs` | FluentValidation validator ensuring SpaceId, GroupId, and ActorUserId are non-empty |

## Key decisions

- **Permission check first** — `BillingManage` permission is required before any state mutation. Space owners pass implicitly via `PermissionService`.
- **Two renewal paths** — If canceled and still within the billing period (`CurrentPeriodEnd > UtcNow`), the existing period is preserved. Otherwise, a new 1-month period is created starting now.
- **Group reactivation** — If the group was deactivated (Limited_Mode), it's reactivated on renewal.
- **Active subscription rejection** — Throws `InvalidOperationException` if the subscription is already active, matching the domain's `Renew()` guard.
- **Audit logging** — Records `subscription.renew` action with space, actor, and entity reference.

## How it connects

- Uses `GroupSubscription.Renew()` domain method (implemented in task 1.1)
- Uses `Group.Reactivate()` domain method (implemented in task 1.1)
- Uses `Permissions.BillingManage` constant (implemented in task 1.2)
- Will be exposed via `BillingController` endpoint (task 5.1)
- Follows same pattern as `CancelSubscriptionCommand` and `DeactivateFreezeWithDiscardCommand`

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build
```

Build should succeed with zero errors.

## What comes next

- Task 3.3: `ExpireSubscriptionsCommand` handler (batch expiry)
- Task 5.1: API endpoint wiring in `BillingController`
- Property tests for renewal behavior (tasks 3.8, 3.9)

## Git commit

```bash
git add -A && git commit -m "feat(billing): implement RenewSubscriptionCommand handler and validator"
```
