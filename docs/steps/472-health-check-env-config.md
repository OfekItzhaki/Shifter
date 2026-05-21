# 472 — Health Check Environment Configuration

## Phase

Health Check Alerts — Environment Configuration

## Purpose

Adds the health check and Pushover alerting environment variables to `.env.example` and `docker-compose.yml` so that operators can configure the monitoring system without reading source code. This ensures the new health check feature is discoverable and properly passed through to the API container.

## What was built

| File | Change |
|------|--------|
| `infra/compose/.env.example` | Added `PUSHOVER_USER_KEY`, `PUSHOVER_APP_TOKEN`, `HEALTH_CHECK_INTERVAL_SECONDS`, `HEALTH_CHECK_ALERT_COOLDOWN_SECONDS` with descriptive comments and placeholder/default values |
| `infra/compose/docker-compose.yml` | Added `HealthCheck__PushoverUserKey`, `HealthCheck__PushoverAppToken`, `HealthCheck__IntervalSeconds`, `HealthCheck__AlertCooldownSeconds` environment variable mappings to the `api` service |

## Key decisions

- Used the `HealthCheck__` prefix in docker-compose to match ASP.NET Core's configuration section binding (maps to `HealthCheck:PushoverUserKey` etc.)
- Provided sensible defaults in docker-compose (`300` for interval, `3600` for cooldown) so the system works out of the box without explicit configuration
- Left Pushover keys empty by default — the system gracefully degrades (logs only) when unconfigured
- Placed the new section after LemonSqueezy in `.env.example` to group external service credentials together

## How it connects

- The `HealthCheckOptions` class in the Application layer binds from both the `HealthCheck` configuration section and direct environment variable reads
- The `HealthCheckMonitorService` and `PushoverNotifier` consume these options via `IOptions<HealthCheckOptions>`
- Docker-compose passes the values through to the API container where ASP.NET Core's configuration system picks them up

## How to run / verify

1. Check `.env.example` contains the new variables with comments
2. Check `docker-compose.yml` has the new env vars in the `api` service
3. Copy `.env.example` to `.env`, fill in Pushover credentials, and run `docker compose up` — the API should start with health monitoring enabled

## What comes next

- Final checkpoint to ensure all tests pass (Task 9)

## Git commit

```bash
git add -A && git commit -m "feat(health-check): add env vars to .env.example and docker-compose"
```
