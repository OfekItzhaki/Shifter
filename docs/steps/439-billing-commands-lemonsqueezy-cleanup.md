# 439 — Billing Commands LemonSqueezy Cleanup

## Phase

LemonSqueezy Billing Integration — Integration Wiring and Cleanup

## Purpose

Verify that all existing billing commands (`ActivateSubscriptionCommand`, `ExpireSubscriptionsCommand`, `RenewSubscriptionCommand`) use LemonSqueezy identifiers and remove any remaining Stripe-specific code or references from the codebase.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Application/Billing/Commands/ActivateSubscriptionCommand.cs` | Already uses `LemonSqueezySubscriptionId` and `LemonSqueezyCustomerId` — no changes needed |
| `Jobuler.Application/Billing/Commands/ExpireSubscriptionsCommand.cs` | No Stripe references found — no changes needed |
| `Jobuler.Application/Billing/Commands/RenewSubscriptionCommand.cs` | No Stripe references found — no changes needed |
| `docs/steps/255-period-manager-and-wiring.md` | Updated stale "Stripe webhook controller" reference to "LemonSqueezy webhook controller" |
| `docs/steps/419-subscription-expired-status-and-lifecycle-methods.md` | Updated "via Stripe" reference to "via LemonSqueezy" |

## Key decisions

- The `ActivateSubscriptionCommand` was already updated in task 1.1 to use LemonSqueezy identifiers — confirmed correct.
- `ExpireSubscriptionsCommand` and `RenewSubscriptionCommand` operate on domain-level properties (`Status`, `CurrentPeriodEnd`) and never referenced Stripe fields directly.
- Historical SQL migration files (`026_subscriptions_and_coupons.sql`) retain Stripe column names as they represent the original schema — the rename migration (`067_lemonsqueezy_billing_migration.sql`) handles the transition.
- Spec/design documents (`.kiro/specs/`) retain Stripe references for historical context and are not modified.
- Step documentation files were updated to reflect the current LemonSqueezy integration.

## How it connects

- This task confirms the billing command layer is fully migrated to LemonSqueezy identifiers.
- The webhook handlers (`HandleSubscriptionCreatedCommand`, `HandleSubscriptionUpdatedCommand`, etc.) dispatch to these commands using LemonSqueezy IDs.
- The `ExpireSubscriptionsJob` background job uses `ExpireSubscriptionsCommand` which queries by `Status` and `CurrentPeriodEnd` — provider-agnostic.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore   # Should succeed with 0 errors
```

Search for "Stripe" in `.cs` files — should return zero results:
```bash
grep -ri "stripe" --include="*.cs" .
```

## What comes next

- Task 9.2: Add LemonSqueezy configuration to appsettings
- Task 9.3: Write unit tests for status mapping and configuration validation

## Git commit

```bash
git add -A && git commit -m "chore(billing): verify billing commands use LemonSqueezy identifiers and remove stale Stripe references"
```
