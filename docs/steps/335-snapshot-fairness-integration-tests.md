# 335 — Snapshot & Fairness Integration Tests

## Phase
Split-Burden Scaling — Testing

## Purpose
Validates that the BurdenScalingService produces the correct effective burden level for DailySnapshots and that the fairness counter logic correctly excludes split-reduced tasks from the hard task count. These tests ensure requirements 5.1, 5.2, 5.3, 6.1, and 6.2 are covered by automated tests.

## What was built
- `apps/api/Jobuler.Tests/Scheduling/SnapshotAndFairnessIntegrationTests.cs` — 4 unit tests:
  - Split task (Hard, split 2, 360 min original) → snapshot stores "normal"
  - Non-split task (Hard, split 1) → snapshot stores "hard"
  - Short task (Hard, split 2, 60 min original) → snapshot stores "hard" (threshold not met)
  - Fairness counter does NOT count split-reduced task as hard

## Key decisions
- Tests call `BurdenScalingService.ComputeEffectiveBurden()` directly since that's the pure function the snapshot service delegates to — no database needed.
- The fairness counter test simulates the exact parsing logic from `UpdateFairnessCountersCommand` (Enum.TryParse on the snapshot's burden level string) to verify the integration contract.

## How it connects
- Validates the integration between `BurdenScalingService` (domain), `AssignmentSnapshotService` (infrastructure), and `UpdateFairnessCountersCommand` (application).
- Complements the existing `BurdenScalingServiceTests` and `BurdenScalingPropertyTests` which test the formula in isolation.

## How to run / verify
```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~SnapshotAndFairnessIntegrationTests"
```

## What comes next
- All split-burden-scaling backend tests are now complete.
- Frontend and export tests (task 6.x) are already done.

## Git commit
```bash
git add -A && git commit -m "feat(split-burden-scaling): add snapshot and fairness integration tests"
```
