# 605 — Seq__ServerUrl Environment Variable Override

## Phase

Infrastructure — Centralized Logging

## Purpose

Adds the `Seq__ServerUrl` environment variable to the API service in `docker-compose.yml`, allowing local development or deployment to override the Seq ingestion URL. This decouples the Serilog Seq sink configuration from the hardcoded `appsettings.json` value, enabling developers to point to a different Seq instance or disable the sink entirely by omitting the variable.

## What was built

| File | Change |
|------|--------|
| `infra/compose/docker-compose.yml` | Added `Seq__ServerUrl: "http://seq:5341"` to the API service environment section |

## Key decisions

- Used the `Seq__ServerUrl` key as specified in the task, following the .NET double-underscore convention for nested configuration keys.
- The value `http://seq:5341` uses the Docker internal DNS name, ensuring the API communicates with Seq over the internal network without going through the reverse proxy.
- Placed the variable after the existing `HealthCheck__*` variables to group infrastructure-related config together.

## How it connects

- **Requirement 2.3**: The Serilog configuration resolves the Seq ingestion URL from an environment variable so the value is not hardcoded.
- **Requirement 4.4**: The API container communicates with Seq using the Docker internal DNS name, bypassing the reverse proxy.
- The `appsettings.json` Seq sink configuration (task 4.2) defines the default `serverUrl`; this environment variable overrides it at runtime.

## How to run / verify

```bash
cd infra/compose
docker compose config | grep -A 2 "Seq__ServerUrl"
```

Expected output should show `Seq__ServerUrl: http://seq:5341` in the API service environment.

## What comes next

- Task 6.1: Verify deployment workflow compatibility
- Task 6.2: Write smoke test script to validate configuration files

## Git commit

```bash
git add -A && git commit -m "feat(logging): add Seq__ServerUrl env override to API service in docker-compose"
```
