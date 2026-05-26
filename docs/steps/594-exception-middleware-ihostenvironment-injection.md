# 594 — Inject IHostEnvironment into ExceptionHandlingMiddleware

## Phase

Problem Details Migration — Task 2.1

## Purpose

The middleware needs to distinguish between production and development environments to control whether sensitive exception details (stack traces, type names, inner exceptions) are included in error responses. Injecting `IHostEnvironment` enables environment-aware behavior required by Requirements 12.4 and 12.5.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Middleware/ExceptionHandlingMiddleware.cs` | Added `IHostEnvironment` constructor parameter and `_environment` field |

## Key decisions

- **No changes to `Program.cs`** — ASP.NET Core's `UseMiddleware<T>()` automatically resolves constructor dependencies from the DI container. `IHostEnvironment` is registered by the framework by default.
- **Field naming** — stored as `_environment` following the existing `_next` / `_logger` convention in the class.

## How it connects

- The `_environment` field will be used by subsequent tasks (2.2–2.7) to conditionally add debug extensions and to call `ProductionSafetyGuard.Sanitize()` before serialization.
- `ProductionSafetyGuard` (task 1.2, already implemented) accepts `IHostEnvironment` to decide whether to strip sensitive data.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with zero errors.

## What comes next

- Task 2.2: Implement ProblemDetails response writing using `ProblemDetailsFactory` and the new `_environment` field.

## Git commit

```bash
git add -A && git commit -m "feat(problem-details): inject IHostEnvironment into ExceptionHandlingMiddleware"
```
