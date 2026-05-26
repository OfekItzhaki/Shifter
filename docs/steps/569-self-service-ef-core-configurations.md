# 569 — Self-Service Scheduling EF Core Configurations

## Phase

Self-Service Scheduling — Infrastructure Layer

## Purpose

Configures EF Core Fluent API mappings for all new self-service scheduling entities, enabling the ORM to correctly persist and query these entities against the PostgreSQL database. This includes table mappings, column names, relationships, indexes, unique constraints, and check constraints as defined in the design schema.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/SelfServiceSchedulingConfiguration.cs` | Fluent API configurations for SelfServiceConfig, SchedulingCycle, ShiftTemplate, ShiftSlot, ShiftRequest, WaitlistEntry, SwapRequest |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs` | Added SchedulingMode property mapping with enum-to-string conversion |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Removed duplicate DbSet declarations (already added in task 1.9) |

## Key decisions

- **Enum storage as text**: All enums (ShiftSlotStatus, ShiftRequestStatus, WaitlistEntryStatus, SwapRequestStatus, SchedulingMode) are stored as their string representation for readability and PostgreSQL compatibility, following the existing project pattern.
- **Check constraints on ShiftTemplate**: Added DB-level constraints for day_of_week (0-6), required_headcount (1-999), and start_time < end_time to enforce data integrity at the database level.
- **Filtered indexes**: Used PostgreSQL partial indexes (e.g., `WHERE NOT is_deleted`, `WHERE status IN ('Pending', 'Approved')`) for performance on common query patterns.
- **Unique constraints**: Enforced no-duplicate-active-request per person+slot, no-duplicate-waitlist per person+slot, and one-slot-per-template+date+group at the database level.
- **Cascade deletes**: Group deletion cascades to SelfServiceConfig, SchedulingCycle, and ShiftTemplate. ShiftSlot deletion cascades to ShiftRequests and WaitlistEntries. SwapRequest uses Restrict on ShiftRequest FKs to prevent orphaned swap records.
- **RLS policies**: Will be configured in the migration (task 2.2) via raw SQL, following the existing pattern in the codebase.

## How it connects

- Depends on: Task 1.x domain entities (SelfServiceConfig, SchedulingCycle, ShiftTemplate, ShiftSlot, ShiftRequest, WaitlistEntry, SwapRequest)
- Used by: Task 2.2 (migration generation), all Application layer services that query these entities
- The configurations are auto-discovered by EF Core via `ApplyConfigurationsFromAssembly` in AppDbContext.OnModelCreating

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build should succeed with no errors. The configurations will be validated when the migration is generated in task 2.2.

## What comes next

- Task 2.2: Generate EF Core migration for the self-service scheduling schema
- Task 2.3: Implement PostgresAdvisoryLockService

## Git commit

```bash
git add -A && git commit -m "feat(self-service): add EF Core entity configurations for self-service scheduling"
```
