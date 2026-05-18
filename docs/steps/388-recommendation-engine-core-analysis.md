# 388 — Recommendation Engine Core Analysis Logic

## Phase

Feature: Double-Shift Recommendation (Spec Task 5.1)

## Purpose

Implements the core analysis logic of the `RecommendationEngine` that detects staffing shortfalls and produces ranked double-shift recommendations. This is the brain of the recommendation system — it takes solver input/output and determines which tasks would benefit from enabling double shifts.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Scheduling/RecommendationEngine.cs` | Full implementation of `IRecommendationEngine.AnalyzeAsync` with 5-step analysis pipeline |

## Key decisions

1. **Pure analysis only** — This task implements only the analysis logic. Persistence (inserting `DoubleShiftRecommendation` rows) is handled in task 5.2.
2. **Shortfall detection via home leave overlap** — Available personnel per day = total members − distinct people on home leave that day. A day is flagged if available < `MinPeopleAtBase`.
3. **Consecutive pair simulation** — Double-shift coverage is calculated by counting pairs of adjacent uncovered slots (where one ends exactly when the next begins). Each pair represents one additional slot that could be covered by a single person doing both shifts.
4. **Emergency freeze guard** — If `EmergencyFreezeActive` is true, the engine returns immediately with no recommendations (Req 6.3).
5. **Minimum 2 candidates precondition** — If fewer than 2 active tasks have `AllowsDoubleShift == false`, no recommendations are generated (Req 6.4).
6. **Internal static methods** — `DetectShortfall` and `SimulateDoubleShiftCoverage` are `internal static` for testability without mocking.

## How it connects

- Implements `IRecommendationEngine` interface defined in `Jobuler.Application/Scheduling/`
- Uses `SolverInputDto` and `SolverOutputDto` from the solver pipeline
- Reads `HomeLeaveConfig` and `GroupTask` entities from `AppDbContext`
- Will be called by `SolverWorkerService` after solver completion (task 5.3)
- Results will be persisted as `DoubleShiftRecommendation` entities (task 5.2)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
```

Build succeeds with 0 warnings, 0 errors.

## What comes next

- Task 5.2: Recommendation persistence and lifecycle management
- Task 5.3: Integration into `SolverWorkerService`
- Tasks 5.4–5.10: Property-based tests for the engine logic

## Git commit

```bash
git add -A && git commit -m "feat(double-shift): implement RecommendationEngine core analysis logic"
```
