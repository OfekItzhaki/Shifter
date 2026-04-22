# Step 022 — Test Coverage and Build Fixes

## Phase
Post-MVP Completion

## Purpose
Add test coverage for everything built in steps 019–021 (role assignment, notifications, availability windows), fix a pre-existing MediatR version conflict that prevented the test suite from running, and restore missing domain entity source files.

## What was built

### Tests

| File | Description |
|---|---|
| `Tests/Application/AssignRoleCommandTests.cs` | 7 tests: assign creates assignment, idempotent assign, unknown person throws, unknown role throws, cross-space throws, remove deletes assignment, idempotent remove |
| `Tests/Application/NotificationTests.cs` | 13 tests: domain MarkRead, NotificationService creates per-member, GetNotificationsQuery filters by user/space/unread, DismissNotification marks read, wrong user is no-op, DismissAll marks all, DismissAll doesn't affect other users |
| `Tests/Domain/AvailabilityWindowTests.cs` | 5 tests: valid range, ends-before-starts throws, equal times throws, null note, whitespace trim |
| `Tests/Integration/UserRoleAssignmentFlowTests.cs` | 3 integration tests mimicking a full user flow: register → create space → create person → create role → assign → verify in detail → remove → verify gone; multi-role; tenant isolation |

### Build fixes

| File | Change |
|---|---|
| `Api/Jobuler.Api.csproj` | Replaced `MediatR.Extensions.Microsoft.DependencyInjection` v11 (conflicted with MediatR v12) with `MediatR` v12 directly |
| `Application/Jobuler.Application.csproj` | Added `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Configuration.Abstractions`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `BCrypt.Net-Next` packages so Application compiles standalone |
| `Application/Auth/IJwtService.cs` | Moved `IJwtService` interface from Infrastructure to Application (correct layering — Application defines contracts, Infrastructure implements them) |
| `Infrastructure/Auth/JwtService.cs` | Updated to implement `Jobuler.Application.Auth.IJwtService` instead of the now-removed local interface |
| `Infrastructure/Jobuler.Infrastructure.csproj` | Added `Microsoft.IdentityModel.Tokens`, `System.IdentityModel.Tokens.Jwt`, `Microsoft.Extensions.Hosting.Abstractions` |
| `Infrastructure/AI/OpenAiAssistant.cs` | Fixed raw string literal: switched from `$"""` to `$$"""` so `{` is literal and interpolations use `{{expr}}` |
| `Application/Persistence/AppDbContext.cs` | Moved from Infrastructure to Application so handlers can reference it without a circular project reference. Uses `ConfigurationAssembly` static property so Infrastructure's EF configurations are still applied at runtime |
| `Domain/Logs/SystemLog.cs` | Restored missing domain entity (source was absent from workspace, binary was compiled) |
| `Domain/Logs/AuditLog.cs` | Restored missing domain entity |
| `Application/Scheduling/Commands/RollbackVersionCommand.cs` | Removed duplicate class definition that caused CS0101 |
| `Jobuler.sln` | Added test project to `ProjectConfigurationPlatforms` so `dotnet test` on the solution actually runs tests |

## Key decisions

### AppDbContext moved to Application
The pre-existing codebase had Application handlers directly using `AppDbContext` (a pragmatic violation of strict layering). Moving `AppDbContext` to Application resolves the circular dependency while keeping the existing handler code unchanged. EF configurations remain in Infrastructure and are applied via `ConfigurationAssembly` set at startup.

### IJwtService moved to Application
Interfaces belong in the layer that consumes them. Moving `IJwtService` to Application means auth handlers no longer need to reference Infrastructure, which is the correct direction.

### Integration tests mimic real user flows
The integration tests use InMemory EF and call real handler classes directly — no mocks for the happy path. This gives high confidence that the full command/query chain works end-to-end.

## How to run / verify

```bash
cd apps/api
dotnet test Jobuler.sln --verbosity minimal
# Expected: 51 passed, 0 failed
```

## What comes next
- PDF export
- Update HANDOFF.md to reflect completed items

## Git commit

```bash
git add -A && git commit -m "test: add coverage for roles, notifications, availability + fix build"
```
