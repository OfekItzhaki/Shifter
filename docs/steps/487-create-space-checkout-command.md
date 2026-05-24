# 487 — Create Space Checkout Command

## Phase

Space-Level Billing — Application Layer Commands

## Purpose

Implements the `CreateSpaceCheckoutCommand` and handler that allows a space admin to initiate a LemonSqueezy checkout session at the space level. This replaces the group-level checkout flow for spaces that have migrated to space-level billing. The command enforces permission checks, rejects checkouts for already-active subscriptions, and passes `space_id` in metadata for webhook correlation.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/CreateSpaceCheckoutCommand.cs` | Command record (`SpaceId`, `UserId`, `VariantId?`) and MediatR handler that verifies BillingManage permission, rejects active subscriptions, and calls `ILemonSqueezyClient.CreateCheckoutAsync` with `space_id` metadata |
| `apps/api/Jobuler.Application/Billing/Validators/CreateSpaceCheckoutValidator.cs` | FluentValidation validator ensuring `SpaceId` and `UserId` are non-empty |

## Key decisions

- **VariantId is optional** — falls back to `BillingOptions.DefaultVariantId` when not provided, matching the existing group-level pattern.
- **Only Active status is rejected** — trialing, canceled, and expired subscriptions can proceed to checkout (they need to upgrade/reactivate).
- **Metadata contains only `space_id`** — no `group_id` since this is a space-level checkout. Webhook handlers will correlate via `space_id`.
- **No audit log in this command** — the checkout is a transient action (redirect to LemonSqueezy). The actual subscription activation is logged when the webhook arrives.

## How it connects

- **Upstream**: Will be dispatched by `BillingController` (task 10.1) via `POST /spaces/{spaceId}/billing/checkout`
- **Downstream**: Calls `ILemonSqueezyClient.CreateCheckoutAsync` (already implemented in Infrastructure)
- **Depends on**: `SpaceSubscription` entity (task 1.1), `ILemonSqueezyClient` interface, `IPermissionService`, `BillingOptions`
- **Consumed by**: Webhook handler `HandleSpaceSubscriptionCreatedCommand` (task 6.1) will process the resulting subscription

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

The command and validator compile cleanly. Full integration testing requires the API layer endpoint (task 10.1) and property tests (task 5.7).

## What comes next

- Task 5.2: `CancelSpaceSubscriptionCommand`
- Task 5.7: Property tests for checkout and upgrade commands (Properties 6, 7, 16)
- Task 10.1: Wire the command to the `BillingController` endpoint

## Git commit

```bash
git add -A && git commit -m "feat(billing): create space checkout command and validator"
```
