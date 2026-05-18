# 335 — BurdenScalingService Unit Tests

## Phase

Split-Burden Scaling — Domain layer verification

## Purpose

Provides concrete example-based unit tests for `BurdenScalingService.ComputeEffectiveBurden()` covering all burden level transitions, the duration threshold guard, and the split-count-of-1 no-op case. These complement the property-based tests by documenting specific expected behaviors as regression anchors.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Domain/BurdenScalingServiceTests.cs` | xUnit test class with 8 tests covering all specified scenarios |

## Key decisions

- Used `[Theory]` with `[InlineData]` for the split-count-1 case to cover all three burden levels in a single parameterized test.
- Used individual `[Fact]` methods for each specific burden transition to keep test names descriptive and failures easy to diagnose.
- Chose `shiftDurationMinutes` values that produce exact threshold boundaries (e.g., 120×2=240, 80×3=240, 48×5=240) to test the ≥240 condition precisely.

## How it connects

- Tests validate the `BurdenScalingService` implemented in step 324.
- Covers requirements 2.1, 2.2, 3.1, 3.2, 3.3, 3.4, 3.5 from the split-burden-scaling spec.
- Complements the FsCheck property tests (task 1.5) which validate universal properties across random inputs.

## How to run / verify

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~BurdenScalingServiceTests"
```

All 8 tests should pass.

## What comes next

- Property-based tests for burden scaling (task 1.5) provide broader coverage.
- Snapshot and fairness integration tests (task 4.4) verify end-to-end behavior.

## Git commit

```bash
git add -A && git commit -m "feat(split-burden-scaling): add BurdenScalingService unit tests"
```
