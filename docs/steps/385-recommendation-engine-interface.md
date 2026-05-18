# 385 — Recommendation Engine Interface

## Phase
Double-Shift Recommendation — Application Layer

## Purpose
Defines the `IRecommendationEngine` contract and its result types (`RecommendationResult`, `RecommendationItem`) in the Application layer. This interface decouples the recommendation analysis logic (implemented in Infrastructure) from the `SolverWorkerService` that invokes it.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/IRecommendationEngine.cs` | Interface with `AnalyzeAsync` method + `RecommendationResult` and `RecommendationItem` records |

## Key decisions
- Placed records in the same file as the interface (consistent with the project's lightweight DTO style)
- Used `SolverInputDto` and `SolverOutputDto` from `Jobuler.Application.Scheduling.Models` as parameters — keeps the interface in the Application layer without Infrastructure dependencies
- Used C# records for immutable result types
- `CancellationToken` defaults to `default` following existing interface conventions in the project

## How it connects
- `SolverWorkerService` (Infrastructure) will call `IRecommendationEngine.AnalyzeAsync` after each solver run
- `RecommendationEngine` (Infrastructure) will implement this interface
- The result types feed into `DoubleShiftRecommendation` entity creation in the persistence layer

## How to run / verify
```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj
```

## What comes next
- Task 3.2: Query DTOs for recommendation responses
- Task 5.1: `RecommendationEngine` implementation in Infrastructure

## Git commit
```bash
git add -A && git commit -m "feat(scheduling): add IRecommendationEngine interface and result records"
```
