# 336 — Split Count Persistence Property Test (FsCheck)

## Phase

Split-Burden Scaling — Property-Based Testing

## Purpose

Validates that the `GroupTask` domain entity correctly persists `SplitCount` and `ShiftDurationMinutes` values through both `Create` and `Update` operations, for any valid input combination. This is Property 2 from the split-burden-scaling design document.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Domain/SplitCountPersistencePropertyTests.cs` | FsCheck property-based test class with two properties: Create round-trip and Update round-trip |

## Key decisions

- Used FsCheck `[Property]` attribute with `MaxTest = 100` for sufficient coverage
- Custom `Arbitrary` generates `splitCount ∈ [1, 10]` and `shiftDurationMinutes ∈ [1, 1440]` as specified in the design
- Tests are pure domain-level (no database, no mocking) — exercises the entity factory and update methods directly
- Two sub-properties: one for `Create` and one for `Update` to cover both persistence paths

## How it connects

- Validates Requirements 1.1 (persist SplitCount on create) and 1.3 (persist updated SplitCount)
- Depends on the `GroupTask` entity having `SplitCount` property (task 1.3)
- Complements the unit tests in `BurdenScalingServiceTests.cs` which test the formula

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~SplitCountPersistencePropertyTests"
```

Both properties should pass with 100 random inputs each.

## What comes next

- Task 4.3: Property test for solver payload preserving original burden
- Task 4.4: Unit tests for snapshot and fairness integration

## Git commit

```bash
git add -A && git commit -m "feat(split-burden-scaling): add FsCheck property test for split count persistence round-trip"
```
