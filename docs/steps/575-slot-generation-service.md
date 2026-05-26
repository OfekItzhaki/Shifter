# 575 — Slot Generation Service

## Phase

Self-Service Scheduling — Application Layer (Task 5.3)

## Purpose

Implements the `SlotGenerationService` that generates shift slots from shift templates for a scheduling cycle. This is the core pipeline that turns recurring weekly template definitions into concrete, bookable time slots for each date in a cycle.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/SlotGenerationService.cs` | Service implementing `ISlotGenerationService` — generates one `ShiftSlot` per non-deleted template with an active `GroupTask` for each matching date in the cycle |

## Key decisions

- **Placed in Application layer**: The service uses `AppDbContext` directly (consistent with other command handlers in this project) rather than going through a repository abstraction in Infrastructure.
- **Idempotent by design**: Loads existing slot keys (template+date) into a HashSet before generation, skipping any that already exist. This allows the background job to safely retry without creating duplicates.
- **Exclusive end date**: The cycle date range uses `StartsAt` (inclusive) to `EndsAt` (exclusive) — a slot is generated for dates where `cycleStartDate <= date < cycleEndDate`.
- **Batch save**: All slots are added to the context in a single loop, then persisted with one `SaveChangesAsync` call for efficiency.
- **Warning logging**: Logs at `Warning` level when no active templates exist or when a template references an inactive/missing GroupTask, per requirements 3.5 and 3.6.

## How it connects

- **Consumed by**: `GenerateCycleSlotsJob` (Hangfire background job, task 16.1) calls this service daily.
- **Depends on**: `ShiftTemplate` entities (task 1.4), `SchedulingCycle` entity (task 1.3), `ShiftSlot` entity (task 1.5), `GroupTask` entity (existing).
- **Interface**: Implements `ISlotGenerationService` defined in task 1.9.
- **Validated by**: Property tests 5, 6, 7 (task 5.4) — correct count, inherited properties, idempotency.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~SelfService"
```

## What comes next

- Task 5.4: Property tests for slot generation (Properties 5, 6, 7)
- Task 16.1: `GenerateCycleSlotsJob` background job that invokes this service
- Task 18.1: DI registration of `SlotGenerationService`

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement SlotGenerationService for cycle slot generation"
```
