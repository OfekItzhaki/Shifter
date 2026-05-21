# 467 — Solver Health Check

## Phase

Health Check Alerts — Service Health Checks

## Purpose

Implements the Solver service reachability health check. The Solver is a critical dependency (Python CP-SAT service) that must be monitored for availability. This check makes an HTTP GET request to the Solver base URL to verify the service is reachable.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/HealthChecks/SolverHealthCheck.cs` | `IServiceHealthCheck` implementation that uses `IHttpClientFactory` with a named "Solver" client to GET the Solver base URL and report healthy/unhealthy status |

## Key decisions

- **Named HttpClient ("Solver")**: Uses `IHttpClientFactory.CreateClient("Solver")` rather than injecting a typed `HttpClient`. The named client will be configured with the Solver base URL during DI registration (task 7.2), matching the pattern used by other health checks (LemonSqueezy, SendGrid).
- **GET / endpoint**: The design specifies `GET {SolverBaseUrl}/` as the reachability check. This is a lightweight probe — the Solver's root endpoint should respond quickly without triggering any computation.
- **Same pattern as other checks**: Follows the exact structure of `PostgresHealthCheck` and `RedisHealthCheck` — Stopwatch for response time, try/catch for error handling, returning `ServiceHealthResult`.

## How it connects

- Implements `IServiceHealthCheck` from the Application layer
- Will be registered in DI and collected by `HealthCheckRunner` (task 3.1)
- The named "Solver" HttpClient will be configured with `Configuration["Solver:BaseUrl"]` in task 7.2
- Reports to the `/health/detailed` endpoint alongside other service checks

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
```

The health check will be fully testable once DI wiring (task 7.2) registers the named HttpClient with the Solver base URL.

## What comes next

- Task 3.1: `HealthCheckRunner` aggregates all health checks including this one
- Task 7.2: DI registration wires the named "Solver" HttpClient with the correct base URL

## Git commit

```bash
git add -A && git commit -m "feat(health): implement SolverHealthCheck with named HttpClient"
```
