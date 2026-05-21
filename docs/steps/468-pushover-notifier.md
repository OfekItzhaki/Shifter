# 468 — Pushover Notifier

## Phase

Phase: Health Check Alerts — Notification Integration

## Purpose

Implements the `IPushoverNotifier` interface to send high-priority push notifications to the platform operator via the Pushover API when a monitored service transitions to unhealthy. This is the alerting mechanism that enables real-time outage awareness.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/HealthChecks/PushoverNotifier.cs` | Implementation of `IPushoverNotifier` that POSTs to the Pushover API with high-priority alerts |

## Key decisions

- **FormUrlEncodedContent** — Pushover API expects form-encoded POST bodies, not JSON.
- **Named HttpClient "Pushover"** — Uses `IHttpClientFactory` with a named client for proper lifecycle management and testability.
- **Graceful degradation** — When `PushoverUserKey` or `PushoverAppToken` are missing, logs a warning and returns without throwing. The monitor continues running without alerting.
- **No retry on failure** — If the Pushover API returns a non-success status or throws, the error is logged and the method returns. Retries are intentionally omitted to avoid blocking the health check cycle.
- **Message format** — Includes service name and UTC timestamp in a human-readable format: `Service '{name}' is unhealthy. Detected at {timestamp} UTC.`

## How it connects

- Implements `IPushoverNotifier` defined in `Jobuler.Application/Common/HealthChecks/`
- Will be called by `HealthCheckMonitorService` (task 6.1) on healthy→unhealthy transitions
- Reads credentials from `IOptions<HealthCheckOptions>` (configured via environment variables)
- Named client "Pushover" will be registered in DI (task 7.2)

## How to run / verify

```bash
cd apps/api/Jobuler.Infrastructure
dotnet build --no-restore
```

Unit tests for this component are covered in task 4.2.

## What comes next

- Task 4.2: Unit tests for PushoverNotifier
- Task 6.1: HealthCheckMonitorService that calls this notifier on state transitions
- Task 7.2: DI registration of the named HttpClient and service binding

## Git commit

```bash
git add -A && git commit -m "feat(health): implement PushoverNotifier for health alert notifications"
```
