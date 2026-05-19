# 418 — BillingManage Permission Constant

## Phase

Subscription Cancellation & Renewal — Domain Layer Extensions

## Purpose

Adds the `BillingManage` permission constant to the Domain layer so that `CancelSubscriptionCommand` and `RenewSubscriptionCommand` handlers can authorize billing operations via `IPermissionService.RequirePermissionAsync`.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Spaces/SpacePermissionGrant.cs` | Added `public const string BillingManage = "billing.manage";` to the `Permissions` static class |

## Key decisions

- Permission key follows the existing `{domain}.{action}` naming convention (`billing.manage`).
- Space owners implicitly hold all permissions via existing `PermissionService` logic — no additional wiring needed.
- No migration required: permission keys are stored as strings in the DB, so new constants can be added without schema changes.

## How it connects

- Used by `CancelSubscriptionCommand` (task 3.1) and `RenewSubscriptionCommand` (task 3.2) for authorization checks.
- Follows the same pattern as `SchedulePublish`, `PermissionsManage`, etc.

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build
```

Confirm no compilation errors and that `Permissions.BillingManage` resolves to `"billing.manage"`.

## What comes next

- Property tests for domain subscription transitions (tasks 1.3–1.7)
- Application layer commands that reference this permission (tasks 3.1, 3.2)

## Git commit

```bash
git add -A && git commit -m "feat(billing): add BillingManage permission constant"
```
