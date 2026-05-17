# 310 — Update Platform Settings Command

## Phase

Admin Session Timeout — Backend Commands

## Purpose

Provides a command and handler for updating platform-level settings (specifically the platform session timeout duration). This enables super-admins to configure the inactivity timeout for Super Platform Mode through the API.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Platform/Commands/UpdatePlatformSettingsCommand.cs` | Command record with `UserId` and `PlatformTimeoutMinutes` parameters, plus handler that validates platform admin permission, enforces [5, 120] range, loads the `PlatformSettings` entity by key, and updates its value. |
| `apps/api/Jobuler.Application/Platform/Validators/UpdatePlatformSettingsCommandValidator.cs` | FluentValidation validator ensuring `UserId` is not empty and `PlatformTimeoutMinutes` is between 5 and 120 inclusive. |

## Key decisions

- **Platform admin check inside handler**: Follows the established pattern from billing commands (`CreateCouponHandler`, `DeactivateCouponHandler`) where the handler loads the user and checks `IsPlatformAdmin` directly, throwing `UnauthorizedAccessException` if not authorized. This is appropriate because platform-level commands don't have a `spaceId` to pass to `IPermissionService`.
- **Dual validation**: Range is validated both in the FluentValidation validator (for early rejection via the `ValidationBehavior` pipeline) and in the handler (defense-in-depth). The handler throws `InvalidOperationException` which maps to 400 via `ExceptionHandlingMiddleware`.
- **String storage**: The timeout value is stored as a string in the `PlatformSettings` key-value table, converted via `ToString()`. This matches the existing `PlatformSettings` entity design.

## How it connects

- Depends on: `PlatformSettings` domain entity (step 307), `AppDbContext` with `PlatformSettings` DbSet
- Used by: `PlatformController` PATCH endpoint (task 4.4) which will dispatch this command
- Related: `UpdateGroupSettingsCommand` handles the per-group timeout (task 3.2)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Api
```

Build should succeed with no new warnings.

## What comes next

- Task 4.4: Add `PATCH /platform/settings` and `GET /platform/settings` endpoints to `PlatformController` that dispatch this command
- Task 3.4: Property test for timeout duration range validation

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): add UpdatePlatformSettingsCommand and handler"
```
