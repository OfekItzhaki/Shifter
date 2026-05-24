# 514 — Schedule Regeneration EF Configuration & Migration

## Phase

Schedule Regeneration — Infrastructure Layer

## Purpose

Adds the database columns, foreign keys, and partial index required by the schedule regeneration feature. This enables:
- Tracking which group a regeneration run targets (`group_id` on `schedule_runs`)
- Linking a run to its resulting draft version (`result_version_id` on `schedule_runs`)
- Recording which published version a regeneration draft supersedes (`supersedes_version_id` on `schedule_versions`)
- Storing the source type metadata on versions (`source_type` on `schedule_versions`)
- Efficient concurrency guard via a partial index on in-progress regeneration runs per group

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Infrastructure/Persistence/Configurations/SchedulingConfiguration.cs` | Updated `ScheduleRunConfiguration` to map `GroupId` and `ResultVersionId` columns, configure FKs to `groups` and `schedule_versions`, and add the partial index. Updated `ScheduleVersionConfiguration` to map `SupersedesVersionId` and `SourceType` columns and configure self-referencing FK. |
| `Jobuler.Application/Persistence/Migrations/20260524000204_AddScheduleRegeneration.cs` | EF Core migration adding columns, indexes, and foreign keys |
| `Jobuler.Application/Persistence/Migrations/20260524000204_AddScheduleRegeneration.Designer.cs` | Auto-generated migration designer file |
| `Jobuler.Application/Persistence/Migrations/AppDbContextModelSnapshot.cs` | Updated model snapshot |

## Key decisions

- **FK delete behavior: SetNull** — If a group or version is deleted, the FK columns are nulled rather than cascading deletes to schedule runs/versions. This preserves historical run records.
- **Partial index filter uses lowercase enum values** — The `ScheduleRunTrigger` and `ScheduleRunStatus` enums are stored as lowercase strings in PostgreSQL (via value converters), so the filter uses `'regeneration'`, `'queued'`, `'running'`.
- **Self-referencing FK on `schedule_versions`** — `supersedes_version_id` points back to the same table, enabling the audit trail of which published version a regeneration draft replaces.

## How it connects

- **Domain layer (step 513)**: The `GroupId`, `ResultVersionId`, `SupersedesVersionId`, and `SourceType` properties were added to the domain entities in the previous step.
- **Application layer (next)**: The `TriggerRegenerationCommand` handler will use the `group_id` column for the concurrency guard query and the partial index ensures this check is efficient.
- **Worker**: After solver success, the worker sets `ResultVersionId` on the run and `SupersedesVersionId` on the new draft version.

## How to run / verify

```bash
cd apps/api
dotnet build                    # Should succeed with no new errors
dotnet ef database update --project Jobuler.Application --startup-project Jobuler.Api --context AppDbContext
```

After applying the migration, verify in PostgreSQL:
```sql
\d schedule_runs           -- Should show group_id and result_version_id columns
\d schedule_versions       -- Should show supersedes_version_id and source_type columns
\di ix_schedule_runs_group_regeneration  -- Should show the partial index
```

## What comes next

- Task 4.1: `TriggerRegenerationCommand` handler that uses the concurrency guard index
- Task 5.1: API endpoint that dispatches the command
- Task 7.1: Worker changes that set `ResultVersionId` and `SupersedesVersionId` on success

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): EF configuration and migration for regeneration columns and index"
```
