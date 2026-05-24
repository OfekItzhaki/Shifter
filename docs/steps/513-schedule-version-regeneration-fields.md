# 513 — Schedule Version Regeneration Fields

## Phase

Schedule Regeneration — Domain Layer

## Purpose

Extends the `ScheduleVersion` domain entity with two new fields (`SupersedesVersionId` and `SourceType`) and a `CreateRegenerationDraft` factory method. These additions enable the regeneration workflow to create draft versions that track which published version they intend to replace and identify themselves as regeneration-sourced.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Scheduling/ScheduleVersion.cs` | Added `SupersedesVersionId` (nullable Guid, private setter), `SourceType` (nullable string, private setter), and `CreateRegenerationDraft` static factory method |
| `apps/api/Jobuler.Tests/Domain/ScheduleVersionTests.cs` | Added 3 unit tests verifying the factory method sets all fields correctly, handles optional `summaryJson`, and does not set unrelated fields |

## Key decisions

- **Private setters** — Consistent with existing entity pattern; fields are only set via factory methods, preserving immutability after creation.
- **`SourceType` as string** — Matches the design doc specification. Allows future extensibility without enum migrations (values: "standard", "emergency", "rollback", "regeneration").
- **Factory method pattern** — Follows the existing `CreateDraft` and `CreateRollback` pattern: static method returning a new instance with all required fields set.
- **No external dependencies** — Domain layer remains dependency-free per architecture rules.

## How it connects

- The EF migration (task 2.1) will map these new properties to database columns.
- The background worker (task 7.1) will call `CreateRegenerationDraft` when the solver completes successfully.
- The `SupersedesVersionId` enables the publish flow (task 9.1) to include audit metadata about which version was replaced.

## How to run / verify

```bash
cd apps/api
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~ScheduleVersionTests"
```

All 7 tests should pass (4 existing + 3 new).

## What comes next

- Task 2.1: EF migration to add `supersedes_version_id` and `source_type` columns to the `schedule_versions` table.

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): add SupersedesVersionId and SourceType to ScheduleVersion"
```
