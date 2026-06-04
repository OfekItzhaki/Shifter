#!/bin/bash
# Environment-aware backup for a Docker Compose Shifter deployment.
#
# Examples:
#   /opt/shifter/infra/scripts/backup-compose.sh
#   SHIFTER_DIR=/opt/shifter-staging COMPOSE_PROJECT_NAME=shifter-staging /opt/shifter-staging/infra/scripts/backup-compose.sh

set -euo pipefail

SHIFTER_DIR="${SHIFTER_DIR:-/opt/shifter}"
COMPOSE_DIR="${COMPOSE_DIR:-$SHIFTER_DIR/infra/compose}"
ENV_FILE="${ENV_FILE:-$COMPOSE_DIR/.env}"
BACKUP_DIR="${BACKUP_DIR:-$SHIFTER_DIR/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"
TIMESTAMP="$(date +%Y%m%d_%H%M%S)"

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing env file: $ENV_FILE" >&2
  exit 1
fi

env_value() {
  local key="$1"
  local value

  value="$(grep -E "^[[:space:]]*${key}=" "$ENV_FILE" | tail -n 1 | cut -d '=' -f 2- || true)"
  value="${value%%#*}"
  echo "$value" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'
}

COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-$(env_value COMPOSE_PROJECT_NAME)}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-$(basename "$COMPOSE_DIR")}"
POSTGRES_DB="${POSTGRES_DB:-$(env_value POSTGRES_DB)}"
POSTGRES_USER="${POSTGRES_USER:-$(env_value POSTGRES_USER)}"

if [ -z "$POSTGRES_DB" ] || [ -z "$POSTGRES_USER" ]; then
  echo "POSTGRES_DB and POSTGRES_USER are required in $ENV_FILE" >&2
  exit 1
fi

mkdir -p "$BACKUP_DIR"

cd "$COMPOSE_DIR"

DB_BACKUP="$BACKUP_DIR/postgres_${COMPOSE_PROJECT_NAME}_${TIMESTAMP}.dump"
UPLOADS_BACKUP="$BACKUP_DIR/uploads_${COMPOSE_PROJECT_NAME}_${TIMESTAMP}.tar.gz"

echo "[$(date)] Backing up PostgreSQL for project '$COMPOSE_PROJECT_NAME'..."
docker compose --project-name "$COMPOSE_PROJECT_NAME" exec -T postgres \
  pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --compress=9 > "$DB_BACKUP"

if [ ! -s "$DB_BACKUP" ]; then
  echo "[$(date)] ERROR: database backup is empty" >&2
  rm -f "$DB_BACKUP"
  exit 1
fi

echo "[$(date)] Database backup: $DB_BACKUP ($(du -h "$DB_BACKUP" | cut -f1))"

UPLOADS_VOLUME="${COMPOSE_PROJECT_NAME}_uploads_data"
UPLOADS_MOUNT="$(docker volume inspect "$UPLOADS_VOLUME" --format '{{.Mountpoint}}' 2>/dev/null || true)"
if [ -n "$UPLOADS_MOUNT" ] && [ -d "$UPLOADS_MOUNT" ]; then
  tar -czf "$UPLOADS_BACKUP" -C "$UPLOADS_MOUNT" .
  echo "[$(date)] Uploads backup: $UPLOADS_BACKUP ($(du -h "$UPLOADS_BACKUP" | cut -f1))"
else
  echo "[$(date)] Uploads volume not found, skipping: $UPLOADS_VOLUME"
fi

find "$BACKUP_DIR" -name "postgres_${COMPOSE_PROJECT_NAME}_*.dump" -mtime +"$RETENTION_DAYS" -delete
find "$BACKUP_DIR" -name "uploads_${COMPOSE_PROJECT_NAME}_*.tar.gz" -mtime +"$RETENTION_DAYS" -delete

echo "[$(date)] Backup complete. Retention: $RETENTION_DAYS days."
