# 466 — PostgreSQL Health Check Implementation

## Phase

Health Check Alerts — Individual Service Health Checks

## Purpose

Implements the PostgreSQL connectivity health check as a standalone class following the `IServiceHealthCheck` interface. This encapsulates the database connectivity verification (SELECT 1) into a reusable, independently testable component that will be aggregated by the `HealthCheckRunner`.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/HealthChecks/PostgresHealthCheck.cs` | Implements `IServiceHealthCheck` for PostgreSQL. Executes `SELECT 1` via `AppDbContext`, measures response time, and returns healthy/unhealthy status with error details on failure. |

## Key decisions

- **Reuses existing pattern**: The `SELECT 1` approach matches what the existing `HealthController.Get()` already does, ensuring consistency.
- **Stopwatch for timing**: Uses `System.Diagnostics.Stopwatch` for accurate response time measurement, included in both success and failure results.
- **Exception-safe**: Catches all exceptions and returns an unhealthy result with the error message rather than letting exceptions propagate. The `HealthCheckRunner` will handle timeouts separately.
- **Minimal dependencies**: Only depends on `AppDbContext` (injected via DI), keeping the class focused and testable.

## How it connects

- Implements `IServiceHealthCheck` defined in `Jobuler.Application/Common/HealthChecks/`
- Returns `ServiceHealthResult` record defined in the Application layer
- Will be registered in DI and consumed by `HealthCheckRunner` (task 3.1)
- Uses `AppDbContext.Database.ExecuteSqlRawAsync` — same approach as the existing `/health` endpoint

## How to run / verify

```bash
cd apps/api/Jobuler.Infrastructure
dotnet build --no-restore
```

The build should succeed with no new warnings. Full integration testing requires a running PostgreSQL instance and will be covered by integration tests in task 7.3.

## What comes next

- Task 2.2: RedisHealthCheck implementation
- Task 2.3–2.5: LemonSqueezy, SendGrid, Solver health checks
- Task 3.1: HealthCheckRunner that aggregates all checks with timeout handling

## Git commit

```bash
git add -A && git commit -m "feat(health): implement PostgresHealthCheck service"
```
