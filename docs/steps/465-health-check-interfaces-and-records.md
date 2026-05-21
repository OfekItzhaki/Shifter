# 465 — Health Check Interfaces and Records

## Phase

Health Check Alerts — Application Layer Contracts

## Purpose

Define the Application layer interfaces and data models for the health check monitoring and alerting system. These contracts establish the boundaries between the Application and Infrastructure layers, allowing individual service health checks, a runner that aggregates results, Pushover notification delivery, and configuration options to be implemented independently.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Common/HealthChecks/IServiceHealthCheck.cs` | Interface for individual service health checks with `ServiceName` property and `CheckAsync` method |
| `apps/api/Jobuler.Application/Common/HealthChecks/ServiceHealthResult.cs` | Record representing the result of a single health check (ServiceName, Status, ErrorMessage, ResponseTime) |
| `apps/api/Jobuler.Application/Common/HealthChecks/IHealthCheckRunner.cs` | Interface for the aggregator that runs all checks and produces a report |
| `apps/api/Jobuler.Application/Common/HealthChecks/HealthCheckReport.cs` | Record representing the full health report (OverallStatus, Version, Timestamp, Checks) |
| `apps/api/Jobuler.Application/Common/HealthChecks/IPushoverNotifier.cs` | Interface for sending Pushover push notifications on service failure |
| `apps/api/Jobuler.Application/Common/HealthChecks/HealthCheckOptions.cs` | Configuration class for Pushover credentials, polling interval, and alert cooldown |

## Key decisions

- **Placed under `Common/HealthChecks/` subfolder** — keeps health check contracts grouped together without polluting the flat `Common/` namespace.
- **Records for immutable data** — `ServiceHealthResult` and `HealthCheckReport` are C# records since they represent immutable snapshots of health state.
- **Class for options** — `HealthCheckOptions` is a mutable class to work with the `IOptions<T>` binding pattern.
- **Nullable optional fields** — `ErrorMessage` and `ResponseTime` on `ServiceHealthResult` are nullable since healthy/skipped services don't have errors or timing.
- **No external dependencies** — all types use only BCL types (`string`, `DateTime`, `TimeSpan`, `IReadOnlyList<T>`), keeping the Application layer clean.

## How it connects

- Infrastructure layer will implement `IServiceHealthCheck` for each monitored service (PostgreSQL, Redis, LemonSqueezy, SendGrid, Solver).
- Infrastructure layer will implement `IHealthCheckRunner` to aggregate checks with timeout handling.
- Infrastructure layer will implement `IPushoverNotifier` for push notification delivery.
- The API layer's `HealthController` will depend on `IHealthCheckRunner` for the `/health/detailed` endpoint.
- `HealthCheckOptions` will be bound from environment variables via `IOptions<HealthCheckOptions>`.

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no new errors.

## What comes next

- Task 2.x: Implement individual service health checks in the Infrastructure layer
- Task 3.1: Implement `HealthCheckRunner` with timeout handling
- Task 4.1: Implement `PushoverNotifier`

## Git commit

```bash
git add -A && git commit -m "feat(health): define Application layer health check interfaces and records"
```
