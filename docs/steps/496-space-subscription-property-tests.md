# Step 496 — Space Subscription Property Tests (Properties 1, 9, 12)

## Phase

Space-Level Billing — Domain Layer Testing

## Purpose

Validates the core SpaceSubscription entity behavior using property-based tests (FsCheck). These tests ensure that trial date computation, cancel state transitions, and renewal logic hold true across all valid inputs — not just specific examples.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Billing/SpaceSubscriptionPropertyTests.cs` | Property-based tests covering Properties 1, 9, and 12 from the design document |

## Key decisions

- Used FsCheck with xUnit integration (already in the test project)
- Minimum 100 iterations per property test as specified in the design document
- Generators constrain inputs to valid ranges (e.g., trial days 1–365, future/past period ends)
- Tests validate the SpaceSubscription entity directly without mocks — pure domain logic testing

## How it connects

- Tests validate the `SpaceSubscription` entity created in step 482
- Properties align with the correctness properties defined in the space-billing design document
- These tests serve as regression guards for the subscription lifecycle state machine

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~SpaceSubscriptionPropertyTests"
```

All 6 property tests should pass (100 iterations each).

## What comes next

- Task 1.4: Property tests for access and expiry logic (Properties 3, 4, 10, 11)
- Task 1.5: Property tests for peak member count and upgrade guard (Properties 15, 16)

## Git commit

```bash
git add -A && git commit -m "feat(billing): space subscription property tests (Properties 1, 9, 12)"
```
