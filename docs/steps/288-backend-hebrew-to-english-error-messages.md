# 288 — Backend Hebrew-to-English Error Messages

## Phase
Maintenance — i18n cleanup

## Purpose
Replace all hardcoded Hebrew exception messages in backend C# code with English equivalents. These messages are returned to the frontend via `ExceptionHandlingMiddleware` as JSON error responses — the frontend handles its own localized display, so backend messages should be in English for developer/API clarity.

## What was built
Files modified (string-only changes, no logic changes):

| File | Strings replaced |
|------|-----------------|
| `Jobuler.Domain/Scheduling/ScheduleVersion.cs` | 2 |
| `Jobuler.Domain/Spaces/SpaceRole.cs` | 1 |
| `Jobuler.Domain/Spaces/UnavailabilityReason.cs` | 2 |
| `Jobuler.Domain/Identity/WebAuthnCredential.cs` | 5 |
| `Jobuler.Domain/Groups/Group.cs` | 2 |
| `Jobuler.Domain/Groups/GroupMembership.cs` | 1 |
| `Jobuler.Domain/Groups/HomeLeaveTemplate.cs` | 3 |
| `Jobuler.Infrastructure/Auth/Fido2Service.cs` | 5 |
| `Jobuler.Infrastructure/Scheduling/RedisSolverJobQueue.cs` | 1 |
| `Jobuler.Application/HomeLeave/FeasibilityEngine.cs` | 1 |
| `Jobuler.Application/Tasks/Commands/GroupTaskCommands.cs` | 3 |
| `Jobuler.Application/Tasks/Queries/GetGroupTasksQuery.cs` | 1 |
| `Jobuler.Application/Groups/Commands/SetHomeLeavePriorityCommand.cs` | 1 |
| `Jobuler.Tests/HomeLeave/FeasibilityEngineTests.cs` | 1 (test assertion updated) |

**Total: 29 Hebrew strings replaced with English equivalents.**

## Key decisions
- Exception types preserved exactly (InvalidOperationException, KeyNotFoundException, ArgumentException)
- Messages kept concise and developer-facing
- Test assertions updated to match new English messages
- Hebrew test *input data* (e.g. Hebrew names as valid input) left unchanged — those test that the system accepts Unicode input correctly

## How it connects
- `ExceptionHandlingMiddleware` maps exception types to HTTP status codes and returns the message as JSON
- Frontend i18n layer handles user-facing localization independently
- No API contract changes — error response structure unchanged

## How to run / verify
```bash
cd apps/api
dotnet build        # zero errors
dotnet test         # all tests pass
```

## What comes next
- No further backend Hebrew strings remain in production code
- Frontend continues to handle its own localized error display

## Git commit
```bash
git add -A && git commit -m "fix(i18n): replace all backend Hebrew error messages with English"
```
