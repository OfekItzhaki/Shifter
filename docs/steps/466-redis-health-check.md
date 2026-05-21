# Step 466 — Redis Health Check

## Phase

Health Check Alerts feature — Service Health Checks

## Purpose

Implements the Redis connectivity health check as part of the health monitoring system. This check executes a PING command against the Redis instance via `IConnectionMultiplexer` and reports whether Redis is reachable, along with the response time.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/HealthChecks/RedisHealthCheck.cs` | `IServiceHealthCheck` implementation that PINGs Redis and returns healthy/unhealthy with response time |

## Key decisions

1. **Reuse existing DI pattern** — `IConnectionMultiplexer` is already registered as a singleton in `Program.cs`. The health check injects it directly, matching the pattern used by `RedisCacheService` and `RedisSolverJobQueue`.
2. **PingAsync for connectivity** — Uses `GetDatabase().PingAsync()` which is the standard Redis PING command. This is the same approach the existing `HealthController.Get()` uses.
3. **Consistent pattern with PostgresHealthCheck** — Follows the exact same structure: Stopwatch for timing, try/catch for error handling, returns `ServiceHealthResult` with appropriate status.

## How it connects

- Implements `IServiceHealthCheck` from the Application layer (`Jobuler.Application/Common/HealthChecks/`)
- Will be registered in DI and consumed by `HealthCheckRunner` (task 3.1)
- Part of the health monitoring system that feeds into the `/health/detailed` endpoint and background monitor

## How to run / verify

```bash
cd apps/api/Jobuler.Infrastructure
dotnet build --no-restore
```

The build should succeed with no new warnings.

## What comes next

- Task 2.3: LemonSqueezyHealthCheck
- Task 2.4: SendGridHealthCheck
- Task 2.5: SolverHealthCheck
- Task 3.1: HealthCheckRunner that aggregates all checks

## Git commit

```bash
git add -A && git commit -m "feat(health): implement Redis health check with PING connectivity test"
```
