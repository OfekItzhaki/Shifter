# 366 — User Settings Controller Unit Tests

## Phase

User Timezone Settings — Task 4.2 (optional)

## Purpose

Validates that the `UserSettingsController` correctly dispatches commands/queries to MediatR, propagates validation errors for invalid country/state codes, and enforces authentication via the `[Authorize]` attribute.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Application/UserSettingsControllerTests.cs` | 12 unit tests covering valid updates, invalid country codes, invalid state codes, and unauthenticated access |
| `apps/api/Jobuler.Tests/Jobuler.Tests.csproj` | Added project reference to `Jobuler.Api` to enable direct controller testing |

## Key decisions

- **Direct controller testing with mocked IMediator** — The controller is thin (dispatches to MediatR only), so testing it directly with NSubstitute mocks is simpler and faster than WebApplicationFactory integration tests.
- **Validation errors tested as thrown exceptions** — Since FluentValidation errors are thrown by the MediatR pipeline and caught by `ExceptionHandlingMiddleware`, the controller tests verify the exception propagation rather than HTTP status codes directly.
- **Auth tested via attribute reflection + claim absence** — The `[Authorize]` attribute is verified to exist on the controller class, and unauthenticated access is tested by providing an empty `ClaimsPrincipal` which causes the `CurrentUserId` property to throw.

## How it connects

- Tests validate the controller created in task 4.1 (`UserSettingsController`)
- Relies on the `UpdateUserLocationCommand` and `GetUserSettingsQuery` from task 2.1/2.2
- Validation behavior tested here is implemented by `UpdateUserLocationValidator` (task 2.1)
- The `ExceptionHandlingMiddleware` converts `ValidationException` → HTTP 400 in production

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~UserSettingsControllerTests"
```

Expected: 12 tests pass.

## What comes next

- Task 5 (checkpoint) — verify all backend tests pass together
- Frontend tasks (6.x, 7.x, 8.x) build on the API endpoints tested here

## Git commit

```bash
git add -A && git commit -m "test(timezone): add UserSettingsController unit tests (task 4.2)"
```
