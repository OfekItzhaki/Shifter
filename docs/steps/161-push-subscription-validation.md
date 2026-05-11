# 161 — Push Subscription FluentValidation

## Phase

Phase 4 — Push Notifications (Backend API Layer)

## Purpose

Validates incoming push subscription requests before they reach the command handler, ensuring only well-formed data (valid HTTPS endpoint, Base64URL-encoded keys) enters the database. Returns HTTP 400 with descriptive errors on invalid input.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Notifications/Validators/CreatePushSubscriptionCommandValidator.cs` | FluentValidation validator for `CreatePushSubscriptionCommand` — validates endpoint is HTTPS URL, p256dh and auth are non-empty Base64URL strings |
| `apps/api/Jobuler.Tests/Validation/CreatePushSubscriptionCommandValidatorTests.cs` | Unit tests covering valid input, invalid endpoints (HTTP, FTP, malformed), invalid Base64URL characters, empty/null fields |

## Key decisions

- **Validator in a `Validators` subfolder** — follows the same pattern as `Auth/Validators/` and `Groups/Validators/` in the project
- **Base64URL regex validation** — uses `[A-Za-z0-9\-_]+` pattern (no padding `=` required, matching the Web Push spec where keys are transmitted without padding)
- **No length validation on p256dh/auth** — the design doc mentions 65 bytes (p256dh) and 16 bytes (auth) decoded, but browsers may vary slightly; we validate format only, not exact length
- **Relies on existing pipeline** — the `ValidationBehavior<TRequest, TResponse>` MediatR pipeline behavior auto-discovers and runs this validator; `ExceptionHandlingMiddleware` maps `ValidationException` → HTTP 400

## How it connects

- **Upstream**: `PushSubscriptionsController` dispatches `CreatePushSubscriptionCommand` via MediatR
- **Pipeline**: `ValidationBehavior` intercepts the command, runs this validator, throws `ValidationException` on failure
- **Middleware**: `ExceptionHandlingMiddleware` catches the exception and returns `{ "error": "Validation failed.", "errors": [...] }` with HTTP 400
- **Auto-registration**: `AddValidatorsFromAssembly(typeof(LoginCommand).Assembly)` in `Program.cs` discovers this validator automatically

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~CreatePushSubscriptionCommandValidatorTests"
```

All 19 tests should pass.

## What comes next

- Task 4.3: Property-based tests for input validation (FsCheck) covering Property 9 and Property 10

## Git commit

```bash
git add -A && git commit -m "feat(push-notifications): add FluentValidation for push subscription requests"
```
