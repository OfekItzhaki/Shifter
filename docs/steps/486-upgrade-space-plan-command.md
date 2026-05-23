# 486 — Upgrade Space Plan Command

## Phase

Space-Level Billing — Application Layer Commands

## Purpose

Implements the `UpgradeSpacePlanCommand` that allows space admins to upgrade their subscription plan by creating a LemonSqueezy checkout session for a higher-tier variant. This command enforces that only active or trialing subscriptions can be upgraded, and always includes `space_id` in checkout metadata for webhook correlation.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/UpgradeSpacePlanCommand.cs` | Command record (`SpaceId`, `UserId`, `VariantId`) and MediatR handler that verifies BillingManage permission, loads the space subscription, rejects if not Active/Trialing, and creates a LemonSqueezy checkout with the selected variant ID |
| `apps/api/Jobuler.Application/Billing/Validators/UpgradeSpacePlanValidator.cs` | FluentValidation validator ensuring `SpaceId`, `UserId`, and `VariantId` are not empty |

## Key decisions

- **VariantId is required** (not optional like in `CreateSpaceCheckoutCommand`) because an upgrade always targets a specific higher-tier variant selected by the admin.
- **Guard clause throws `InvalidOperationException`** when subscription status is not Active or Trialing, consistent with the domain entity's `UpdateTier` guard and the design document's Property 16.
- **No audit log** for the upgrade attempt itself — the actual tier change is recorded when the webhook arrives (via `HandleSpaceSubscriptionUpdatedCommand`).
- **Checkout metadata always includes `space_id`** for webhook correlation, satisfying Requirement 9.4.

## How it connects

- Called by `BillingController` via `POST /spaces/{spaceId}/billing/upgrade` (task 10.1)
- On successful checkout completion, LemonSqueezy sends a `subscription_updated` webhook which is handled by `HandleSpaceSubscriptionUpdatedCommand` (task 6.2) to update the tier
- Uses the same `ILemonSqueezyClient.CreateCheckoutAsync` and `CreateCheckoutRequest` as other checkout commands
- Validator is auto-discovered by MediatR pipeline behavior (FluentValidation integration)

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --filter "Billing"
```

## What comes next

- Task 5.5: `ExpireSpaceSubscriptionsCommand` (background job)
- Task 5.7: Property tests for checkout and upgrade commands (Properties 6, 7, 16)
- Task 10.1: Wire the upgrade endpoint in `BillingController`

## Git commit

```bash
git add -A && git commit -m "feat(billing): add UpgradeSpacePlanCommand and validator"
```
