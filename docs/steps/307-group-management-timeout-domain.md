# Step 307 — Group ManagementTimeoutMinutes Domain Property

## Phase

Admin Session Timeout — Domain Model Extension

## Purpose

Extends the `Group` domain entity with a `ManagementTimeoutMinutes` property and a `SetManagementTimeout(int minutes)` method. This allows each group to have a configurable inactivity timeout for management mode sessions, with validation ensuring the value stays within the allowed range of 5–120 minutes.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Groups/Group.cs` | Added `ManagementTimeoutMinutes` property (default 15) and `SetManagementTimeout(int minutes)` method with [5, 120] range validation |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs` | Added EF Core mapping for `management_timeout_minutes` column with default value 15 |

## Key decisions

- Follows the same pattern as `SetMinRestBetweenShifts` for range validation — throws `InvalidOperationException` on invalid input.
- Default value of 15 matches the migration (060) and the requirements.
- Validation is in the domain entity (not just the database CHECK constraint) to enforce invariants at the domain layer per Clean Architecture.
- Calls `Touch()` on successful update to track modification time.

## How it connects

- Migration 060 already added the `management_timeout_minutes` column with a CHECK constraint — this step maps it to the domain model.
- The `UpdateGroupSettingsCommand` handler (task 3.2) will call `group.SetManagementTimeout(value)` when the admin updates the timeout.
- The frontend will read this value when starting the inactivity timer for management mode sessions.

## How to run / verify

```bash
cd apps/api
dotnet build
dotnet test --filter "FullyQualifiedName~AutoSchedulerBugCondition"
```

All tests pass. The build succeeds with no new warnings.

## What comes next

- Task 1.4: Create `PlatformSettings` domain entity
- Task 3.2: Extend `UpdateGroupSettingsCommand` to accept and persist `ManagementTimeoutMinutes`

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): add ManagementTimeoutMinutes to Group domain entity"
```
