# Step 501 — Migration Correctness Property Tests

## Phase

Space-Level Billing — Property-Based Testing

## Purpose

Validates Property 14 from the space-billing design document: migration from group-level to space-level billing creates correct SpaceSubscriptions. This ensures the one-time migration command correctly handles all scenarios: marking GroupSubscriptions as migrated, creating Active SpaceSubscriptions for spaces with active/trialing group subs, creating Trialing SpaceSubscriptions for spaces without active group subs, and skipping spaces that already have SpaceSubscriptions.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Billing/MigrationCorrectnessPropertyTests.cs` | FsCheck property tests (4 properties, 100 iterations each) validating migration correctness |

## Key decisions

- Used `ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))` to allow the in-memory database to work with the migration handler's transaction-based batch processing
- Split Property 14 into four sub-properties (14a–14d) for clear separation of concerns: marking as migrated, active migration, trialing migration, and skip logic
- Used FsCheck generators to create random sets of GroupSubscriptions with varying statuses (Active, Trialing, Canceled, Expired) and period dates
- Mocked `ITrialDurationCache` with NSubstitute to return a fixed 14-day trial duration

## How it connects

- Tests the `MigrateToSpaceBillingCommand` handler implemented in step 491
- Validates Requirements 8.1, 8.2, 8.3, 8.4 from the space-billing spec
- Uses the same in-memory database pattern as other billing property tests (steps 496–500)

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~MigrationCorrectnessPropertyTests"
```

All 4 property tests should pass (100 iterations each).

## What comes next

- Task 10.3: Webhook signature rejection property test (Property 17)
- Frontend property tests for days remaining and color computation (Property 5)

## Git commit

```bash
git add -A && git commit -m "feat(space-billing): migration correctness property tests (Property 14)"
```
