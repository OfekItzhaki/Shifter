# 606 — Deploy VPS Workflow Seq & Caddy Compatibility

## Phase

Centralized Logging — Deployment Integration

## Purpose

The existing `deploy-vps.yml` workflow explicitly named only `api`, `solver`, and `web` in the `docker compose up` command. Since `seq` and `caddy` are not dependencies of those services, they would never be started (and `--remove-orphans` could even remove them). This step fixes the deploy command to include the new logging infrastructure services.

## What was built

| File | Change |
|------|--------|
| `.github/workflows/deploy-vps.yml` | Added `seq caddy` to the `docker compose up -d --build` command so both services start on every deployment |

## Key decisions

- Added `seq` and `caddy` as explicit service names in the compose command rather than switching to a blanket `docker compose up -d --build` (which would rebuild all images including postgres/redis unnecessarily).
- No `depends_on` from API/web/solver to Seq — a Seq health check failure cannot block other services from starting.
- No new GitHub secrets required — all Seq configuration (`SEQ_ADMIN_PASSWORD`, `SEQ_UI_USERNAME`, `SEQ_UI_PASSWORD_HASH`) is read from the `.env` file already present on the VPS.

## How it connects

- Depends on: Tasks 1.1 (Seq service definition), 2.2 (Caddy service definition)
- Enables: Automatic deployment of the full logging stack on every push to main

## How to run / verify

1. Review the deploy command in `.github/workflows/deploy-vps.yml` — it should include `seq caddy` in the service list.
2. Verify `docker-compose.yml` has no `depends_on: seq` on the `api`, `web`, or `solver` services.
3. Verify the workflow uses only existing secrets (`VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`, `VPS_PORT`).

## What comes next

- Task 6.2: Smoke test script to validate configuration files

## Git commit

```bash
git add -A && git commit -m "feat(logging): include seq and caddy in deploy-vps workflow"
```
