# 594 — ProblemDetailsResults Static Helper

## Phase

Problem Details Migration — Infrastructure Layer

## Purpose

Provides a static helper that controllers use to return RFC 7807 ProblemDetails responses with domain-specific extension properties (e.g., `alternativeSlots`, `retryAfterSeconds`). This avoids controllers constructing anonymous error objects and ensures all error responses share the same structure produced by `ProblemDetailsFactory`.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Middleware/ProblemDetailsResults.cs` | Static class with a `Problem()` method that wraps `ProblemDetailsFactory.Create()` into an `ObjectResult` with `Content-Type: application/problem+json` |

## Key decisions

- Returns `ObjectResult` (not `IActionResult`) so callers get a concrete type with `StatusCode` and `ContentTypes` already set.
- Sets `ContentTypes` on the `ObjectResult` to `application/problem+json` to ensure the correct media type is negotiated by ASP.NET Core's output formatter.
- Delegates entirely to `ProblemDetailsFactory.Create()` for the ProblemDetails construction — single source of truth for type URI, instance path, and traceId.

## How it connects

- **Upstream:** Controllers (`ShiftRequestsController`, `WaitlistController`, etc.) call `ProblemDetailsResults.Problem()` instead of returning anonymous objects.
- **Downstream:** Internally calls `ProblemDetailsFactory.Create()` which populates all RFC 7807 fields and extensions.
- **Consumed by:** Task 4.1 (ShiftRequestsController migration) and Task 4.2 (WaitlistController migration).

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Api/Jobuler.Api.csproj
```

The file should compile without errors. No runtime behavior changes until controllers are migrated to use it.

## What comes next

- Task 2.1–2.7: Refactor `ExceptionHandlingMiddleware` to use ProblemDetails.
- Task 4.1: Migrate `ShiftRequestsController` error responses to use `ProblemDetailsResults.Problem()`.
- Task 4.2: Migrate `WaitlistController` error responses similarly.

## Git commit

```bash
git add -A && git commit -m "feat(problem-details): add ProblemDetailsResults static helper for controllers"
```
