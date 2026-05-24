# Step 523 — Regeneration Permission Enforcement Property Test

## Phase

Phase: Schedule Regeneration — Property-Based Testing

## Purpose

Validates that the schedule regeneration endpoint correctly enforces the `ScheduleRecalculate` permission. This property test ensures that for any user who does not hold the required permission, the regeneration request is rejected with an `UnauthorizedAccessException` (which maps to HTTP 403 via `ExceptionHandlingMiddleware`) and no `ScheduleRun` is created.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Scheduling/RegenerationPermissionEnforcementPropertyTests.cs` | FsCheck property-based test with 100+ iterations verifying permission enforcement |

## Key decisions

- **Tests at the controller level**: The permission check happens in `ScheduleRunsController.Regenerate()` via `_permissions.RequirePermissionAsync`, so the test exercises the controller directly with a mocked permission service that denies access.
- **Two complementary properties**: One asserts `UnauthorizedAccessException` is thrown and no DB records are created; the other verifies the MediatR command is never dispatched when permission is denied.
- **Random inputs via FsCheck**: Generates random `userId`, `spaceId`, and `groupId` (excluding `Guid.Empty`) to prove the property holds for all valid identifier combinations.
- **NSubstitute for mocking**: Uses NSubstitute to mock `IPermissionService` (throws on `ScheduleRecalculate`) and `IMediator` (verifies no dispatch).

## How it connects

- Validates **Requirements 7.1, 7.2** from the schedule-regeneration spec
- Implements **Property 8: Permission enforcement** from the design document
- Tests the `ScheduleRunsController.Regenerate` endpoint added in task 5.1
- Relies on the `ExceptionHandlingMiddleware` to map `UnauthorizedAccessException` → HTTP 403 in production

## How to run / verify

```bash
cd apps/api
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~RegenerationPermissionEnforcementPropertyTests" --verbosity normal
```

Expected: 2 tests pass (100 iterations each).

## What comes next

- Task 7.1: Worker handling for regeneration trigger mode
- Task 7.3: Property test for failed regeneration recording error without side effects
- Task 7.5: Property test for published version immutability

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): property test for permission enforcement (Property 8)"
```
