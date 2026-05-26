# 600 — Seq Environment Variables

## Phase

Phase N — Centralized Logging Infrastructure

## Purpose

Add Seq-related environment variables to `.env.example` so that developers and operators know which secrets to configure for the centralized logging stack (Seq admin password, Caddy basic auth credentials for the Seq web UI).

## What was built

| File | Description |
|------|-------------|
| `infra/compose/.env.example` | Added `SEQ_ADMIN_PASSWORD`, `SEQ_UI_USERNAME`, `SEQ_UI_PASSWORD_HASH` with placeholder values and documentation comments |

## Key decisions

- Used `changeme_seq_admin` as the placeholder for `SEQ_ADMIN_PASSWORD` — clearly signals it must be replaced.
- Provided `admin` as the default username placeholder for `SEQ_UI_USERNAME`.
- Included a comment showing how to generate the bcrypt hash using the Caddy Docker image (`docker run --rm caddy:2-alpine caddy hash-password --plaintext 'your-password-here'`).
- Placed the section before Health Check Alerts to group infrastructure/observability variables together.

## How it connects

- The `SEQ_ADMIN_PASSWORD` variable is referenced by the Seq service in `docker-compose.yml` (task 1.1) via `SEQ_FIRSTRUN_ADMINPASSWORD`.
- The `SEQ_UI_USERNAME` and `SEQ_UI_PASSWORD_HASH` variables are consumed by the Caddy reverse proxy sidecar (task 2.1/2.2) for HTTP Basic Authentication.
- Satisfies Requirement 3.5: credentials configured via environment variables, never hardcoded.

## How to run / verify

1. Open `infra/compose/.env.example` and confirm the three new variables exist with placeholder values.
2. Copy `.env.example` to `.env` and replace placeholders with real values before running the stack.

## What comes next

- Task 2.1: Create Caddy configuration that references `SEQ_UI_USERNAME` and `SEQ_UI_PASSWORD_HASH`.
- Task 2.2: Add Caddy service to docker-compose.yml that passes these environment variables.

## Git commit

```bash
git add -A && git commit -m "feat(logging): add Seq environment variables to .env.example"
```
