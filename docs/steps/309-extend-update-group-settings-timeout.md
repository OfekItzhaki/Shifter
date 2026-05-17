# Step 309 — Extend UpdateGroupSettingsCommand with ManagementTimeoutMinutes

## Phase

Phase: Admin Session Timeout Feature

## Purpose

Allows group admins to configure the management mode inactivity timeout duration through the existing group settings PATCH endpoint. The timeout value is persisted per group and used by the frontend inactivity timer when entering management mode.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Groups/Commands/UpdateGroupSettingsCommand.cs` | Added optional `int? ManagementTimeoutMinutes` parameter to the command record. Handler calls `group.SetManagementTimeout(value)` when the parameter is provided. |
| `apps/api/Jobuler.Api/Controllers/GroupsController.cs` | Added `ManagementTimeoutMinutes` to `UpdateGroupSettingsRequest` record and passed it through to the command in the `UpdateSettings` action. |

## Key decisions

- The parameter is optional (`int?`) so existing clients that don't send it are unaffected — backward compatible.
- Validation is handled by the domain entity's `SetManagementTimeout` method which enforces the [5, 120] range and throws `InvalidOperationException` for out-of-range values (mapped to 400 by the exception middleware).
- The existing `Permissions.PeopleManage` permission check on the `UpdateSettings` endpoint covers this new field — no additional authorization logic needed.

## How it connects

- Depends on task 1.3 which added `ManagementTimeoutMinutes` property and `SetManagementTimeout` method to the `Group` domain entity.
- The frontend (task 10.1) will include `managementTimeoutMinutes` in the PATCH request body.
- Task 4.3 extends the GET response to also return this value.

## How to run / verify

```bash
cd apps/api && dotnet build
```

Build should succeed with no errors related to these changes.

## What comes next

- Task 3.3: Create `UpdatePlatformSettingsCommand` for system-level timeout configuration.
- Task 4.3: Extend the group settings GET response to include `managementTimeoutMinutes`.

## Git commit

```bash
git add -A && git commit -m "feat(admin-timeout): extend UpdateGroupSettingsCommand with ManagementTimeoutMinutes"
```
