# 567 — ShiftTemplate Domain Entity

## Phase
Self-Service Scheduling — Domain Layer Foundation

## Purpose
Introduces the `ShiftTemplate` entity that defines a recurring weekly shift pattern for slot generation. Admins create templates specifying a day of week, time window, required headcount, and associated group task. The system uses these templates to automatically generate concrete `ShiftSlot` instances for each scheduling cycle.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Scheduling/ShiftTemplate.cs` | Domain entity with SpaceId, GroupId, GroupTaskId, DayOfWeek, StartTime, EndTime, RequiredHeadcount, IsDeleted, CreatedByUserId. Extends `AuditableEntity` and implements `ITenantScoped`. Includes `Create` factory method with validation, `Update` method, and `SoftDelete` method. |

## Key decisions

- **Extends `AuditableEntity`**: Provides `Id`, `CreatedAt`, `UpdatedAt` tracking consistent with other scheduling entities.
- **Implements `ITenantScoped`**: Ensures tenant isolation via `SpaceId` as required by architecture rules.
- **`TimeOnly` for start/end times**: Uses `TimeOnly` rather than `DateTime` since templates define recurring time-of-day patterns, not specific dates.
- **Domain validation in factory method**: Enforces `StartTime < EndTime` and `RequiredHeadcount` between 1 and 999 (Requirements 2.5, 2.6).
- **Soft delete pattern**: `IsDeleted` flag preserves the entity for referential integrity with already-generated slots while excluding it from future generation.
- **`Update` method**: Allows modifying template properties with the same validation rules, supporting Requirement 2.3.

## How it connects

- Referenced by `ShiftSlot` (each slot tracks its source template via `ShiftTemplateId`)
- Used by `SlotGenerationService` to generate slots for each matching day in a cycle
- Managed via `ShiftTemplatesController` CRUD endpoints
- Belongs to a `Group` (via `GroupId`) and references a `GroupTask` (via `GroupTaskId`)

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build
```

## What comes next

- Task 1.5: `ShiftSlot` entity and `ShiftSlotStatus` enum
- Task 2.1: EF Core configuration for `ShiftTemplate` (table, indexes, constraints, RLS)
- Task 5.1: `ShiftTemplate` CRUD commands and queries in the Application layer

## Git commit

```bash
git add -A && git commit -m "feat(self-service): add ShiftTemplate domain entity"
```
