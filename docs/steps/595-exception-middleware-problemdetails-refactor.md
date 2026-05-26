# 595 — ExceptionHandlingMiddleware ProblemDetails Refactor

## Phase

Problem Details Migration — Phase 2 (Middleware Refactoring)

## Purpose

Refactor the `ExceptionHandlingMiddleware` to produce RFC 7807 ProblemDetails responses instead of custom `{ error: "..." }` JSON. This covers tasks 2.2–2.6 of the problem-details-migration spec: response writing, ValidationException mapping, all other exception mappings, RateLimitExceededException with Retry-After, and DbUpdateException constraint handling.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Middleware/ExceptionHandlingMiddleware.cs` | Full rewrite of `HandleExceptionAsync` to use `ProblemDetailsFactory.Create()`, `ProductionSafetyGuard.Sanitize()`, and atomic `WriteAsJsonAsync` with camelCase serialization |

## Key decisions

1. **Single `MapException` method** — all exception-to-response mapping is centralized in a switch expression returning a tuple of (statusCode, title, detail, typeSlug, extensions). This keeps the handler method clean and focused on writing the response.

2. **Switch ordering** — `ConflictException` (which extends `InvalidOperationException`) is matched before `InvalidOperationException` to prevent incorrect 400 responses for conflicts.

3. **Development debug extensions** — `exceptionType`, `stackTrace`, and `innerException` are added only in development mode, then `ProductionSafetyGuard.Sanitize()` strips them in production as a safety net.

4. **Atomic write** — uses `WriteAsJsonAsync` with `JsonSerializerOptions` (camelCase) for a single response write operation, satisfying Requirement 3.4.

5. **DbUpdateException check constraints → 409** — changed from the previous 400 status to 409 Conflict per the design spec (Requirement 5.3), preserving the existing `ExtractCheckConstraintMessage` logic.

6. **Content-Type** — all error responses use `application/problem+json` per RFC 7807.

## How it connects

- Depends on `ProblemDetailsFactory` (task 1.1) and `ProductionSafetyGuard` (task 1.2) created in the previous wave.
- The `_environment` field was injected in task 2.1.
- Task 2.7 (fallback safety net) will wrap this logic in a try-catch for serialization failures.
- Controller migrations (tasks 4.1, 4.2) use `ProblemDetailsResults` for direct error responses.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

The build succeeds with zero warnings related to this file.

## What comes next

- Task 2.7: Wrap ProblemDetails creation in a try-catch fallback safety net
- Tasks 4.1–4.2: Migrate controller-level error responses
- Tasks 6.1–6.7: Unit and property-based tests for the middleware

## Git commit

```bash
git add -A && git commit -m "feat(problem-details): refactor ExceptionHandlingMiddleware to RFC 7807 ProblemDetails"
```
