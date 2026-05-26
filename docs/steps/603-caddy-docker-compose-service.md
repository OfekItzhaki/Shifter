# Step 603 — Caddy Reverse Proxy Docker Compose Service

## Phase

Infrastructure — Centralized Logging

## Purpose

Adds a Caddy reverse proxy sidecar to the Docker Compose stack that terminates TLS and enforces HTTP Basic Authentication before forwarding requests to the Seq web UI. This ensures the Seq log viewer is never exposed directly to the public internet without authentication.

## What was built

| File | Change |
|------|--------|
| `infra/compose/docker-compose.yml` | Added `caddy` service using `caddy:2-alpine` image with Caddyfile mount, port 80/443 mapping, environment variables for Basic Auth credentials, `depends_on: seq`, and `restart: unless-stopped` policy. Added `caddy_data` and `caddy_config` named volumes for TLS certificate persistence. |

## Key decisions

- **Port 80 and 443 mapped**: Port 80 is needed for Caddy to handle HTTP→HTTPS redirects and ACME (Let's Encrypt) challenges. Port 443 serves the authenticated Seq UI over HTTPS.
- **Caddyfile mounted read-only**: The Caddyfile is mounted as `:ro` since Caddy doesn't need to modify it at runtime.
- **`caddy_data` and `caddy_config` volumes**: These persist TLS certificates and Caddy state across container restarts, avoiding unnecessary certificate re-issuance.
- **`depends_on: seq`**: Caddy needs Seq to be running to proxy requests to it. Only Caddy depends on Seq — other services (API, web, solver) do not.
- **Environment variables for credentials**: `SEQ_UI_USERNAME` and `SEQ_UI_PASSWORD_HASH` are passed from the `.env` file, keeping secrets out of source control.

## How it connects

- Depends on task 2.1 (Caddyfile creation at `infra/compose/caddy/Caddyfile`)
- Depends on task 1.1 (Seq service definition in docker-compose.yml)
- The Caddy service proxies to `seq:80` over the internal Docker network
- External admin users access `https://logs.shifter.ofeklabs.com` which routes through Caddy

## How to run / verify

```bash
cd infra/compose
docker compose config  # Validates the compose file syntax
docker compose up caddy --dry-run  # Verifies Caddy service can be resolved
```

## What comes next

- Task 4.3: Refactor Program.cs Serilog initialization
- Task 4.4: Add Seq__ServerUrl environment variable to API service
- Checkpoint 3: Validate full Docker Compose configuration

## Git commit

```bash
git add -A && git commit -m "feat(logging): add Caddy reverse proxy service to Docker Compose"
```
