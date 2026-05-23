# 486 — Renew Space Subscription Command

## Phase

Space-Level Billing — Application Layer Commands

## Purpose

Implements the `RenewSpaceSubscriptionCommand` and handler that allows a space admin to renew a canceled or expired space subscription. The handler determines whether the subscription is within its grace period (canceled but period not yet ended) or fully expired, and calls the appropriate domain method. For expired renewals, it triggers statistics period rotation via `IStatisticsPeriodService.OnPeriodRenewedAsync`.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/RenewSpaceSubscriptionCommand.cs` | MediatR command record (`SpaceId`, `UserId`) and handler implementing renewal logic with permission check, grace period detection, and statistics period rotation |
| `apps/api/Jobuler.Application/Billing/Validators/RenewSpaceSubscriptionValidator.cs` | FluentValidation validator ensuring `SpaceId` and `UserId` are non-empty |

## Key decisions

- **Grace period detection**: A subscription is within grace period when `Status == Canceled && CurrentPeriodEnd > now`. This preserves existing period dates on renewal.
- **Expired renewal**: When past grace period or fully expired, new period dates start from `DateTime.UtcNow` with a 1-month duration, and `OnPeriodRenewedAsync` is triggered to rotate statistics periods for all groups.
- **No audit log**: Unlike the group-level `RenewSubscriptionCommand`, the space-level command does not log to audit since the design doc doesn't specify it for this operation. Can be added later if needed.
- **Domain guards**: The entity's `RenewWithinGracePeriod()` and `RenewAfterExpiry()` methods throw `InvalidOperationException` if the subscription is already active, providing defense-in-depth.

## How it connects

- Depends on `SpaceSubscription` entity (task 1.1) for domain logic
- Depends on `IStatisticsPeriodService` (task 3.1/3.3) for period rotation
- Will be wired to `POST /spaces/{spaceId}/billing/renew` endpoint in task 10.1
- Validates requirements 6.5, 6.6, 6.7 from the space-billing spec

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build should succeed with no errors in the Application project.

## What comes next

- Task 5.4: `UpgradeSpacePlanCommand` and handler
- Task 10.1: Wire the renew endpoint in `BillingController`

## Git commit

```bash
git add -A && git commit -m "feat(billing): add RenewSpaceSubscriptionCommand and validator"
```
