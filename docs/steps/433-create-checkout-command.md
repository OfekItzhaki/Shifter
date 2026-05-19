# 433 — Create Checkout Command

## Phase

LemonSqueezy Billing Integration — Application Layer

## Purpose

Implements the `CreateCheckoutCommand` and its handler, which allows space admins to initiate a LemonSqueezy checkout session for a group. This is the core command that validates permissions, group existence, subscription state, and delegates to the LemonSqueezy API client.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Billing/Commands/CreateCheckoutCommand.cs` | MediatR command record with `SpaceId`, `GroupId`, `UserId` properties and handler that orchestrates the checkout flow |
| `Jobuler.Application/Billing/BillingOptions.cs` | Options class for billing configuration (variant IDs) accessible from the Application layer |
| `Jobuler.Api/Program.cs` | Added `BillingOptions` DI registration from the "LemonSqueezy" config section |

## Key decisions

- Created `BillingOptions` in the Application layer rather than referencing `LemonSqueezySettings` from Infrastructure, respecting the Clean Architecture layering rule (Application cannot reference Infrastructure).
- `BillingOptions` is bound to the same "LemonSqueezy" config section, containing only the fields needed by Application-layer handlers (`DefaultVariantId`, `TestVariantId`).
- The command returns `IRequest<string>` (the checkout URL) rather than a wrapper DTO, matching the simplicity of the existing patterns.
- Permission check uses `Permissions.BillingManage` consistent with `CancelSubscriptionCommand` and `RenewSubscriptionCommand`.
- Group validation uses `AnyAsync` for existence check (no need to load the full entity).
- Subscription check uses `FirstOrDefaultAsync` to inspect status before rejecting.

## How it connects

- Called by `BillingController` (task 7.2) via MediatR dispatch
- Depends on `ILemonSqueezyClient` (task 2.2) for checkout session creation
- Depends on `BillingOptions` / `LemonSqueezySettings` (task 2.1) for variant ID configuration
- Uses `IPermissionService` for authorization (existing infrastructure)
- Validates against `GroupSubscription` entity (task 1.1) for active/trialing guard

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --no-build --filter "FullyQualifiedName~Billing"
```

All 21 billing tests pass. Build succeeds with no errors.

## What comes next

- Task 4.2: Property test for checkout rejection when active/trialing subscription exists
- Task 7.2: Wire the command to the BillingController endpoint

## Git commit

```bash
git add -A && git commit -m "feat(billing): implement CreateCheckoutCommand and handler"
```
