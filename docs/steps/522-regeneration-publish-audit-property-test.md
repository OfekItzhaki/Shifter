# 522 — Regeneration Publish Audit Log Completeness Property Test

## Phase

Schedule Regeneration Feature

## Purpose

Validates that publishing a regeneration draft always produces an audit log entry containing all required metadata: the superseded version ID, the regeneration run ID, and the publishing user ID. This ensures the audit trail is complete for any regeneration publish action, satisfying Requirement 5.3.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Scheduling/RegenerationPublishAuditPropertyTests.cs` | FsCheck property-based test (100 iterations) that generates random (userId, spaceId, versionId, supersedesVersionId, runId) tuples, creates a regeneration draft, publishes it via the handler, and asserts the audit log entry contains all three required fields |

## Key decisions

- **Synchronous FsCheck pattern**: FsCheck 2.x does not support `async Task<bool>` as a property return type. The test uses `.GetAwaiter().GetResult()` for async calls within the synchronous `bool`-returning property method, consistent with existing property tests in the project.
- **NSubstitute capture pattern**: The audit logger is mocked with NSubstitute, and the `LogAsync` call arguments are captured to verify the `afterJson` payload contains the required fields.
- **In-memory database**: Each test iteration uses a fresh in-memory database to ensure isolation between runs.
- **Minimal mocking**: Only the dependencies that the handler calls are mocked (audit logger, cache, snapshot service, cumulative tracker). The `IScheduleNotificationSender` is omitted (nullable parameter) to avoid unnecessary complexity.

## How it connects

- Validates the implementation from task 9.1 (step 519) which added regeneration metadata to the `PublishVersionCommand` handler's audit log.
- Depends on the `ScheduleVersion.CreateRegenerationDraft` factory method (task 1.2) which sets `SourceType`, `SupersedesVersionId`, and `SourceRunId`.
- Part of the schedule-regeneration spec's Property 10 correctness guarantee.

## How to run / verify

```bash
cd apps/api
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~RegenerationPublishAuditPropertyTests" --verbosity normal
```

Expected: 1 test passes (100 FsCheck iterations).

## What comes next

- Task 9.3: Property test for regeneration not blocking standard runs (Property 7)

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): property test for audit log completeness on regeneration publish"
```
