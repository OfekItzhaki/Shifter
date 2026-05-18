# 395 — Recommendation Engine DI Registration

## Phase
Feature: Double-Shift Recommendation (API Layer)

## Purpose
Register `RecommendationEngine` as the scoped implementation of `IRecommendationEngine` in the DI container so that `SolverWorkerService` and other consumers can resolve it at runtime.

## What was built
- **`apps/api/Jobuler.Api/Program.cs`** — Added `builder.Services.AddScoped<IRecommendationEngine, RecommendationEngine>()` in the "Scheduling services" section alongside other scheduling registrations.

## Key decisions
- **Scoped lifetime** — matches `AppDbContext` lifetime, ensuring the engine shares the same DB context per request/scope. This is critical because `SolverWorkerService` creates a scope per job.
- **Registered in Program.cs** — no separate DI extension method exists for Infrastructure services; all registrations live in `Program.cs` following the existing pattern.

## How it connects
- `SolverWorkerService` (hosted service) resolves `IRecommendationEngine` from a scoped service provider after each solver run completes.
- `RecommendationEngine` depends on `AppDbContext` and `ILogger<RecommendationEngine>`, both already registered as scoped/singleton.
- The `using` directives for `Jobuler.Application.Scheduling` and `Jobuler.Infrastructure.Scheduling` were already present in `Program.cs`.

## How to run / verify
```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```
Build should succeed with zero errors.

## What comes next
- Frontend API hooks and types (Task 12)
- Unit tests for the recommendation engine (Task 17)

## Git commit
```bash
git add -A && git commit -m "feat(recommendations): register IRecommendationEngine in DI container"
```
