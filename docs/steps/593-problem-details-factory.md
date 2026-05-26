# 593 — ProblemDetailsFactory Static Helper

## Phase

Problem Details Migration — Phase 1 (Infrastructure)

## Purpose

Provides a single, consistent factory method for constructing RFC 7807 `ProblemDetails` instances across the API. Every error response — whether produced by the `ExceptionHandlingMiddleware` or by controllers directly — will use this factory to guarantee structural completeness (type URI, instance path, traceId, status, title, detail).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Middleware/ProblemDetailsFactory.cs` | Internal static class with a `Create` method that builds a `ProblemDetails` object with all required RFC 7807 fields plus the `traceId` extension and any caller-supplied extensions. |

## Key decisions

- **Static helper, not DI service** — the factory is pure (no dependencies beyond `HttpContext`) so a static class keeps usage simple and avoids unnecessary service registrations.
- **`internal` visibility** — only the API project needs access; it's not part of the public contract.
- **Base URI constant** — `https://docs.jobuler.com/errors/` is defined once; individual callers pass only the slug.
- **traceId always first** — added before merging caller extensions so it cannot be accidentally overwritten by a caller passing a `traceId` key (last-write-wins from the merge loop would overwrite it, but the convention is that callers should not pass `traceId`).

## How it connects

- Will be consumed by `ExceptionHandlingMiddleware` (task 2.2) to replace all `JsonSerializer.Serialize(new { error = ... })` calls.
- Will be consumed by `ProblemDetailsResults` helper (task 1.3) for controller-level error responses.
- `ProductionSafetyGuard` (task 1.2) will sanitize the output of this factory before serialization.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Api/Jobuler.Api.csproj
```

The file should compile without errors or warnings.

## What comes next

- Task 1.2: `ProductionSafetyGuard` static helper (strips sensitive extensions in production).
- Task 1.3: `ProblemDetailsResults` controller helper that wraps this factory into an `ObjectResult`.

## Git commit

```bash
git add -A && git commit -m "feat(problem-details): add ProblemDetailsFactory static helper class"
```
