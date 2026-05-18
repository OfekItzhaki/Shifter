# 324 — Burden Scaling Service

## Phase

Feature — Split-Burden Scaling

## Purpose

Introduces a pure, stateless service that computes the effective burden level for a task assignment based on how many sub-shifts the task is split into. When a long-duration task (≥ 4 hours original) is split into shorter segments, each segment is less burdensome — this service reflects that by reducing the tracked burden tier by one level per split applied, floored at `Easy`.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Tasks/BurdenScalingService.cs` | Static class with `ComputeEffectiveBurden` method implementing the burden reduction formula |

## Key decisions

- **Static class** — no DI needed because this is a pure function with zero dependencies. Trivially testable.
- **240-minute threshold** — only tasks whose original duration (`shiftDurationMinutes × splitCount`) ≥ 240 minutes are eligible for reduction. Short tasks keep their original burden.
- **Floor at Easy (0)** — the result can never go below `TaskBurdenLevel.Easy` regardless of split count.
- **Lives in Domain layer** — operates purely on domain concepts (`TaskBurdenLevel` enum), no infrastructure dependencies.

## How it connects

- Will be called by `AssignmentSnapshotService` (Infrastructure) to compute effective burden when creating DailySnapshots after solver runs.
- Will be called by API response mapping to expose `EffectiveBurdenLevel` in task DTOs.
- The solver (`SolverPayloadNormalizer`) does NOT use this service — it continues to send the original burden level.

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build
```

Build should succeed with no errors or warnings.

## What comes next

- Task 1.3: Update `GroupTask` domain entity with `SplitCount` property
- Task 1.5: Property-based tests for the burden scaling formula
- Task 1.6: Unit tests for `BurdenScalingService`

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): add BurdenScalingService pure computation"
```
