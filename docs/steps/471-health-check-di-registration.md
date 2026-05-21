# 471 — Health Check DI Registration

## Phase

Health Check Alerts — Task 7.2

## Purpose

Registers all health check monitoring and alerting services in the DI container so the background monitor, health check runner, individual service checks, Pushover notifier, and named HttpClients are properly wired at application startup.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Program.cs` | Added health check DI section: `HealthCheckOptions` binding from env vars and config section, all `IServiceHealthCheck` implementations, `IHealthCheckRunner`, `IPushoverNotifier`, named HttpClients (Pushover, LemonSqueezy, SendGrid, Solver), and `HealthCheckMonitorService` as hosted service |
| `apps/api/Jobuler.Api/appsettings.json` | Added `HealthCheck` configuration section with default values for local development |
| `apps/api/Jobuler.Infrastructure/HealthChecks/HealthCheckMonitorService.cs` | Refactored constructor to use `IServiceScopeFactory` instead of directly injecting `IHealthCheckRunner` (required because the runner depends on scoped services like `AppDbContext`) |

## Key decisions

1. **IServiceScopeFactory pattern** — The `HealthCheckMonitorService` (singleton hosted service) cannot directly inject scoped services. Refactored to create a scope per check cycle, matching the existing `SolverWorkerService` pattern.
2. **Environment variable precedence** — Config section (`HealthCheck:*`) provides defaults; environment variables (`PUSHOVER_USER_KEY`, etc.) override them. This follows the existing VAPID/LemonSqueezy pattern.
3. **Scoped health checks** — Registered as scoped because `PostgresHealthCheck` depends on `AppDbContext` (scoped). The runner is also scoped since it aggregates them.
4. **Singleton PushoverNotifier** — Only depends on `IHttpClientFactory` and `IOptions<T>` (both singleton-safe), so registered as singleton for efficiency.
5. **Named HttpClients** — Solver client gets a base address from config; others use full URLs in their implementations.

## How it connects

- Depends on: All health check implementations (tasks 2.x, 3.1, 4.1, 6.1), interfaces (task 1.1)
- Enables: The `/health/detailed` endpoint (task 7.1) and background monitoring (task 6.1) to function at runtime
- The `HealthController` can now resolve `IHealthCheckRunner` from DI

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with 0 errors. The application will start with health monitoring active when deployed with the appropriate environment variables.

## What comes next

- Task 7.3: Integration tests for health endpoints
- Task 8.1: Update `.env.example` and `docker-compose.yml` with new environment variables

## Git commit

```bash
git add -A && git commit -m "feat(health): register DI services and configure health check options"
```
