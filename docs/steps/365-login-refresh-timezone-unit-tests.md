# Step 365 — Login & Refresh Timezone Integration Unit Tests

## Phase

User Timezone Settings — Task 3.3

## Purpose

Validates that the `LoginCommandHandler` and `RefreshTokenCommandHandler` correctly integrate with `ITimezoneResolver` to include timezone data in their responses. Covers the default fallback to `Asia/Jerusalem` when no country is set.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Application/LoginRefreshTimezoneTests.cs` | 7 unit tests covering login and refresh timezone integration |

### Tests implemented

1. **Login_ResponseIncludesTimezoneIdAndOffsetMinutes** — Verifies login result contains `TimezoneId` and `TimezoneOffsetMinutes` for a US/CA user.
2. **Login_SingleTimezoneCountry_ReturnsCorrectTimezone** — Verifies single-timezone country (IL) resolves correctly.
3. **Login_NullCountry_DefaultsToAsiaJerusalem** — Verifies null country falls back to `Asia/Jerusalem`.
4. **Login_CallsTimezoneResolverWithUserLocation** — Verifies the resolver is called with the user's stored country/state codes.
5. **Refresh_RecalculatesTimezoneOffset** — Verifies refresh recalculates offset (DST change scenario).
6. **Refresh_ResponseIncludesTimezoneFields** — Verifies refresh result includes timezone fields.
7. **Refresh_NullCountry_DefaultsToAsiaJerusalem** — Verifies refresh with null country falls back to `Asia/Jerusalem`.

## Key decisions

- Used **NSubstitute** for mocking (project standard, not Moq)
- Used **EF Core InMemory** provider for database isolation
- Used low BCrypt work factor (4) in tests for speed
- Mocked `ITimezoneResolver` to isolate handler logic from actual timezone resolution
- Mocked `IServiceScopeFactory` and `IConflictDetectionService` to satisfy `LoginCommandHandler` dependencies

## How it connects

- Tests validate the integration added in tasks 3.1 and 3.2
- Confirms requirements 4.1 (login includes timezone), 4.2 (offset in response), and 4.4 (refresh recalculates)
- The `ITimezoneResolver` implementation is tested separately in task 1.4

## How to run / verify

```bash
cd apps/api
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~LoginRefreshTimezoneTests" --verbosity normal
```

Expected: 7 tests pass.

## What comes next

- Task 4.2: Unit tests for `UserSettingsController`
- Task 6.1: Frontend auth store timezone field integration

## Git commit

```bash
git add -A && git commit -m "test(timezone): add login and refresh timezone integration unit tests"
```
