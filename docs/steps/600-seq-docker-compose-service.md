# 600 — Seq Docker Compose Service

## Phase

Centralized Logging — Infrastructure

## Purpose

Adds a Seq structured log server container to the Docker Compose stack so that the platform has a centralized destination for structured log events. This is the foundational infrastructure piece that all other logging tasks depend on.

## What was built

| File | Change |
|------|--------|
| `infra/compose/docker-compose.yml` | Added `seq` service definition with `datalust/seq:latest` image, `ACCEPT_EULA` and `SEQ_FIRSTRUN_ADMINPASSWORD` environment variables, internal-only port exposure (5341 ingestion, 80 web UI), `seq_data:/data` named volume, health check, and `unless-stopped` restart policy. Added `seq_data` to the named volumes section. |

## Key decisions

- **`expose` instead of `ports`**: Ports 5341 and 80 are only exposed on the internal Docker network. No host port mapping ensures Seq is never directly accessible from the public internet — external access will go through the Caddy reverse proxy (task 2.2).
- **Named volume `seq_data`**: Persists Seq's indexed log data and configuration across container restarts and redeployments.
- **Health check with `curl`**: Uses `curl -f http://localhost:5341/health` to verify the ingestion endpoint is responsive. Includes `start_period: 15s` to give Seq time to initialize on first run.
- **No `depends_on` from other services**: Seq is non-critical infrastructure — if it fails to start, the API, solver, and web services continue running normally. Log events are buffered by the Serilog durable sink (configured in a later task).
- **Default network placement**: No explicit network configuration means Seq joins the default Compose network, allowing any future container to reach it at `http://seq:5341`.

## How it connects

- **Upstream**: The API service (task 4.4) will add `Seq__ServerUrl=http://seq:5341` to its environment, pointing the Serilog Seq sink at this container.
- **Downstream**: The Caddy reverse proxy (task 2.2) will forward authenticated requests to `seq:80` for the web UI.
- **Health check monitor**: The existing `HealthCheckMonitorService` inside the API container can reach Seq at `http://seq:5341` over the internal network without authentication.

## How to run / verify

```bash
cd infra/compose
docker compose config   # Validates the YAML syntax and variable interpolation
docker compose up seq   # Starts only the Seq service (requires SEQ_ADMIN_PASSWORD in .env)
curl http://localhost:5341/health  # Only works from inside the Docker network or if temporarily mapped
```

## What comes next

- Task 1.2: Add `SEQ_ADMIN_PASSWORD` and related variables to `.env.example`
- Task 2.1/2.2: Caddy reverse proxy for authenticated external access to the Seq web UI
- Task 4.4: Wire the API service to send logs to `http://seq:5341`

## Git commit

```bash
git add -A && git commit -m "feat(logging): add Seq structured log server to Docker Compose stack"
```
