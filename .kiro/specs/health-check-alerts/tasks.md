# Implementation Plan: Health Check Alerts

## Overview

This plan implements a comprehensive health monitoring and alerting system for the Shifter API. It adds per-service health checks (PostgreSQL, Redis, LemonSqueezy, SendGrid, Solver), a detailed health endpoint, a background monitor with state-transition detection, and Pushover push notifications for outage alerts. The implementation follows the existing 4-layer architecture with interfaces in Application, implementations in Infrastructure, and the endpoint on the existing HealthController.

## Tasks

- [x] 1. Define Application layer interfaces and models
  - [x] 1.1 Create health check interfaces and records
    - Create `Jobuler.Application/Common/HealthChecks/IServiceHealthCheck.cs` with `ServiceName` property and `CheckAsync` method
    - Create `Jobuler.Application/Common/HealthChecks/ServiceHealthResult.cs` record with ServiceName, Status, ErrorMessage, ResponseTime
    - Create `Jobuler.Application/Common/HealthChecks/IHealthCheckRunner.cs` with `RunAllAsync` method
    - Create `Jobuler.Application/Common/HealthChecks/HealthCheckReport.cs` record with OverallStatus, Version, Timestamp, Checks
    - Create `Jobuler.Application/Common/HealthChecks/IPushoverNotifier.cs` with `SendAlertAsync` method
    - Create `Jobuler.Application/Common/HealthChecks/HealthCheckOptions.cs` with PushoverUserKey, PushoverAppToken, IntervalSeconds (default 300), AlertCooldownSeconds (default 3600)
    - _Requirements: 1.1, 1.4, 5.1, 5.4, 5.5, 6.1, 6.2, 6.3_

- [x] 2. Implement individual service health checks
  - [x] 2.1 Implement PostgresHealthCheck
    - Create `Jobuler.Infrastructure/HealthChecks/PostgresHealthCheck.cs` implementing `IServiceHealthCheck`
    - Execute `SELECT 1` via `AppDbContext` to verify connectivity
    - Return "healthy" on success, "unhealthy" with error message on failure
    - Measure and include response time
    - _Requirements: 2.1_

  - [x] 2.2 Implement RedisHealthCheck
    - Create `Jobuler.Infrastructure/HealthChecks/RedisHealthCheck.cs` implementing `IServiceHealthCheck`
    - Execute PING via `IConnectionMultiplexer`
    - Return "healthy" on success, "unhealthy" with error message on failure
    - Measure and include response time
    - _Requirements: 2.2_

  - [x] 2.3 Implement LemonSqueezyHealthCheck
    - Create `Jobuler.Infrastructure/HealthChecks/LemonSqueezyHealthCheck.cs` implementing `IServiceHealthCheck`
    - Make authenticated GET request to LemonSqueezy API to validate the API key
    - Use `IHttpClientFactory` named client
    - Return "healthy" on success, "unhealthy" with error message on failure
    - _Requirements: 2.3_

  - [x] 2.4 Implement SendGridHealthCheck
    - Create `Jobuler.Infrastructure/HealthChecks/SendGridHealthCheck.cs` implementing `IServiceHealthCheck`
    - Make authenticated GET request to SendGrid API to validate the API key
    - Return "skipped" when SendGrid API key is not configured
    - Return "healthy" on success, "unhealthy" with error message on failure
    - _Requirements: 2.4, 1.5_

  - [x] 2.5 Implement SolverHealthCheck
    - Create `Jobuler.Infrastructure/HealthChecks/SolverHealthCheck.cs` implementing `IServiceHealthCheck`
    - Make HTTP GET request to Solver service base URL to verify reachability
    - Use `IHttpClientFactory` named client
    - Return "healthy" on success, "unhealthy" with error message on failure
    - _Requirements: 2.5_

- [x] 3. Implement HealthCheckRunner with timeout handling
  - [x] 3.1 Create HealthCheckRunner
    - Create `Jobuler.Infrastructure/HealthChecks/HealthCheckRunner.cs` implementing `IHealthCheckRunner`
    - Inject all `IServiceHealthCheck` instances via DI
    - Run each check with a 10-second `CancellationTokenSource` timeout
    - Catch exceptions and mark timed-out/failed checks as "unhealthy"
    - Derive overall status: "healthy" if all non-skipped checks pass, "degraded" otherwise
    - Include application version and UTC timestamp in the report
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.6_

  - [ ]* 3.2 Write property tests for HealthCheckRunner (Properties 1, 2, 3)
    - **Property 1: Response structure completeness** — For any set of service health check results, the report contains an entry for every registered service, a UTC timestamp, and the version string
    - **Validates: Requirements 1.1, 1.4**
    - **Property 2: Overall status derivation** — For any set of results, overall status is "healthy" iff all non-skipped services report "healthy"; otherwise "degraded"
    - **Validates: Requirements 1.2, 1.3**
    - **Property 3: Timeout marks service unhealthy** — For any check exceeding 10s timeout, the resulting status is "unhealthy"
    - **Validates: Requirements 2.6**
    - Create `Jobuler.Tests/HealthChecks/HealthCheckRunnerPropertyTests.cs`
    - Use FsCheck.Xunit with minimum 100 iterations per property
    - Mock `IServiceHealthCheck` instances with generated results

- [x] 4. Implement PushoverNotifier
  - [x] 4.1 Create PushoverNotifier
    - Create `Jobuler.Infrastructure/HealthChecks/PushoverNotifier.cs` implementing `IPushoverNotifier`
    - Use `IHttpClientFactory` named client "Pushover"
    - POST to `https://api.pushover.net/1/messages.json` with token, user, message, priority=1, title="Shifter Health Alert"
    - Include service name and UTC timestamp in the notification message
    - If credentials missing: log warning, return without sending (no-op)
    - If request fails: log error at Error level, do not retry
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

  - [ ]* 4.2 Write unit tests for PushoverNotifier
    - Create `Jobuler.Tests/HealthChecks/PushoverNotifierTests.cs`
    - Test request body format includes token, user, message, priority=1
    - Test notification message contains service name and UTC timestamp
    - Test graceful degradation when credentials are missing (logs warning, no exception)
    - Test error logging when Pushover API returns non-success status
    - _Requirements: 5.1, 5.2, 5.3, 5.6, 5.7_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement HealthCheckMonitorService (background service)
  - [x] 6.1 Create HealthCheckMonitorService
    - Create `Jobuler.Infrastructure/HealthChecks/HealthCheckMonitorService.cs` extending `BackgroundService`
    - Inject `IHealthCheckRunner`, `IPushoverNotifier`, `IOptions<HealthCheckOptions>`, `ILogger`
    - Maintain `ConcurrentDictionary<string, ServiceState>` for in-memory state tracking
    - On startup: perform initial health check within 30 seconds (random 5-30s delay)
    - Loop: run checks at configured interval
    - On healthy→unhealthy transition: call `IPushoverNotifier.SendAlertAsync` (respecting cooldown)
    - On unhealthy→healthy transition: log recovery at Information level, reset cooldown for that service
    - Enforce cooldown per service: suppress duplicate alerts within cooldown window
    - On recovery then re-failure: reset cooldown and send new alert immediately
    - Clamp interval to minimum 30 seconds if configured below, log warning
    - Catch unhandled exceptions in outer loop: log Error, continue to next cycle
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 4.1, 4.2, 4.3, 4.4, 6.4_

  - [ ]* 6.2 Write property tests for HealthCheckMonitorService state machine (Properties 4, 5, 7, 8)
    - **Property 4: State transition triggers alert with correct content** — For any service transitioning healthy→unhealthy, a Pushover notification is sent containing the service name and UTC timestamp
    - **Validates: Requirements 3.4, 5.3**
    - **Property 5: Recovery logs at Information level** — For any service transitioning unhealthy→healthy, an Information-level log entry is produced containing the service name
    - **Validates: Requirements 3.5**
    - **Property 7: Cooldown suppresses duplicate alerts** — For any service remaining continuously unhealthy, at most one alert is sent per cooldown period
    - **Validates: Requirements 4.1, 4.3**
    - **Property 8: Recovery resets cooldown** — For any service transitioning unhealthy→healthy→unhealthy, the second unhealthy transition triggers a new alert immediately
    - **Validates: Requirements 4.4**
    - Create `Jobuler.Tests/HealthChecks/HealthCheckMonitorPropertyTests.cs`
    - Use FsCheck.Xunit with minimum 100 iterations per property
    - Generate sequences of service states and timing to test state machine logic

  - [ ]* 6.3 Write property test for exception resilience (Property 6)
    - **Property 6: Exception resilience** — For any exception thrown during a health check cycle, the monitor catches it, logs at Error level, and continues executing subsequent cycles
    - **Validates: Requirements 3.7**
    - Add to `Jobuler.Tests/HealthChecks/HealthCheckMonitorPropertyTests.cs`
    - Generate random exceptions and verify the monitor continues running

  - [ ]* 6.4 Write property test for interval clamping (Property 9)
    - **Property 9: Interval clamping** — For any configured interval value less than 30, the effective polling interval is clamped to 30 seconds
    - **Validates: Requirements 6.4**
    - Create `Jobuler.Tests/HealthChecks/HealthCheckOptionsPropertyTests.cs`
    - Use FsCheck.Xunit to generate integer values and verify clamping behavior

- [x] 7. Add detailed health endpoint and wire DI
  - [x] 7.1 Add `/health/detailed` endpoint to HealthController
    - Add new action method `Detailed` on existing `HealthController`
    - Inject `IHealthCheckRunner` into the controller
    - Return HTTP 200 with report when overall status is "healthy"
    - Return HTTP 503 with report when overall status is "degraded"
    - Endpoint must be accessible without authentication (already `[AllowAnonymous]` on controller)
    - _Requirements: 1.1, 1.2, 1.3, 1.6_

  - [x] 7.2 Register DI services and configure options
    - Register `HealthCheckOptions` from environment variables via `IOptions<T>` pattern
    - Register all `IServiceHealthCheck` implementations
    - Register `IHealthCheckRunner` → `HealthCheckRunner`
    - Register `IPushoverNotifier` → `PushoverNotifier`
    - Register `HealthCheckMonitorService` as hosted service
    - Register named `HttpClient` instances for Pushover, LemonSqueezy, SendGrid, Solver
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [ ]* 7.3 Write integration tests for health endpoints
    - Create `Jobuler.Tests/HealthChecks/HealthEndpointIntegrationTests.cs`
    - Test `/health/detailed` returns correct JSON structure with all service entries
    - Test `/health/detailed` returns 200 when all healthy, 503 when degraded
    - Test `/health/detailed` is accessible without authentication
    - Test `/health` endpoint backward compatibility (regression test)
    - _Requirements: 1.1, 1.2, 1.3, 1.6, 7.1, 7.2, 7.3_

- [x] 8. Update environment configuration and documentation
  - [x] 8.1 Update .env.example and docker-compose
    - Add `PUSHOVER_USER_KEY`, `PUSHOVER_APP_TOKEN`, `HEALTH_CHECK_INTERVAL_SECONDS`, `HEALTH_CHECK_ALERT_COOLDOWN_SECONDS` to `.env.example` with placeholder values
    - Add environment variable references to `docker-compose.yml` if needed
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The existing `/health` endpoint is NOT modified — only a new `/health/detailed` action is added to the same controller
- All secrets (Pushover keys) come from environment variables, never hardcoded
- The background monitor runs independently of the web layer and gracefully degrades if Pushover is unconfigured

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5"] },
    { "id": 2, "tasks": ["3.1", "4.1"] },
    { "id": 3, "tasks": ["3.2", "4.2"] },
    { "id": 4, "tasks": ["6.1"] },
    { "id": 5, "tasks": ["6.2", "6.3", "6.4"] },
    { "id": 6, "tasks": ["7.1", "7.2"] },
    { "id": 7, "tasks": ["7.3", "8.1"] }
  ]
}
```
