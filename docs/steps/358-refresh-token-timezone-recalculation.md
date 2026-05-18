# 358 — Refresh Token Timezone Recalculation

## Phase

Feature: User Timezone Settings — Task 3.2

## Purpose

When a user refreshes their token (e.g., after a session gap or overnight), the timezone offset may have changed due to DST transitions. This step modifies the `RefreshTokenCommandHandler` to recalculate the timezone on every token refresh, ensuring the frontend always has the correct offset without requiring a full re-login.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Application/Auth/Commands/LoginCommand.cs` | Added `TimezoneId` (string) and `TimezoneOffsetMinutes` (int) fields to the `LoginResult` record |
| `Jobuler.Application/Auth/Commands/RefreshTokenCommandHandler.cs` | Injected `ITimezoneResolver`, calls `Resolve(user.CountryCode, user.StateCode)` and includes timezone data in the response |
| `Jobuler.Application/Auth/Commands/LoginCommandHandler.cs` | Injected `ITimezoneResolver`, calls `Resolve` and includes timezone data in the login response (required for `LoginResult` signature change) |
| `Jobuler.Application/Auth/Commands/WebAuthnLoginCompleteCommand.cs` | Injected `ITimezoneResolver`, includes timezone data in WebAuthn login response (required for `LoginResult` signature change) |

## Key decisions

1. **Single `LoginResult` record for all auth flows** — Login, refresh, and WebAuthn all return the same `LoginResult` shape. Adding timezone fields to the record required updating all three handlers simultaneously.
2. **Recalculate on every refresh** — Per requirement 4.4, the offset is recalculated during token refresh to handle DST changes that may have occurred between sessions.
3. **No caching of timezone resolution** — The resolver is called fresh each time to ensure the offset reflects the current moment's DST state.

## How it connects

- Depends on: `ITimezoneResolver` (task 1.2), `User.CountryCode`/`StateCode` (task 1.1)
- Consumed by: Frontend `authStore` which stores `timezoneId` and `timezoneOffsetMinutes` from the refresh response (task 6.1)
- Related: Task 3.1 (login timezone) was completed as part of this change since both share the `LoginResult` record

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The build should succeed with 0 errors. Integration tests for the refresh flow (task 3.3) will verify the timezone fields are present in the response.

## What comes next

- Task 3.3: Unit tests for login and refresh timezone integration
- Task 6.1: Frontend authStore updates to consume the new fields

## Git commit

```bash
git add -A && git commit -m "feat(timezone): recalculate timezone offset on token refresh"
```
