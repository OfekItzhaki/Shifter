# Step 335 — Burden Scaling Property Tests (FsCheck)

## Phase

Split-Burden Scaling — Domain Layer Verification

## Purpose

Adds FsCheck-based property tests for `BurdenScalingService.ComputeEffectiveBurden` to verify the burden scaling formula holds across all valid input combinations. Property-based testing provides stronger guarantees than example-based tests by exercising the function with hundreds of random inputs.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Domain/BurdenScalingPropertyTests.cs` | FsCheck property tests covering 4 sub-properties of the burden scaling formula |
| `apps/api/Jobuler.Tests/Jobuler.Tests.csproj` | Added `FsCheck` and `FsCheck.Xunit` package references |

## Key decisions

- **FsCheck over manual loops** — the design doc specifies FsCheck for property-based testing. This provides shrinking on failure and better coverage than hand-rolled random loops.
- **Custom generators** — constrained `splitCount ∈ [1, 10]` and `shiftDurationMinutes ∈ [1, 1440]` to match the domain's valid input space.
- **4 separate properties** — each sub-property is a distinct `[Property]` test for clear failure reporting: identity at splitCount=1, identity below threshold, formula correctness above threshold, and floor invariant.

## How it connects

- Tests the `BurdenScalingService` created in step 324
- Validates Requirements 2.1, 3.1, 3.5 from the split-burden-scaling spec
- FsCheck package is now available for future property tests (tasks 3.4, 4.3)

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~BurdenScalingPropertyTests"
```

All 4 property tests should pass (200 iterations each).

## What comes next

- Task 1.6: Unit tests for `BurdenScalingService` with specific examples
- Task 3.4: Property test for split count persistence round-trip

## Git commit

```bash
git add -A && git commit -m "feat(split-burden-scaling): add FsCheck property tests for BurdenScalingService"
```
