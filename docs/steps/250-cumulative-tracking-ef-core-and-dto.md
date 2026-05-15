# 250 — Cumulative Tracking EF Core Configurations and Solver DTO

## Phase

Phase 3 — Application Layer (Cumulative Tracking and Periods)

## Purpose

Wire up the new domain entities (SubscriptionPeriod, CumulativeRecord, DailySnapshot) to EF Core so they can be persisted to PostgreSQL. Also define the `CumulativeTrackingDto` that carries per-person cumulative data into the solver payload.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Infrastructure/Persistence/Configurations/SubscriptionPeriodConfiguration.cs` | Maps `SubscriptionPeriod` to `subscription_periods` table with snake_case columns, partial index on active status |
| `Jobuler.Infrastructure/Persistence/Configurations/CumulativeRecordConfiguration.cs` | Maps `CumulativeRecord` to `cumulative_records` table with all 25+ counter columns, unique constraint, lookup indexes |
| `Jobuler.Infrastructure/Persistence/Configurations/DailySnapshotConfiguration.cs` | Maps `DailySnapshot` to `daily_snapshots` table with DATE column type for SnapshotDate, unique constraint, range indexes |
| `Jobuler.Application/Persistence/AppDbContext.cs` | Added DbSet properties for SubscriptionPeriod, CumulativeRecord, DailySnapshot |
| `Jobuler.Application/Scheduling/Models/CumulativeTrackingDto.cs` | Record with JSON property names matching Python solver expectations |
| `Jobuler.Application/Scheduling/Models/SolverInputDto.cs` | Added optional `CumulativeTracking` list parameter to SolverInputDto |

## Key decisions

- Followed existing project pattern: no explicit `HasOne`/`HasForeignKey` navigation properties — just column mappings and indexes
- `CumulativeRecord` ignores `CreatedAt` from base Entity (table uses `updated_at` instead)
- `SnapshotDate` mapped as `date` column type for PostgreSQL DATE storage
- `CumulativeTrackingDto` uses `JsonPropertyName` attributes to match Python snake_case field names
- `CumulativeTracking` added as optional parameter (default null) at the end of `SolverInputDto` to maintain backward compatibility

## How it connects

- Configurations are auto-discovered by `AppDbContext.OnModelCreating` via `ApplyConfigurationsFromAssembly`
- `CumulativeTrackingDto` will be used by `ICumulativeTracker.GetForSolverPayloadAsync` (task 8.5)
- `SolverInputDto.CumulativeTracking` will be populated by `SolverPayloadNormalizer` (task 12.1)
- Domain entities were created in step 249

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build should succeed with no errors on the new files.

## What comes next

- Task 5: Backfill scripts to populate initial data
- Task 7–8: Application layer services (AssignmentSnapshotService, CumulativeTracker)
- Task 12: Wire CumulativeTracking into SolverPayloadNormalizer

## Git commit

```bash
git add -A && git commit -m "feat(cumulative): EF Core configurations and CumulativeTrackingDto for solver payload"
```
