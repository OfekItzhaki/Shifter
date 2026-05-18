# 326 — Split Count EF Core Column Mapping

## Phase

Feature — Split-Burden Scaling

## Purpose

Maps the `SplitCount` property on the `GroupTask` domain entity to the `split_count` PostgreSQL column via EF Core Fluent API configuration. Without this mapping, EF Core would not know how to persist or read the `SplitCount` value from the database.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/TasksConfiguration.cs` | Added `builder.Property(t => t.SplitCount).HasColumnName("split_count");` to `GroupTaskConfiguration` |

## Key decisions

- Placed the mapping immediately after `ShiftDurationMinutes` since `SplitCount` is semantically related (both describe the task's time structure).
- No value conversion needed — `SplitCount` is a plain `int` property mapping to an `INTEGER` column.
- No `.IsRequired()` needed — the column already has `NOT NULL DEFAULT 1` from the migration (task 1.1).

## How it connects

- **Depends on**: Task 1.1 (migration adding `split_count` column) and Task 1.3 (domain entity `SplitCount` property).
- **Enables**: EF Core can now persist and query `SplitCount` for GroupTask entities, which is required by the API layer (task 3.x) and snapshot service (task 4.x).

## How to run / verify

```bash
cd apps/api
dotnet build
```

The project should compile without errors. The mapping will be exercised when the API creates or reads GroupTask entities with a `SplitCount` value.

## What comes next

- Task 1.5: Property test for burden scaling formula correctness
- Task 1.6: Unit tests for `BurdenScalingService`
- Task 3.x: API layer updates to accept and return `SplitCount`

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): map SplitCount to split_count column in EF Core configuration"
```
