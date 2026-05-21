# 470 — Health Detailed Endpoint

## Phase

Health Check Alerts feature

## Purpose

Adds the `/health/detailed` endpoint to the existing `HealthController`, providing operators with per-service health status for all monitored services (PostgreSQL, Redis, LemonSqueezy, SendGrid, Solver). This endpoint returns HTTP 200 when all services are healthy and HTTP 503 when any service is degraded.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/HealthController.cs` | Injected `IHealthCheckRunner` into the controller constructor and added the `Detailed()` action method mapped to `GET /health/detailed` |

## Key decisions

- **Reuse existing controller** — The new endpoint is added as a new action on the existing `HealthController` rather than creating a new controller, keeping health-related endpoints co-located.
- **Delegate to IHealthCheckRunner** — The endpoint delegates all health check logic to the `IHealthCheckRunner` abstraction (implemented in Infrastructure layer), keeping the controller thin.
- **Status code mapping** — Returns 200 for "healthy" overall status and 503 for "degraded", matching the existing `/health` endpoint pattern.
- **No authentication** — The controller already has `[AllowAnonymous]`, so the new endpoint inherits this attribute.

## How it connects

- Depends on `IHealthCheckRunner` interface (defined in Application layer, task 1.1)
- Depends on `HealthCheckRunner` implementation (Infrastructure layer, task 3.1)
- Will be wired via DI registration (task 7.2)
- Integration tests will verify the endpoint behavior (task 7.3)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.sln
```

The endpoint will be fully functional once DI registration is completed in task 7.2.

## What comes next

- Task 7.2: Register DI services and configure options (wires `IHealthCheckRunner` into the DI container)
- Task 7.3: Integration tests for the health endpoints

## Git commit

```bash
git add -A && git commit -m "feat(health): add /health/detailed endpoint to HealthController"
```
