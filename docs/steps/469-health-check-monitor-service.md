# 469 — Health Check Monitor Service

## Phase

Health Check Alerts — Background Monitoring

## Purpose

Implements the `HealthCheckMonitorService`, a `BackgroundService` that continuously polls service health at a configurable interval and sends Pushover notifications when a service transitions from healthy to unhealthy. This is the core state machine that ties together the health check runner and the Pushover notifier into an automated monitoring loop.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/HealthChecks/HealthCheckMonitorService.cs` | Background service with state-transition detection, cooldown enforcement, interval clamping, and exception resilience |

## Key decisions

- **In-memory state via `ConcurrentDictionary<string, ServiceState>`** — No database persistence needed; state rebuilds on restart with the first check establishing a baseline (no alert on unknown→unhealthy).
- **Recovery resets cooldown** — When a service goes healthy then unhealthy again, the `LastAlertSentUtc` is cleared on recovery, so the next failure triggers an immediate alert regardless of elapsed time.
- **Interval clamping** — Values below 30 seconds are clamped to 30 with a warning log, preventing accidental API abuse.
- **Exception resilience** — The outer loop catches all exceptions (except cancellation on shutdown) and logs at Error level, ensuring the monitor never crashes.
- **Random initial delay (5-30s)** — Prevents thundering herd on multi-instance deployments and satisfies the "within 30 seconds" startup requirement.
- **`InternalsVisibleTo` already configured** — The Infrastructure project exposes internals to `Jobuler.Tests`, allowing property tests to access `ServiceState` and `GetEffectiveInterval()`.

## How it connects

- Depends on `IHealthCheckRunner` (implemented by `HealthCheckRunner` in the same folder)
- Depends on `IPushoverNotifier` (implemented by `PushoverNotifier` in the same folder)
- Configured via `IOptions<HealthCheckOptions>` (defined in Application layer)
- Will be registered as a hosted service in the DI wiring task (7.2)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
```

The service will be fully testable via property-based tests in task 6.2/6.3/6.4.

## What comes next

- Task 6.2: Property tests for state machine (Properties 4, 5, 7, 8)
- Task 6.3: Property test for exception resilience (Property 6)
- Task 6.4: Property test for interval clamping (Property 9)
- Task 7.2: DI registration as hosted service

## Git commit

```bash
git add -A && git commit -m "feat(health-checks): implement HealthCheckMonitorService background service"
```
