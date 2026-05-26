# Step 596 — Exception Middleware Fallback Safety Net

## Phase

Problem Details Migration

## Purpose

Ensures the `ExceptionHandlingMiddleware` never crashes silently or leaves the response in an undefined state. If `ProblemDetailsFactory` or JSON serialization throws during error handling, the middleware catches the inner exception, logs it at Critical level, and writes a minimal RFC 7807-compliant fallback response preserving the original HTTP status code. Additionally handles the ASP.NET Core `Response.HasStarted` scenario gracefully.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Middleware/ExceptionHandlingMiddleware.cs` | Added `Response.HasStarted` early-exit check before attempting to write; wrapped ProblemDetails creation and serialization in try-catch; on inner exception writes minimal JSON `{"type":"about:blank","title":"Error","status":<code>}` |

## Key decisions

- **HasStarted check at the top**: If the response has already started streaming (e.g., chunked transfer), any write attempt would throw. We log and bail early.
- **Preserve original status code in fallback**: The `MapException` result is computed before the try block, so even if serialization fails, the correct HTTP status is used in the minimal response.
- **Minimal JSON body**: Uses `about:blank` as the type URI per RFC 7807 convention for generic errors, keeping the fallback as simple as possible to avoid further serialization failures.
- **Double HasStarted check**: The inner catch also checks `HasStarted` because partial writes inside the try block could have started the response before failing.
- **LogCritical for inner failures**: Serialization failures during error handling are critical operational issues that need immediate attention.

## How it connects

- Depends on `ProblemDetailsFactory` (step 593) and `ProductionSafetyGuard` (step 593) which are called inside the try block.
- Protects all exception mappings implemented in tasks 2.2–2.6.
- Validates Requirement 11.4 (fallback on formatting failure).

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
```

The fallback behavior is tested via unit tests in task 6.1 (`ExceptionHandlingMiddlewareTests.cs`) which will verify:
- Serialization failure triggers minimal JSON fallback
- `Response.HasStarted` scenario — no write attempted

## What comes next

- Task 3 (Checkpoint): Verify middleware compiles and basic behavior works end-to-end.
- Task 4: Migrate controller-level error responses to ProblemDetails.
- Task 6.1: Unit tests covering the fallback safety net scenarios.

## Git commit

```bash
git add -A && git commit -m "feat(problem-details): add fallback safety net for serialization failures in middleware"
```
