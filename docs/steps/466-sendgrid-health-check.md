# Step 466 — SendGrid Health Check

## Phase

Health Check Alerts — Individual Service Health Checks

## Purpose

Implements the SendGrid connectivity health check that validates the API key by making an authenticated GET request to the SendGrid API. Returns "skipped" when the API key is not configured, supporting the optional service pattern.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/HealthChecks/SendGridHealthCheck.cs` | Health check implementation that calls `GET /v3/user/profile` with Bearer token authentication. Returns "skipped" if `SendGrid:ApiKey` is not configured, "healthy" on success, "unhealthy" with error details on failure. |

## Key decisions

- **Reuses existing configuration path** — reads `SendGrid:ApiKey` from `IConfiguration`, consistent with `SendGridEmailSender`.
- **Uses IHttpClientFactory named client** — creates a "SendGrid" named client for proper HttpClient lifecycle management.
- **Skipped vs unhealthy** — when the API key is not configured, the check returns "skipped" (not "unhealthy") since SendGrid is an optional service.
- **Response time measurement** — uses `Stopwatch` to measure and report the check duration.

## How it connects

- Implements `IServiceHealthCheck` from the Application layer (created in step 465).
- Will be registered in DI and consumed by `HealthCheckRunner` (task 3.1).
- Follows the same pattern as other service health checks (PostgreSQL, Redis, LemonSqueezy, Solver).

## How to run / verify

```bash
cd apps/api/Jobuler.Infrastructure
dotnet build --no-restore
```

## What comes next

- Implement `SolverHealthCheck` (task 2.5)
- Implement `HealthCheckRunner` that aggregates all checks (task 3.1)
- Register all health checks in DI (task 7.2)

## Git commit

```bash
git add -A && git commit -m "feat(health): implement SendGrid health check with skip-when-unconfigured"
```
