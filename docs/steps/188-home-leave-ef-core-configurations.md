# 188 — Home-Leave EF Core Configurations

## Phase

Home-Leave Scheduling — Database & Domain Layer

## Purpose

Add Entity Framework Core Fluent API configurations for the new `HomeLeaveConfig` and `HomeLeaveTemplate` domain entities, register their DbSets in `AppDbContext`, and map the `IsClosedBase` property on `Group` to the `is_closed_base` column.

## What was built

| File | Description |
|------|-------------|
| `Infrastructure/Persistence/Configurations/HomeLeaveConfigConfiguration.cs` | Fluent API mapping for `HomeLeaveConfig` → `home_leave_configs` table with unique index on `group_id` |
| `Infrastructure/Persistence/Configurations/HomeLeaveTemplateConfiguration.cs` | Fluent API mapping for `HomeLeaveTemplate` → `home_leave_templates` table with composite unique index on `(space_id, name)` |
| `Infrastructure/Persistence/Configurations/GroupsConfiguration.cs` | Added `IsClosedBase` → `is_closed_base` column mapping with default `false` |
| `Application/Persistence/AppDbContext.cs` | Added `HomeLeaveConfigs` and `HomeLeaveTemplates` DbSets |

## Key decisions

- Followed existing project patterns: snake_case column names, `IEntityTypeConfiguration<T>` per entity, configurations auto-discovered via `ApplyConfigurationsFromAssembly`.
- Unique constraint on `group_id` in `HomeLeaveConfigConfiguration` enforces the one-to-one relationship between groups and their home-leave config (Requirement 12.7).
- Composite unique index on `(space_id, name)` in `HomeLeaveTemplateConfiguration` prevents duplicate template names within a space (Requirement 12.3).
- `IsClosedBase` placed after `AutoPublish` in the `GroupConfiguration` to maintain logical ordering of group settings.

## How it connects

- These configurations are auto-discovered by EF Core via `modelBuilder.ApplyConfigurationsFromAssembly(ConfigurationAssembly)` in `AppDbContext.OnModelCreating`.
- The DbSets enable Application-layer handlers to query and persist `HomeLeaveConfig` and `HomeLeaveTemplate` entities.
- The migration `042_home_leave.sql` (created in task 1.1) defines the actual database schema; these configurations tell EF Core how to map .NET entities to those tables.

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build should succeed with zero errors. The configurations will be exercised when the API starts and EF Core validates the model against the database.

## What comes next

- API backend commands/queries for home-leave config CRUD (tasks 3.1–3.3)
- API backend for template management (tasks 4.1–4.2)
- Solver payload normalizer extension to read `HomeLeaveConfig` from DB (task 5.3)

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add EF Core configurations for HomeLeaveConfig and HomeLeaveTemplate"
```
