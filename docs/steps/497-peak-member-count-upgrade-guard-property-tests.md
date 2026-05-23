# Step 497 — Peak Member Count & Upgrade Guard Property Tests

## Phase

Space-Level Billing — Domain Property Tests

## Purpose

Validates two correctness properties from the space-billing design document using FsCheck property-based tests:
- **Property 15**: PeakMemberCount always equals the maximum member count observed during a billing period, and resets to zero when the period changes.
- **Property 16**: UpdateTier (upgrade) is rejected with InvalidOperationException when the subscription status is not Active or Trialing.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Billing/SpaceSubscriptionPeakAndUpgradePropertyTests.cs` | FsCheck property tests for Properties 15 and 16 (5 test methods, 100 iterations each) |

## Key decisions

- Tests operate directly on the `SpaceSubscription` domain entity without mocks, keeping them simple and fast.
- Property 15 uses a generator producing random sequences of 1–50 member counts (range 1–500) to verify the max-tracking invariant.
- Property 15 also verifies that after `ResetPeakForNewPeriod()`, a new sequence of counts tracks a fresh maximum independent of the first period.
- Property 16 tests both the rejection case (Canceled/Expired statuses throw) and the success case (Active/Trialing statuses allow tier change).
- PastDue status is included in the generator for completeness but mapped to Canceled in practice since PastDue is not directly reachable via SpaceSubscription domain methods.

## How it connects

- Validates Requirements 10.2 (upgrade guard), 10.4 (peak member count tracking), 10.5 (peak reset on period change).
- Exercises `SpaceSubscription.UpdatePeakMemberCount()`, `ResetPeakForNewPeriod()`, and `UpdateTier()` from task 1.1.
- Complements the existing `SubscriptionLifecyclePropertyTests` and `CheckoutRejectionPropertyTests` in the Billing test folder.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~SpaceSubscriptionPeakAndUpgradePropertyTests"
```

All 5 property tests should pass (100 iterations each).

## What comes next

- Task 3.5: Property test for statistics period rotation (Property 13)
- Task 5.7: Property tests for checkout and upgrade commands (Properties 6, 7, 16 at application layer)

## Git commit

```bash
git add -A && git commit -m "feat(billing): property tests for peak member count and upgrade guard (Properties 15, 16)"
```
