# Step 498 — Statistics Period Rotation Property Tests

## Phase

Phase: Space-Level Billing — Property-Based Testing

## Purpose

Validates Property 13 from the space-billing design: lifecycle events rotate statistics periods correctly. Each subscription lifecycle event (trial start, trial expiry, activation, expiry, period renewal) must close active periods and/or open new periods for all groups in the space.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Billing/StatisticsPeriodRotationPropertyTests.cs` | FsCheck property tests (5 properties, 100 iterations each) verifying all lifecycle methods of `StatisticsPeriodService` |

## Key decisions

- Used EF Core InMemory provider to test the real `StatisticsPeriodService` against a database without mocking
- Split Property 13 into 5 sub-properties (13a–13e), one per lifecycle method, for clear failure isolation
- Generated random group counts (1–10) and boundary dates to cover diverse scenarios
- Verified both the closing of old periods (status, EndsAt) and opening of new periods (status, StartsAt, per-group uniqueness)

## How it connects

- Tests the `StatisticsPeriodService` implemented in step 484
- Validates requirements 7.1–7.5 (statistics period boundaries)
- Uses the same `SubscriptionPeriod.Create()` and `Group.Create()` domain methods as production code

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~StatisticsPeriodRotationPropertyTests"
```

All 5 property tests should pass (100 iterations each).

## What comes next

- Task 5.7: Property tests for checkout and upgrade commands (Properties 6, 7, 16)
- Task 6.5: Property tests for webhook handling (Properties 2, 8)

## Git commit

```bash
git add -A && git commit -m "feat(billing): property tests for statistics period rotation (Property 13)"
```
