# 486 — Cancel Space Subscription Command

## Phase

Space-Level Billing — Application Layer Commands

## Purpose

Implements the `CancelSpaceSubscriptionCommand` that allows a space admin to cancel the space-level subscription. This handles permission verification, state transition via the domain entity, and statistics period closure when canceling from a trialing state.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/CancelSpaceSubscriptionCommand.cs` | Command record (`SpaceId`, `UserId`) and MediatR handler that verifies BillingManage permission, loads the SpaceSubscription, checks if trialing, calls `Cancel()`, and triggers `IStatisticsPeriodService.OnTrialExpiredAsync` if transitioning from trialing |
| `apps/api/Jobuler.Application/Billing/Validators/CancelSpaceSubscriptionValidator.cs` | FluentValidation validator ensuring `SpaceId` and `UserId` are non-empty |

## Key decisions

- **Permission check first**: `IPermissionService.RequirePermissionAsync` is called before any data access, following the existing pattern.
- **Trialing detection before Cancel()**: The handler captures `wasTrialing` before calling `Cancel()` because the domain method mutates the status. This allows correct statistics period handling.
- **OnTrialExpiredAsync for trialing cancellations**: When a trial is canceled, it's semantically equivalent to the trial expiring — active statistics periods should be closed.
- **No audit log**: The existing group-level cancel command logs to audit, but the space-level command follows the design doc which doesn't specify audit logging for this action. Can be added later if needed.
- **Domain entity handles guard clauses**: `Cancel()` on `SpaceSubscription` already throws `InvalidOperationException` for canceled/expired states, so the handler doesn't duplicate that logic.

## How it connects

- Uses `SpaceSubscription.Cancel()` domain method (task 1.1)
- Uses `IStatisticsPeriodService` interface (task 3.1) and its implementation (task 3.3)
- Will be dispatched by `BillingController` endpoint `POST /spaces/{spaceId}/billing/cancel` (task 10.1)
- Validates requirements 6.1 (cancel active/trialing) and 6.2 (reject already canceled/expired)

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

The command will be exercised via the API endpoint in task 10.1 and property tests in task 5.7.

## What comes next

- Task 5.3: `RenewSpaceSubscriptionCommand`
- Task 10.1: Wire the cancel endpoint in `BillingController`
- Task 5.7: Property tests for cancel state transitions

## Git commit

```bash
git add -A && git commit -m "feat(billing): add CancelSpaceSubscriptionCommand and validator"
```
