# Step 497 — Space Subscription Access Property Tests

## Phase

Phase: Space-Level Billing — Domain Property Tests

## Purpose

Validates the access control and expiry logic of the `SpaceSubscription` entity using property-based tests (FsCheck). These tests ensure that `IsAccessGranted` and `Expire()` behave correctly across all valid input combinations for Properties 3, 4, 10, and 11 from the design document.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Billing/SpaceSubscriptionAccessPropertyTests.cs` | Property-based tests for access and expiry logic (7 tests, 100 iterations each) |

## Key decisions

- Tests use the public API of `SpaceSubscription` (factory method + state transitions) to construct subscriptions in specific states, avoiding reflection.
- Generators constrain period dates to ensure deterministic time-dependent behavior (future vs past period end).
- Property 3 is split into two tests: one for Active status, one for Trialing (non-expired).
- Property 4 is split into two tests: one for Expired status, one for Canceled with past period end.
- Property 11 is split into two tests: one verifying Expire() succeeds on canceled-past-period, one verifying it throws for non-canceled states.

## How it connects

- Validates the `IsAccessGranted` computed property and `Expire()` method on `SpaceSubscription` (task 1.1).
- Complements the lifecycle property tests from task 1.3 (`SpaceSubscriptionPropertyTests.cs`).
- Ensures requirements 2.1, 2.2, 2.3, 6.3, 6.4 hold universally.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~SpaceSubscriptionAccessPropertyTests"
```

All 7 tests should pass (100 iterations each).

## What comes next

- Task 1.5: Property tests for peak member count and upgrade guard (Properties 15, 16).

## Git commit

```bash
git add -A && git commit -m "feat(space-billing): property tests for access and expiry logic (Properties 3, 4, 10, 11)"
```
