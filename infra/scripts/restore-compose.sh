#!/bin/bash
# Restore a Docker Compose Shifter deployment from backup-compose.sh output.
#
# Examples:
#   CONFIRM=restore DB_BACKUP=/opt/shifter/backups/postgres_shifter_20260612_030000.dump \
#     SHIFTER_DIR=/opt/shifter /opt/shifter/infra/scripts/restore-compose.sh
#
#   DRY_RUN=1 DB_BACKUP=/opt/shifter/backups/postgres_shifter_20260612_030000.dump \
#     SHIFTER_DIR=/opt/shifter /opt/shifter/infra/scripts/restore-compose.sh
#
#   CONFIRM=restore DB_BACKUP=/opt/shifter/backups/postgres_shifter_20260612_030000.dump \
#     UPLOADS_BACKUP=/opt/shifter/backups/uploads_shifter_20260612_030000.tar.gz \
#     RESTORE_UPLOADS=1 /opt/shifter/infra/scripts/restore-compose.sh

set -euo pipefail

SHIFTER_DIR="${SHIFTER_DIR:-/opt/shifter}"
COMPOSE_DIR="${COMPOSE_DIR:-$SHIFTER_DIR/infra/compose}"
ENV_FILE="${ENV_FILE:-$COMPOSE_DIR/.env}"
BACKUP_DIR="${BACKUP_DIR:-$SHIFTER_DIR/backups}"
DB_BACKUP="${DB_BACKUP:-${1:-}}"
UPLOADS_BACKUP="${UPLOADS_BACKUP:-${2:-}}"
RESTORE_UPLOADS="${RESTORE_UPLOADS:-0}"
CONFIRM="${CONFIRM:-}"
DRY_RUN="${DRY_RUN:-0}"
SKIP_PRE_RESTORE_BACKUP="${SKIP_PRE_RESTORE_BACKUP:-0}"
TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
APP_SERVICES_STOPPED=0

restart_app_services_on_error() {
  local exit_code=$?
  if [ "$APP_SERVICES_STOPPED" = "1" ]; then
    echo "[$(date)] ERROR: restore failed with exit code $exit_code. Restarting app services..." >&2
    docker compose --project-name "$COMPOSE_PROJECT_NAME" up -d api web solver || true
  fi
  exit "$exit_code"
}

if [ "$DRY_RUN" != "1" ] && [ "$CONFIRM" != "restore" ]; then
  echo "Refusing to restore without CONFIRM=restore." >&2
  echo "This operation replaces the target PostgreSQL database and, when RESTORE_UPLOADS=1, the uploads volume." >&2
  echo "Use DRY_RUN=1 to validate inputs without changing the deployment." >&2
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

if [ "$DRY_RUN" = "1" ]; then
  PRE_RESTORE_BACKUP="$BACKUP_DIR/pre_restore_${COMPOSE_PROJECT_NAME}_${TIMESTAMP}.dump"
  echo "[$(date)] Restore dry run passed."
  echo "  Compose project: $COMPOSE_PROJECT_NAME"
  echo "  Compose dir:     $COMPOSE_DIR"
  echo "  Env file:        $ENV_FILE"
  echo "  Database:        $POSTGRES_DB"
  echo "  Database user:   $POSTGRES_USER"
  echo "  DB backup:       $DB_BACKUP"
  if [ "$SKIP_PRE_RESTORE_BACKUP" = "1" ]; then
    echo "  Safety backup:   skipped by SKIP_PRE_RESTORE_BACKUP=1"
  else
    echo "  Safety backup:   $PRE_RESTORE_BACKUP"
  fi
  if [ "$RESTORE_UPLOADS" = "1" ]; then
    echo "  Uploads backup:  $UPLOADS_BACKUP"
    echo "  Uploads volume:  ${COMPOSE_PROJECT_NAME}_uploads_data"
    if [ "$SKIP_PRE_RESTORE_BACKUP" = "1" ]; then
      echo "  Uploads safety:  skipped by SKIP_PRE_RESTORE_BACKUP=1"
    else
      echo "  Uploads safety:  $BACKUP_DIR/pre_restore_uploads_${COMPOSE_PROJECT_NAME}_${TIMESTAMP}.tar.gz"
    fi
  else
    echo "  Uploads restore: skipped"
  fi
  docker compose --project-name "$COMPOSE_PROJECT_NAME" config --quiet
  echo "[$(date)] Compose config is valid. No containers, database, or volumes were changed."
  exit 0
fi

echo "[$(date)] Ensuring PostgreSQL is running for project '$COMPOSE_PROJECT_NAME'..."
docker compose --project-name "$COMPOSE_PROJECT_NAME" up -d postgres

echo "[$(date)] Stopping app services before database restore..."
docker compose --project-name "$COMPOSE_PROJECT_NAME" stop api web solver || true
APP_SERVICES_STOPPED=1
trap restart_app_services_on_error ERR

if [ "$SKIP_PRE_RESTORE_BACKUP" != "1" ]; then
  mkdir -p "$BACKUP_DIR"
  PRE_RESTORE_BACKUP="$BACKUP_DIR/pre_restore_${COMPOSE_PROJECT_NAME}_${TIMESTAMP}.dump"

  echo "[$(date)] Creating pre-restore safety backup: $PRE_RESTORE_BACKUP"
  docker compose --project-name "$COMPOSE_PROJECT_NAME" exec -T postgres \
    pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --compress=9 > "$PRE_RESTORE_BACKUP"

  if [ ! -s "$PRE_RESTORE_BACKUP" ]; then
    echo "[$(date)] ERROR: pre-restore safety backup is empty" >&2
    rm -f "$PRE_RESTORE_BACKUP"
    exit 1
  fi

  echo "[$(date)] Pre-restore safety backup complete: $PRE_RESTORE_BACKUP ($(du -h "$PRE_RESTORE_BACKUP" | cut -f1))"
else
  echo "[$(date)] Skipping pre-restore safety backup because SKIP_PRE_RESTORE_BACKUP=1."
fi

echo "[$(date)] Restoring PostgreSQL database '$POSTGRES_DB' from $DB_BACKUP..."
docker compose --project-name "$COMPOSE_PROJECT_NAME" exec -T postgres \
  pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists --no-owner \
    --single-transaction --exit-on-error < "$DB_BACKUP"

if [ "$RESTORE_UPLOADS" = "1" ]; then
  UPLOADS_VOLUME="${COMPOSE_PROJECT_NAME}_uploads_data"
  UPLOADS_MOUNT="$(docker volume inspect "$UPLOADS_VOLUME" --format '{{.Mountpoint}}' 2>/dev/null || true)"

  if [ -z "$UPLOADS_MOUNT" ] || [ ! -d "$UPLOADS_MOUNT" ]; then
    echo "Uploads volume not found: $UPLOADS_VOLUME" >&2
    exit 1
  fi

  if [ "$SKIP_PRE_RESTORE_BACKUP" != "1" ]; then
    mkdir -p "$BACKUP_DIR"
    PRE_RESTORE_UPLOADS_BACKUP="$BACKUP_DIR/pre_restore_uploads_${COMPOSE_PROJECT_NAME}_${TIMESTAMP}.tar.gz"

    echo "[$(date)] Creating pre-restore uploads safety backup: $PRE_RESTORE_UPLOADS_BACKUP"
    tar -czf "$PRE_RESTORE_UPLOADS_BACKUP" -C "$UPLOADS_MOUNT" .

    if [ ! -s "$PRE_RESTORE_UPLOADS_BACKUP" ]; then
      echo "[$(date)] ERROR: pre-restore uploads safety backup is empty" >&2
      rm -f "$PRE_RESTORE_UPLOADS_BACKUP"
      exit 1
    fi

    echo "[$(date)] Pre-restore uploads safety backup complete: $PRE_RESTORE_UPLOADS_BACKUP ($(du -h "$PRE_RESTORE_UPLOADS_BACKUP" | cut -f1))"
  else
    echo "[$(date)] Skipping pre-restore uploads safety backup because SKIP_PRE_RESTORE_BACKUP=1."
  fi

  echo "[$(date)] Replacing uploads volume '$UPLOADS_VOLUME' from $UPLOADS_BACKUP..."
  find "$UPLOADS_MOUNT" -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +
  tar -xzf "$UPLOADS_BACKUP" -C "$UPLOADS_MOUNT"
fi

echo "[$(date)] Starting app services..."
docker compose --project-name "$COMPOSE_PROJECT_NAME" up -d api web solver
APP_SERVICES_STOPPED=0
trap - ERR

echo "[$(date)] Restore complete. Run smoke checks before handing the system back to users."
