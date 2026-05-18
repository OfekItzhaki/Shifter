# 356 — GetUserSettingsQuery and Handler

## Phase

User Timezone Settings — Backend Application Layer (Task 2.2)

## Purpose

Provides a query to retrieve a user's current settings including their geographic location (Country/State), resolved timezone, and time format preference. This is consumed by the `GET /api/user-settings` endpoint to display the user's current configuration on the Settings page.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/UserSettings/Queries/GetUserSettingsQuery.cs` | `GetUserSettingsQuery` record, `UserSettingsDto` record, and `GetUserSettingsQueryHandler` implementation |

### Details

- **`GetUserSettingsQuery(Guid UserId)`** — MediatR query record that identifies which user's settings to retrieve.
- **`UserSettingsDto`** — DTO containing `CountryCode`, `StateCode`, `TimezoneId`, `TimezoneOffsetMinutes`, and `TimeFormat`.
- **`GetUserSettingsQueryHandler`** — Reads the user from the database, calls `ITimezoneResolver.Resolve` to compute the current timezone and offset, and returns the assembled DTO. Throws `KeyNotFoundException` if the user doesn't exist (mapped to 404 by middleware).

## Key decisions

1. **TimeFormat defaults to "24h"** — The `TimeFormat` field is not yet persisted on the User entity (no column exists). It defaults to `"24h"` which matches the primary user base (Israel). A future task will add persistence for this preference.
2. **Timezone resolved on every read** — Rather than caching the timezone on the user entity, we resolve it fresh each time. This ensures DST transitions are always reflected correctly.
3. **KeyNotFoundException for missing user** — Follows the existing error handling pattern where `ExceptionHandlingMiddleware` maps this to HTTP 404.
4. **AsNoTracking** — Read-only query uses `AsNoTracking()` for performance, consistent with other query handlers.

## How it connects

- **Depends on**: `ITimezoneResolver` (task 1.2), `User` entity with `CountryCode`/`StateCode` (task 1.1)
- **Consumed by**: `UserSettingsController` `GET /api/user-settings` endpoint (task 4.1)
- **Frontend consumer**: Settings page loads this on mount to display current location and timezone (task 8.1)

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no new warnings.

## What comes next

- Task 2.3: Property tests for settings persistence and validation
- Task 4.1: `UserSettingsController` wiring the `GET /api/user-settings` endpoint to this query

## Git commit

```bash
git add -A && git commit -m "feat(user-settings): implement GetUserSettingsQuery and handler"
```
