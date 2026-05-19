# 440 — Checkout Rejection Property Test

## Phase

LemonSqueezy Billing Integration — Property-Based Testing

## Purpose

Validates Property 19 from the billing design: for ANY group that already has an active or trialing subscription, the checkout command is rejected with an error. This ensures the system never allows double-billing a group.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Billing/CheckoutRejectionPropertyTests.cs` | FsCheck property tests verifying checkout rejection for active/trialing subscriptions and acceptance for non-blocking statuses |

## Key decisions

- Used in-memory EF Core database per test iteration (unique DB name per run) to avoid state leakage between property test iterations
- Tested both the positive case (rejection for Active/Trialing) and the complementary negative case (success for PastDue/Canceled/Expired)
- Used reflection to set entity IDs for deterministic test setup, matching existing test patterns in the project
- Mocked `IPermissionService` to always allow (isolating the subscription status check)
- Mocked `ILemonSqueezyClient` to return a dummy URL (isolating from external API)

## How it connects

- Validates the guard clause in `CreateCheckoutCommandHandler` (task 4.1)
- Ensures Requirement 1.6 is satisfied across all possible subscription states
- Complements the existing `SubscriptionApplicationPropertyTests` and `SubscriptionLifecyclePropertyTests`

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~CheckoutRejectionPropertyTests"
```

## What comes next

- Task 5.6: Property tests for subscription event handlers
- Task 7.4: Property tests for webhook signature verification and idempotency

## Git commit

```bash
git add -A && git commit -m "feat(billing): property test for checkout rejection on active/trialing subscriptions"
```
