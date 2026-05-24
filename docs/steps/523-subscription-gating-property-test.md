# 523 — Subscription Gating Property Test

## Phase

Schedule Regeneration — Application Layer Property Tests

## Purpose

Validates Property 9 from the schedule-regeneration design: subscription gating ensures that groups with expired trials or inactive subscriptions cannot trigger schedule regeneration (402 Payment Required), while groups with active subscriptions or within their trial period can proceed normally.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Application/SubscriptionGatingPropertyTests.cs` | FsCheck property-based test with 100+ iterations covering all subscription states |

## Key decisions

- **Scenario enum approach**: Created a `SubscriptionScenario` enum to represent the five distinct subscription states (Active, WithinTrial, ExpiredTrial, Canceled, Expired) for clear generator composition.
- **Split into two properties**: Property 9a tests rejection (expired/canceled/expired-subscription → 402), Property 9b tests allowance (active/within-trial → run created). This maps directly to Requirements 10.2 and 10.3.
- **Randomized trial offsets**: The `daysOffset` parameter randomizes how far in the past/future the trial end date is, ensuring the property holds across varying time distances.
- **No mocking of subscription logic**: Tests use real `GroupSubscription` domain entities with their actual `IsActive` property, validating the handler's integration with the domain model.

## How it connects

- Tests the subscription check in `TriggerRegenerationCommandHandler` (step 516)
- Uses the `GroupSubscription` domain entity's `IsActive` property (from the billing domain)
- Validates Requirements 10.2 (reject expired) and 10.3 (allow active/trial)
- Follows the same test patterns as `StaleRunTimeoutRecoveryPropertyTests` (step 522)

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~SubscriptionGatingPropertyTests"
```

Expected: 2 tests pass (100 iterations each for the property tests).

## What comes next

- Task 4.3: Concurrent regeneration rejection property test
- Task 5.2: Permission enforcement property test
- Task 7.3: Failed regeneration error recording property test

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): subscription gating property test (Property 9)"
```
