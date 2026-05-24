# 513 — ScheduleRun Regeneration Enum and Fields

## Phase

Feature: Schedule Regeneration (Domain Layer)

## Purpose

Extends the `ScheduleRun` domain entity to support the regeneration workflow. The solver needs a distinct trigger mode to differentiate regeneration runs from standard/emergency runs, and the entity needs fields to track which group the regeneration targets and which draft version was produced.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Scheduling/ScheduleRun.cs` | Added `Regeneration` value to `ScheduleRunTrigger` enum |
| `apps/api/Jobuler.Domain/Scheduling/ScheduleRun.cs` | Added `GroupId` (nullable Guid) property with private setter |
| `apps/api/Jobuler.Domain/Scheduling/ScheduleRun.cs` | Added `ResultVersionId` (nullable Guid) property with private setter |
| `apps/api/Jobuler.Domain/Scheduling/ScheduleRun.cs` | Added `SetResultVersion(Guid versionId)` public method |
| `apps/api/Jobuler.Domain/Scheduling/ScheduleRun.cs` | Updated `Create` factory to accept optional `Guid? groupId` parameter |

## Key decisions

- **Optional `groupId` parameter**: The `Create` factory uses `Guid? groupId = null` so existing callers (standard/emergency/manual/rollback runs) are unaffected. Only regeneration runs pass a group ID.
- **Private setters**: Both new properties follow the existing pattern of private setters to maintain encapsulation. `GroupId` is set only at creation time; `ResultVersionId` is set via the explicit `SetResultVersion` method.
- **Public `SetResultVersion`**: This method is called by the background worker after the solver successfully produces a draft version. It's intentionally public (not internal) because the worker lives in the Infrastructure layer.
- **No external dependencies**: The Domain layer remains dependency-free — no EF Core, no MediatR, no HTTP references.

## How it connects

- **Concurrency guard (Requirement 9.1)**: The `GroupId` field enables querying for in-progress regeneration runs per group.
- **Result tracking (Requirement 8.3)**: The `ResultVersionId` field allows the polling endpoint to return the draft version ID on completion.
- **Worker integration (Requirement 2.3)**: `SetResultVersion` is called after the solver creates the draft version.
- **EF migration (Task 2.1)**: The new fields will be mapped to `group_id` and `result_version_id` columns in the next task.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain/Jobuler.Domain.csproj
dotnet build Jobuler.sln
```

Both should succeed with 0 errors.

## What comes next

- Task 1.2: Add `SupersedesVersionId` and `SourceType` fields to `ScheduleVersion`
- Task 2.1: EF migration to map the new columns to PostgreSQL

## Git commit

```bash
git add -A && git commit -m "feat(scheduling): add Regeneration trigger and tracking fields to ScheduleRun"
```
