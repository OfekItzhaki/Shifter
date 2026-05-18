# 384 — Double-Shift Recommendation Domain Entity

## Phase

Feature: Double-Shift Recommendation

## Purpose

Introduces the `DoubleShiftRecommendation` domain entity and `RecommendationStatus` enum. This entity represents a system-generated suggestion to enable double shifts on a specific task to address staffing shortfalls detected after a solver run. The entity encapsulates the full lifecycle (Active → Dismissed/Resolved/Cleared) with domain methods enforcing valid state transitions.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Scheduling/DoubleShiftRecommendation.cs` | New domain entity with `RecommendationStatus` enum, all required properties, a factory method `Create(...)`, and lifecycle transition methods `Dismiss(userId)`, `Resolve()`, `Clear()` |

## Key decisions

- **Enum in same file**: `RecommendationStatus` is co-located with the entity (same pattern as `ScheduleRunStatus` in `ScheduleRun.cs`)
- **Private setters**: All properties use `private set` to enforce invariants through domain methods only
- **Guard clauses on transitions**: `Dismiss`, `Resolve`, and `Clear` throw `InvalidOperationException` if the recommendation is not in `Active` status — prevents invalid state transitions
- **Factory method**: `Create(...)` static factory follows the existing pattern in `ScheduleRun.Create(...)`
- **No external dependencies**: The entity lives in the Domain layer with zero external dependencies (only `Jobuler.Domain.Common`)

## How it connects

- **Entity base class**: Inherits `Id` (Guid) and `CreatedAt` (DateTime) from `Entity`
- **Tenant scoping**: Implements `ITenantScoped` via `SpaceId` property for RLS enforcement
- **Used by**: Will be persisted via EF Core configuration (task 2.1), queried by application layer commands/queries (tasks 7.x, 8.x), and created by the `RecommendationEngine` (task 5.2)

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build --no-restore
```

Build should succeed with zero errors and zero warnings related to this file.

## What comes next

- Task 2.1: EF Core entity configuration mapping this entity to the `double_shift_recommendations` table
- Task 3.1: `IRecommendationEngine` interface that produces these entities
- Task 5.2: Persistence logic that creates and transitions these entities

## Git commit

```bash
git add -A && git commit -m "feat(double-shift-recommendation): add DoubleShiftRecommendation domain entity and RecommendationStatus enum"
```
