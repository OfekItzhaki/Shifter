# Step 593 — ProductionSafetyGuard Static Helper

## Phase

Phase: Problem Details Migration (RFC 7807)

## Purpose

Provides a centralized safety mechanism that strips sensitive debugging information (stack traces, exception type names, inner exception details) from ProblemDetails responses in production environments. This ensures no internal implementation details leak to clients regardless of any other configuration settings.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Middleware/ProductionSafetyGuard.cs` | Static helper class with a `Sanitize` method that removes `exceptionType`, `stackTrace`, and `innerException` extension keys from ProblemDetails when running in production |

## Key decisions

- **Static class** — no state needed, pure function that operates on the ProblemDetails instance in-place
- **Returns the same instance** — enables fluent chaining (e.g., `ProductionSafetyGuard.Sanitize(problem, env)` can be used inline)
- **Unconditional stripping in production** — the guard enforces removal regardless of any debug flags or configuration overrides, satisfying Requirement 12.5
- **Development passthrough** — in non-production environments, all extensions are left intact for debugging (Requirement 12.4)
- **Sensitive keys defined as a static array** — easy to extend if new sensitive keys are added in the future

## How it connects

- Called by `ExceptionHandlingMiddleware` as the **final step before serialization** to ensure no sensitive data escapes
- Works with `ProblemDetailsFactory` which may add debug extensions in development mode — this guard strips them in production
- Satisfies Requirements 12.1, 12.2, 12.3, 12.5 from the Problem Details Migration spec

## How to run / verify

```bash
dotnet build apps/api/Jobuler.Api/Jobuler.Api.csproj
```

The class is a static helper — full integration verification happens when the middleware (task 2.2) calls `Sanitize()` before writing responses.

## What comes next

- Task 1.3: `ProblemDetailsResults` controller helper (depends on ProblemDetailsFactory)
- Task 2.2: Middleware refactoring will call `ProductionSafetyGuard.Sanitize()` before serialization
- Task 6.7: Property test verifying no sensitive data leaks in production mode

## Git commit

```bash
git add -A && git commit -m "feat(problem-details): add ProductionSafetyGuard static helper class"
```
