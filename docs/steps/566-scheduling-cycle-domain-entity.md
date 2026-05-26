# 566 — SchedulingCycle Domain Entity

## Phase
Self-Service Scheduling — Domain Layer Foundation

## Purpose
Introduces the `SchedulingCycle` entity that represents a scheduling period (typically one week) for which shift slots are generated and shift requests are collected in self-service mode. This entity tracks the cycle's time boundaries and request window, enabling the system to enforce when members can submit shift requests.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Scheduling/SchedulingCycle.cs` | Domain entity with SpaceId, GroupId, StartsAt, EndsAt, RequestWindowOpensAt, RequestWindowClosesAt, IsGenerated. Implements `ITenantScoped` and extends `AuditableEntity`. Includes factory method with validation, `MarkGenerated()`, `UpdateRequestWindowClose()`, and `IsRequestWindowOpen()` helper. |

## Key decisions

- **Extends `AuditableEntity`**: Provides `Id`, `CreatedAt`, `UpdatedAt` tracking consistent with other scheduling entities.
- **Implements `ITenantScoped`**: Ensures tenant isolation via `SpaceId` as required by architecture rules.
- **Factory method validation**: The `Create` method enforces that `EndsAt > StartsAt`, `RequestWindowOpensAt < RequestWindowClosesAt`, and `RequestWindowClosesAt <= StartsAt` (window must close before cycle starts).
- **`IsGenerated` flag**: Tracks whether slot generation has run for this cycle, supporting idempotent generation.
- **`IsRequestWindowOpen` helper**: Pure domain logic for checking if the request window is currently open at a given UTC time.

## How it connects

- Referenced by `ShiftSlot` (each slot belongs to a cycle via `SchedulingCycleId`)
- Referenced by `ShiftRequest` (each request is scoped to a cycle)
- Used by `SlotGenerationService` to determine date ranges for slot generation
- Used by `ShiftRequestService` to enforce request window timing (Requirements 6.3, 6.4)
- Configured via `SelfServiceConfig` offsets that determine window open/close times

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build
```

## What comes next

- Task 1.4: `ShiftTemplate` entity
- Task 2.1: EF Core configuration for `SchedulingCycle` (table, indexes, RLS)
- Task 5.3: `SlotGenerationService` that generates slots for a cycle

## Git commit

```bash
git add -A && git commit -m "feat(self-service): add SchedulingCycle domain entity"
```
