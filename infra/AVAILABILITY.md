# Availability Runbook

This is the practical first layer for keeping the VPS deployment recoverable:

1. prevent common self-inflicted outages
2. detect failures quickly
3. keep backups close
4. make rollback fast
5. keep staging separate from production

## Current Baseline

The Compose stack already uses:

- `restart: unless-stopped` so containers restart after crashes or VPS reboot
- container health checks for PostgreSQL, Redis, MinIO, API, solver, web, and Seq
- `/health` on the API for external uptime monitoring
- Caddy as the HTTPS reverse proxy

This runbook adds:

- Docker log rotation, so logs do not fill the disk
- environment-aware backups for production or staging
- safer deploys with backup, health verification, and rollback to the previous git revision

## Recommended VPS Layout

Production VPS:

```bash
/opt/shifter
GIT_REF=main
COMPOSE_PROJECT_NAME=shifter-production
```

Staging VPS:

```bash
/opt/shifter-staging
GIT_REF=develop
COMPOSE_PROJECT_NAME=shifter-staging
```

Use a different VPS for staging if possible. It gives cleaner isolation and lets you test deploys without stealing production CPU/RAM.

## Production Deploy

Run this on the production VPS:

```bash
GIT_REF=main \
SHIFTER_DIR=/opt/shifter \
COMPOSE_PROJECT_NAME=shifter-production \
/opt/shifter/infra/scripts/deploy-compose.sh
```

The script:

1. fetches and fast-forwards the selected branch
2. creates a backup
3. rebuilds and restarts the Compose stack
4. checks the frontend and API health endpoint
5. rolls back to the previous git revision if the new version fails health verification

## Staging Deploy

Run this on the staging VPS:

```bash
GIT_REF=develop \
SHIFTER_DIR=/opt/shifter-staging \
COMPOSE_PROJECT_NAME=shifter-staging \
/opt/shifter-staging/infra/scripts/deploy-compose.sh
```

Staging should use:

- `staging.yourdomain.com`
- separate `.env`
- separate database credentials
- separate Redis password
- separate object storage bucket or MinIO volume
- sandbox billing/email/SMS credentials
- Basic Auth in Caddy

## Backups

Manual backup:

```bash
SHIFTER_DIR=/opt/shifter \
COMPOSE_PROJECT_NAME=shifter-production \
/opt/shifter/infra/scripts/backup-compose.sh
```

Cron example:

```cron
0 3 * * * SHIFTER_DIR=/opt/shifter COMPOSE_PROJECT_NAME=shifter-production /opt/shifter/infra/scripts/backup-compose.sh >> /var/log/shifter-backup.log 2>&1
```

For staging, either skip scheduled backups or keep a shorter retention:

```cron
30 3 * * * SHIFTER_DIR=/opt/shifter-staging COMPOSE_PROJECT_NAME=shifter-staging RETENTION_DAYS=3 /opt/shifter-staging/infra/scripts/backup-compose.sh >> /var/log/shifter-staging-backup.log 2>&1
```

Backups are only useful after restore is tested. Before relying on this setup,
restore one backup into staging and verify login, scheduling, file uploads, and
billing-disabled behavior:

```bash
DRY_RUN=1 \
DB_BACKUP=/opt/shifter/backups/postgres_shifter-production_20260612_030000.dump \
UPLOADS_BACKUP=/opt/shifter/backups/uploads_shifter-production_20260612_030000.tar.gz \
RESTORE_UPLOADS=1 \
SHIFTER_DIR=/opt/shifter-staging \
COMPOSE_PROJECT_NAME=shifter-staging \
/opt/shifter-staging/infra/scripts/restore-compose.sh
```

```bash
CONFIRM=restore \
DB_BACKUP=/opt/shifter/backups/postgres_shifter-production_20260612_030000.dump \
UPLOADS_BACKUP=/opt/shifter/backups/uploads_shifter-production_20260612_030000.tar.gz \
RESTORE_UPLOADS=1 \
SHIFTER_DIR=/opt/shifter-staging \
COMPOSE_PROJECT_NAME=shifter-staging \
/opt/shifter-staging/infra/scripts/restore-compose.sh
```

## Monitoring

Set up an external monitor against:

```text
https://your-production-domain.com/health
```

Recommended checks:

- HTTP status is `200`
- response contains `"status":"healthy"`
- alert to email/Telegram/Slack
- interval: 1 to 5 minutes

Also watch VPS disk usage. Docker logs are now rotated, but database, uploads, backups, and images can still fill the disk.

## What This Does Not Solve Yet

This is not full high availability. If the VPS itself is offline, production is offline.

True high availability needs:

- at least two app VPS instances
- a load balancer
- managed/external PostgreSQL
- shared object storage
- a Redis/session strategy that works across instances
- tested failover

Do this later when uptime requirements justify the extra cost and complexity. The next best step after this runbook is automated GitHub Actions deploys for `develop` and `main`.
