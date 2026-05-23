# Step 499 — Days Remaining and Color Computation Property Tests

## Phase

Space-Level Billing — Frontend Property Tests

## Purpose

Validates Property 5 from the space-billing design: the `DaysRemaining` computed property on `SpaceSubscription` correctly computes `ceil((trialEndsAt - now) / 1 day)` for future trial end dates and returns 0 for past dates. Also validates the color threshold logic (sky > 7 days, amber 4–7 days, red ≤ 3 days) as a pure function.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Billing/DaysRemainingAndColorPropertyTests.cs` | FsCheck property tests (5 properties, 100 iterations each) covering DaysRemaining computation and color threshold mapping |

## Key decisions

- Used reflection to set `TrialEndsAt` on `SpaceSubscription` to control exact test dates, since the factory method uses `DateTime.UtcNow` internally.
- Allowed ±1 day tolerance in the future-date property test to account for time passing between test setup and property evaluation.
- Extracted the color computation as a pure static function mirroring the frontend `TrialBanner` logic for isolated threshold testing.
- Tested 5 sub-properties: future DaysRemaining, past DaysRemaining (zero), color thresholds, end-to-end consistency, and expired-maps-to-red.

## How it connects

- Validates Requirements 3.1, 3.4, 3.6 from the space-billing spec.
- Tests the `SpaceSubscription.DaysRemaining` domain property (backend).
- Tests the color logic that the frontend `TrialBanner` component uses for display.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~DaysRemainingAndColorPropertyTests"
```

All 5 property tests should pass (100 iterations each).

## What comes next

- Task 14.1: SpaceBillingCard component
- Task 14.3: Unit tests for SpaceBillingCard

## Git commit

```bash
git add -A && git commit -m "feat(billing): property tests for days remaining and color computation"
```
