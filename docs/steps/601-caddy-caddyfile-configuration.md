# 601 — Caddy Caddyfile Configuration

## Phase

Centralized Logging — Infrastructure

## Purpose

Create the Caddy reverse proxy configuration file that secures the Seq web UI behind HTTP Basic Authentication and TLS. This ensures unauthorized users cannot access application logs while allowing authenticated administrators to view and query structured log events at `logs.shifter.ofeklabs.com`.

## What was built

| File | Description |
|------|-------------|
| `infra/compose/caddy/Caddyfile` | Caddy site block for `logs.shifter.ofeklabs.com` with `basicauth` and `reverse_proxy` directives |

## Key decisions

- **Environment variable placeholders for credentials**: `{$SEQ_UI_USERNAME}` and `{$SEQ_UI_PASSWORD_HASH}` are used instead of hardcoded values, following the security rules that secrets must never be hardcoded in source control.
- **Wildcard path matcher (`*`)**: Basic auth applies to all paths under the domain, ensuring no unauthenticated access to any part of the Seq UI.
- **`reverse_proxy seq:80`**: Forwards authenticated requests to the Seq web UI on the internal Docker network using the service DNS name.
- **Caddy handles TLS automatically**: Caddy's built-in ACME integration obtains and renews Let's Encrypt certificates for the domain without additional configuration.

## How it connects

- The Caddyfile is mounted into the Caddy Docker service (task 2.2) via a volume bind.
- The `SEQ_UI_USERNAME` and `SEQ_UI_PASSWORD_HASH` environment variables are defined in `.env.example` (task 1.2) and populated in the VPS `.env` file.
- The `seq:80` target refers to the Seq service defined in `docker-compose.yml` (task 1.1) on the internal Docker network.
- Satisfies Requirements 3.2 (TLS termination + forwarding), 3.3 (HTTP 401 for unauthenticated requests), 3.4 (Basic Auth mechanism), 3.5 (credentials via env vars), 3.6 (subdomain access).

## How to run / verify

1. Generate a bcrypt password hash: `docker run --rm caddy:2-alpine caddy hash-password --plaintext 'your-password'`
2. Set `SEQ_UI_USERNAME` and `SEQ_UI_PASSWORD_HASH` in `infra/compose/.env`
3. After task 2.2 adds the Caddy service to docker-compose, run `docker compose up caddy`
4. Verify `https://logs.shifter.ofeklabs.com` prompts for credentials (HTTP 401 without auth)
5. Verify authenticated requests show the Seq web UI

## What comes next

- Task 2.2: Add the Caddy service to `docker-compose.yml` that mounts this Caddyfile and exposes port 443.

## Git commit

```bash
git add -A && git commit -m "feat(logging): add Caddy Caddyfile for Seq web UI reverse proxy"
```
