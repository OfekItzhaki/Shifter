# 386 — Double-Shift Recommendation EF Core Configuration

## Phase

Feature: Double-Shift Recommendation — Infrastructure Layer

## Purpose

Configures the EF Core entity mapping for `DoubleShiftRecommendation`, mapping it to the `double_shift_recommendations` PostgreSQL table with proper column names, enum-to-string conversion for `Status`, and composite indexes for efficient querying. Also registers the `DbSet` on `AppDbContext`.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/DoubleShiftRecommendationConfiguration.cs` | EF Core `IEntityTypeConfiguration<DoubleShiftRecommendation>` — maps all columns to snake_case, configures `Status` as string with max length 20, adds 4 composite indexes |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Added `DbSet<DoubleShiftRecommendation> DoubleShiftRecommendations` in the Scheduling section |

## Key decisions

- Used `HasConversion<string>()` for the `Status` enum (same pattern as `GroupAlert.Severity`) — stores enum values as their string names in the DB
- Added 4 indexes matching the design doc: `ix_dsr_space_group_status`, `ix_dsr_space_run`, `ix_dsr_space_task_status`, `ix_dsr_created_at`
- `TaskName` mapped with `HasMaxLength(200)` matching the DB schema design
- Followed existing configuration style (explicit `HasColumnName` for every property)

## How it connects

- Depends on: `DoubleShiftRecommendation` domain entity (step 384)
- Used by: EF Core migration (task 2.2), recommendation engine persistence (task 5.2), all query handlers (tasks 8.1–8.3)
- The configuration is auto-discovered via `ApplyConfigurationsFromAssembly` in `AppDbContext.OnModelCreating`

## How to run / verify

```bash
cd apps/api && dotnet build --no-restore
```

All 5 projects (Domain, Application, Infrastructure, Api, Tests) should compile without errors.

## What comes next

- Task 2.2: Generate EF Core migration with all columns, constraints, indexes, and RLS policy

## Git commit

```bash
git add -A && git commit -m "feat(double-shift): EF Core configuration and DbSet for DoubleShiftRecommendation"
```
