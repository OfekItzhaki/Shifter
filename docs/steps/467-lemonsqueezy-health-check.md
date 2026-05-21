# Step 467 — LemonSqueezy Health Check

## Phase

Health Check Alerts — Individual Service Health Checks

## Purpose

Implements the LemonSqueezy API health check that validates the API key by making an authenticated GET request to `/v1/users/me`. This allows the health monitoring system to detect when the LemonSqueezy billing integration is misconfigured or the API is unreachable.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/HealthChecks/LemonSqueezyHealthCheck.cs` | Implements `IServiceHealthCheck` for LemonSqueezy. Uses `IHttpClientFactory` named client, authenticates with Bearer token from `LemonSqueezySettings.ApiKey`, and reports healthy/unhealthy with response time. |

## Key decisions

- **Reuses existing `LemonSqueezySettings`** — The API key is already configured via `IOptions<LemonSqueezySettings>` in the billing integration. The health check injects the same settings rather than introducing a separate environment variable.
- **Named HTTP client via `IHttpClientFactory`** — Follows the design doc's specification for using a named client ("LemonSqueezy"), enabling testability and proper HttpClient lifecycle management.
- **GET /v1/users/me** — This is the lightest authenticated endpoint on the LemonSqueezy API, suitable for validating the API key without side effects.
- **Lets `OperationCanceledException` propagate** — When the cancellation token fires (e.g., from the 10-second timeout in `HealthCheckRunner`), the exception propagates up so the runner can mark it as unhealthy due to timeout.

## How it connects

- Implements `IServiceHealthCheck` defined in `Jobuler.Application/Common/HealthChecks/`
- Will be registered in DI and consumed by `HealthCheckRunner` (task 3.1)
- Depends on `LemonSqueezySettings` already configured in the billing DI registration (step 428)
- Sits alongside `PostgresHealthCheck`, `RedisHealthCheck`, `SendGridHealthCheck`, and `SolverHealthCheck`

## How to run / verify

```bash
dotnet build apps/api/Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
```

The health check will be exercised once `HealthCheckRunner` and the `/health/detailed` endpoint are wired up (tasks 3.1 and 7.1).

## What comes next

- Task 2.4: `SendGridHealthCheck` implementation
- Task 2.5: `SolverHealthCheck` implementation
- Task 3.1: `HealthCheckRunner` that aggregates all checks with timeout handling

## Git commit

```bash
git add -A && git commit -m "feat(health): implement LemonSqueezy health check"
```
