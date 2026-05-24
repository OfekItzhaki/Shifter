# 519 — Publish Regeneration Audit Metadata

## Phase

Schedule Regeneration Feature

## Purpose

When a regeneration draft is published, the audit log entry must include additional metadata to maintain a complete audit trail: the superseded version ID, the regeneration run ID, and the publishing user ID. This satisfies requirements 5.1–5.4 which mandate that the publish action records the full regeneration context.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs` | Enhanced the audit log `afterJson` payload to include `supersedes_version_id`, `regeneration_run_id`, and `published_by_user_id` when the version being published has `SourceType == "regeneration"` |

## Key decisions

- **Conditional enrichment**: The audit log payload is only enriched when `SourceType == "regeneration"`. Standard publishes retain the existing minimal payload (`version_number` only) to avoid breaking existing audit log consumers.
- **Reuse existing `actorUserId`**: The `IAuditLogger.LogAsync` already receives `req.RequestingUserId` as the `actorUserId` parameter. We additionally include `published_by_user_id` in the `afterJson` for explicit traceability within the JSON payload itself, matching the spec requirement.
- **`JsonSerializer.Serialize` for regeneration payload**: Uses `System.Text.Json.JsonSerializer` (already imported) to produce a well-formed JSON object with nullable Guid fields serialized correctly.

## How it connects

- Depends on the `ScheduleVersion` entity having `SourceType`, `SupersedesVersionId`, and `SourceRunId` fields (added in task 1.2).
- The existing publish flow (archive old version, set new as Published) is unchanged — only the audit log entry is enhanced.
- Property test 10 (task 9.2) will validate that the audit log entry contains all required fields for regeneration publishes.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj
```

To verify behavior: publish a regeneration draft and inspect the audit log entry's `after_json` column — it should contain `supersedes_version_id`, `regeneration_run_id`, and `published_by_user_id`.

## What comes next

- Task 9.2: Property test for audit log completeness on regeneration publish
- Task 9.3: Property test for regeneration not blocking standard runs

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): include regeneration audit metadata in publish version command"
```
