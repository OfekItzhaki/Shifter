# 211 — Unavailability Reason EF Core Configuration & Migration

## Phase

Qualification Templates & Unavailability Reasons — Database & Domain Layer

## Purpose

Provides the EF Core mapping and SQL migration for the new `unavailability_reasons` table and the optional FK from `presence_windows` to it. This bridges the domain entity (task 1.1) to the database, enabling all subsequent CRUD and query operations.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/UnavailabilityReasonConfiguration.cs` | EF Core configuration mapping `UnavailabilityReason` to `unavailability_reasons` table with snake_case columns and composite index on `(SpaceId, IsActive)` |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/PeopleConfiguration.cs` | Updated `PresenceWindowConfiguration` to map `UnavailabilityReasonId` column and define the optional FK with `OnDelete(DeleteBehavior.SetNull)` |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Added `DbSet<UnavailabilityReason> UnavailabilityReasons` in the Spaces section |
| `infra/migrations/043_unavailability_reasons.sql` | SQL migration: creates table with RLS policy, partial index, updated-at trigger, and adds the FK column to `presence_windows` |

## Key decisions

- **Separate configuration file**: `UnavailabilityReasonConfiguration.cs` follows the pattern of `HomeLeaveConfigConfiguration.cs` — one file per entity for space-scoped configs.
- **FK in PeopleConfiguration**: The FK relationship is defined in `PresenceWindowConfiguration` (where the FK column lives) rather than in the reason configuration, matching EF Core conventions.
- **SetNull on delete**: If a reason is hard-deleted, presence windows keep their data but lose the FK reference — no cascade failures.
- **Partial index**: `WHERE is_active = TRUE` on the composite index optimizes the most common query (listing active reasons for a space).
- **RLS policy**: Follows the same `current_setting('app.current_space_id', TRUE)::UUID` pattern as all other tenant-scoped tables.

## How it connects

- Depends on: Task 1.1 (domain entity) and Task 1.2 (PresenceWindow FK property)
- Enables: All Application layer commands/queries (tasks 2.1–2.6) that read/write `UnavailabilityReason` records
- The migration must be applied before any API endpoints can function

## How to run / verify

```bash
# Build the solution
cd apps/api && dotnet build

# Apply the migration (requires running PostgreSQL)
psql -f infra/migrations/043_unavailability_reasons.sql
```

## What comes next

- Application layer commands and queries for CRUD operations (tasks 2.1–2.5)
- Extending `AddPresenceWindowCommand` to validate and persist the reason FK (task 2.6)

## Git commit

```bash
git add -A && git commit -m "feat(qualification-templates): EF Core config and migration for unavailability_reasons"
```
