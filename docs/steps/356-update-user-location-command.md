# 356 — UpdateUserLocationCommand and Handler

## Phase

User Timezone Settings — Backend Application Layer (Task 2.1)

## Purpose

Implements the command, validator, and handler for updating a user's geographic location (Country/State). This is the core write operation that persists the user's location and returns the resolved timezone. The validator ensures only valid ISO 3166-1 alpha-2 country codes and ISO 3166-2 subdivision codes are accepted.

## What was built

| File | Description |
|------|-------------|
| `Application/UserSettings/Commands/UpdateUserLocationCommand.cs` | MediatR command record `UpdateUserLocationCommand(Guid UserId, string CountryCode, string? StateCode)` and its handler that persists location and resolves timezone |
| `Application/UserSettings/Validators/UpdateUserLocationValidator.cs` | FluentValidation validator that checks CountryCode against supported ISO 3166-1 alpha-2 codes and StateCode against the country's valid subdivisions |
| `Application/UserSettings/ValidLocationCodes.cs` | Static reference data class containing the set of valid country codes and country→state mappings used for validation |

## Key decisions

1. **Validation data in Application layer** — The `ValidLocationCodes` static class lives in Application (not Infrastructure) because the validator needs it and Application cannot reference Infrastructure. This is pure reference data (ISO codes), not business logic.
2. **State code validation is strict** — If a country has no state-level timezone mappings (single-timezone country), providing a state code is rejected. This prevents meaningless data from being stored.
3. **Handler calls `ITimezoneResolver.Resolve` after persisting** — The handler first persists the location, then resolves the timezone from the persisted (normalized) values. This ensures the response reflects exactly what was stored.
4. **Returns `TimezoneResolution`** — The command returns the resolved timezone so the frontend can immediately update the session context without a separate query (requirement 4.5).

## How it connects

- **Depends on**: `ITimezoneResolver` (Application interface, implemented in Infrastructure), `User.UpdateLocation()` (Domain entity method), `AppDbContext` (persistence)
- **Used by**: `UserSettingsController` (task 4.1) will dispatch this command via MediatR
- **Validated by**: `ValidationBehavior` MediatR pipeline behavior automatically runs `UpdateUserLocationValidator` before the handler executes

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

The build succeeds with 0 errors. Full integration testing will come with task 2.3 (property tests) and task 4.2 (controller tests).

## What comes next

- Task 2.2: `GetUserSettingsQuery` and handler (reads user settings + resolves timezone)
- Task 2.3: Property tests for settings persistence and validation
- Task 4.1: `UserSettingsController` that dispatches this command

## Git commit

```bash
git add -A && git commit -m "feat(timezone): implement UpdateUserLocationCommand, validator, and handler"
```
