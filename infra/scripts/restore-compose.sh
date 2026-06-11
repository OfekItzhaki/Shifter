#!/bin/bash
# Restore a Docker Compose Shifter deployment from backup-compose.sh output.
#
# Examples:
#   CONFIRM=restore DB_BACKUP=/opt/shifter/backups/postgres_shifter_20260612_030000.dump \
#     SHIFTER_DIR=/opt/shifter /opt/shifter/infra/scripts/restore-compose.sh
#
#   CONFIRM=restore DB_BACKUP=/opt/shifter/backups/postgres_shifter_20260612_030000.dump \
#     UPLOADS_BACKUP=/opt/shifter/backups/uploads_shifter_20260612_030000.tar.gz \
#     RESTORE_UPLOADS=1 /opt/shifter/infra/scripts/restore-compose.sh

set -euo pipefail

SHIFTER_DIR="${SHIFTER_DIR:-/opt/shifter}"
COMPOSE_DIR="${COMPOSE_DIR:-$SHIFTER_DIR/infra/compose}"
ENV_FILE="${ENV_FILE:-$COMPOSE_DIR/.env}"
DB_BACKUP="${DB_BACKUP:-${1:-}}"
UPLOADS_BACKUP="${UPLOADS_BACKUP:-${2:-}}"
RESTORE_UPLOADS="${RESTORE_UPLOADS:-0}"
CONFIRM="${CONFIRM:-}"

if [ "$CONFIRM" != "restore" ]; then
  echo "Refusing to restore without CONFIRM=restore." >&2
  echo "This operation replaces the target PostgreSQL database and, when RESTORE_UPLOADS=1, the uploads volume." >&2
  exit 1
fi

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing env file: $ENV_FILE" >&2
  exit 1
fi

if [ -z "$DB_BACKUP" ] || [ ! -s "$DB_BACKUP" ]; then
  echo "DB_BACKUP is required and must point to a non-empty pg_dump custom-format file." >&2
  exit 1
fi

if [ "$RESTORE_UPLOADS" = "1" ] && { [ -z "$UPLOADS_BACKUP" ] || [ ! -s "$UPLOADS_BACKUP" ]; }; then
  echo "UPLOADS_BACKUP is required and must be non-empty when RESTORE_UPLOADS=1." >&2
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

cd "$COMPOSE_DIR"

echo "[$(date)] Ensuring PostgreSQL is running for project '$COMPOSE_PROJECT_NAME'..."
docker compose --project-name "$COMPOSE_PROJECT_NAME" up -d postgres

echo "[$(date)] Stopping app services before database restore..."
docker compose --project-name "$COMPOSE_PROJECT_NAME" stop api web solver || true

echo "[$(date)] Restoring PostgreSQL database '$POSTGRES_DB' from $DB_BACKUP..."
docker compose --project-name "$COMPOSE_PROJECT_NAME" exec -T postgres \
  pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists --no-owner < "$DB_BACKUP"

if [ "$RESTORE_UPLOADS" = "1" ]; then
  UPLOADS_VOLUME="${COMPOSE_PROJECT_NAME}_uploads_data"
  UPLOADS_MOUNT="$(docker volume inspect "$UPLOADS_VOLUME" --format '{{.Mountpoint}}' 2>/dev/null || true)"

  if [ -z "$UPLOADS_MOUNT" ] || [ ! -d "$UPLOADS_MOUNT" ]; then
    echo "Uploads volume not found: $UPLOADS_VOLUME" >&2
    exit 1
  fi

  echo "[$(date)] Replacing uploads volume '$UPLOADS_VOLUME' from $UPLOADS_BACKUP..."
  find "$UPLOADS_MOUNT" -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +
  tar -xzf "$UPLOADS_BACKUP" -C "$UPLOADS_MOUNT"
fi

echo "[$(date)] Starting app services..."
docker compose --project-name "$COMPOSE_PROJECT_NAME" up -d api web solver

echo "[$(date)] Restore complete. Run smoke checks before handing the system back to users."
