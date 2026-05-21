# 468 — Health Check Runner

## Phase

Health Check Alerts — Infrastructure Layer

## Purpose

Implements the `IHealthCheckRunner` interface that aggregates all registered `IServiceHealthCheck` instances, runs each with a 10-second timeout, and produces a unified `HealthCheckReport`. This is the core orchestration component that both the `/health/detailed` endpoint and the background monitor will use to evaluate system health.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/HealthChecks/HealthCheckRunner.cs` | Implements `IHealthCheckRunner`. Iterates all injected `IServiceHealthCheck` instances, runs each with a 10-second `CancellationTokenSource` timeout, catches exceptions/timeouts marking them as "unhealthy", derives overall status ("healthy" if all non-skipped pass, "degraded" otherwise), and includes application version + UTC timestamp in the report. |

## Key decisions

- **Sequential execution** — Health checks run sequentially rather than in parallel to avoid overwhelming services during degraded states and to keep timeout handling straightforward.
- **Linked CancellationToken** — Uses `CancellationTokenSource.CreateLinkedTokenSource` to respect both the per-check 10-second timeout and the caller's cancellation token.
- **Internal `DeriveOverallStatus`** — Exposed as `internal static` so property-based tests can verify the derivation logic directly (the Infrastructure project has `InternalsVisibleTo` for `Jobuler.Tests`).
- **Assembly version fallback** — Uses `Assembly.GetEntryAssembly()?.GetName().Version?.ToString()` with a fallback to `"unknown"` when running in test contexts where no entry assembly exists.

## How it connects

- Depends on: `IServiceHealthCheck` (implemented by `PostgresHealthCheck`, `RedisHealthCheck`, `LemonSqueezyHealthCheck`, `SendGridHealthCheck`, `SolverHealthCheck`)
- Consumed by: `HealthController.Detailed()` endpoint (task 7.1) and `HealthCheckMonitorService` (task 6.1)
- Tested by: Property tests in task 3.2

## How to run / verify

```bash
cd apps/api/Jobuler.Infrastructure
dotnet build
```

The build should succeed with no errors in the new file.

## What comes next

- Task 3.2: Property-based tests for HealthCheckRunner (Properties 1, 2, 3)
- Task 7.1: Wire into the `/health/detailed` endpoint
- Task 7.2: Register in DI container

## Git commit

```bash
git add -A && git commit -m "feat(health): implement HealthCheckRunner with timeout and status derivation"
```
