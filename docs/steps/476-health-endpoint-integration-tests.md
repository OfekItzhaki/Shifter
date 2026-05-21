# 476 — Health Endpoint Integration Tests

## Phase

Phase 8 — Health Check Alerts (Observability & Alerting)

## Purpose

Validates that the `/health/detailed` and `/health` endpoints behave correctly at the controller level — returning the expected JSON structure, HTTP status codes, and remaining accessible without authentication. These integration tests ensure backward compatibility of the existing `/health` endpoint while verifying the new `/health/detailed` endpoint meets its requirements.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/HealthChecks/HealthEndpointIntegrationTests.cs` | 12 integration tests covering both health endpoints |

## Key decisions

1. **Controller-level testing** — Follows the project's existing integration test pattern (direct controller instantiation with mocked dependencies) rather than WebApplicationFactory, since the test project doesn't include `Microsoft.AspNetCore.Mvc.Testing`.

2. **Mocked IHealthCheckRunner for /health/detailed** — The detailed endpoint delegates to `IHealthCheckRunner`, so mocking it with NSubstitute gives full control over test scenarios (all healthy, degraded, mixed statuses).

3. **In-memory EF Core limitation acknowledged** — The `/health` endpoint uses `ExecuteSqlRawAsync("SELECT 1")` which isn't supported by the in-memory provider. Tests are written to verify structure and degraded behavior rather than the healthy path for PostgreSQL specifically.

4. **Attribute-based auth verification** — Tests verify `[AllowAnonymous]` is present on the controller class via reflection, confirming both endpoints are accessible without authentication.

## How it connects

- Tests the `HealthController` (task 7.1) with the `IHealthCheckRunner` interface (task 1.1) and `HealthCheckReport` model
- Validates requirements 1.1, 1.2, 1.3, 1.6 (detailed endpoint) and 7.1, 7.2, 7.3 (backward compatibility)
- Complements the property-based tests in `HealthCheckRunnerPropertyTests.cs` which test the runner's aggregation logic

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~HealthEndpointIntegrationTests"
```

All 12 tests should pass.

## What comes next

- Task 8.1: Update `.env.example` and `docker-compose.yml` with health check environment variables
- Final checkpoint (task 9) to ensure all health-check-alerts tests pass together

## Git commit

```bash
git add -A && git commit -m "feat(health): add integration tests for health endpoints (task 7.3)"
```
