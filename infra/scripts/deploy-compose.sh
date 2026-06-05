#!/bin/bash
# Safer Docker Compose deployment with backup, health verification, and git rollback.
#
# Examples:
#   GIT_REF=main SHIFTER_DIR=/opt/shifter /opt/shifter/infra/scripts/deploy-compose.sh
#   GIT_REF=develop SHIFTER_DIR=/opt/shifter-staging COMPOSE_PROJECT_NAME=shifter-staging /opt/shifter-staging/infra/scripts/deploy-compose.sh

set -euo pipefail

SHIFTER_DIR="${SHIFTER_DIR:-/opt/shifter}"
COMPOSE_DIR="${COMPOSE_DIR:-$SHIFTER_DIR/infra/compose}"
ENV_FILE="${ENV_FILE:-$COMPOSE_DIR/.env}"
GIT_REMOTE="${GIT_REMOTE:-origin}"
GIT_REF="${GIT_REF:-main}"
HEALTH_TIMEOUT_SECONDS="${HEALTH_TIMEOUT_SECONDS:-180}"
RUN_BACKUP="${RUN_BACKUP:-true}"

if [ ! -d "$SHIFTER_DIR/.git" ]; then
  echo "Missing git repository: $SHIFTER_DIR" >&2
  exit 1
fi

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
WEB_PORT="${WEB_PORT:-$(env_value WEB_PORT)}"
API_PORT="${API_PORT:-$(env_value API_PORT)}"

if [ -z "$WEB_PORT" ] || [ -z "$API_PORT" ]; then
  echo "WEB_PORT and API_PORT are required in $ENV_FILE" >&2
  exit 1
fi

wait_for_health() {
  local deadline=$((SECONDS + HEALTH_TIMEOUT_SECONDS))

  while [ "$SECONDS" -lt "$deadline" ]; do
    local unhealthy
    unhealthy="$(docker compose --project-name "$COMPOSE_PROJECT_NAME" ps --format json \
      | grep -E '"Health":"(unhealthy|starting)"' || true)"

    if [ -z "$unhealthy" ] \
      && curl -fsS "http://127.0.0.1:${WEB_PORT}" >/dev/null \
      && curl -fsS "http://127.0.0.1:${API_PORT}/health" >/dev/null; then
      return 0
    fi

    sleep 5
  done

  return 1
}

rollback_to() {
  local previous_revision="$1"

  echo "[$(date)] Rolling back to $previous_revision..."
  cd "$SHIFTER_DIR"
  git checkout --detach "$previous_revision"
  cd "$COMPOSE_DIR"
  docker compose --project-name "$COMPOSE_PROJECT_NAME" up -d --build --remove-orphans
}

cd "$SHIFTER_DIR"
PREVIOUS_REVISION="$(git rev-parse HEAD)"

echo "[$(date)] Deploying $GIT_REMOTE/$GIT_REF in $SHIFTER_DIR"
git fetch "$GIT_REMOTE" "$GIT_REF"
if git show-ref --verify --quiet "refs/heads/$GIT_REF"; then
  git checkout "$GIT_REF"
else
  git checkout -B "$GIT_REF" "$GIT_REMOTE/$GIT_REF"
fi
git pull --ff-only "$GIT_REMOTE" "$GIT_REF"
NEW_REVISION="$(git rev-parse HEAD)"

if [ "$RUN_BACKUP" = "true" ]; then
  cd "$COMPOSE_DIR"
  if docker compose --project-name "$COMPOSE_PROJECT_NAME" ps --services --status running postgres | grep -qx postgres; then
    "$SHIFTER_DIR/infra/scripts/backup-compose.sh"
  else
    echo "[$(date)] PostgreSQL is not running yet; skipping pre-deploy backup."
  fi
fi

cd "$COMPOSE_DIR"
docker compose --project-name "$COMPOSE_PROJECT_NAME" up -d --build --remove-orphans

if wait_for_health; then
  echo "[$(date)] Deploy healthy: $NEW_REVISION"
  docker compose --project-name "$COMPOSE_PROJECT_NAME" ps
  exit 0
fi

echo "[$(date)] Deploy failed health verification." >&2
docker compose --project-name "$COMPOSE_PROJECT_NAME" ps >&2
docker compose --project-name "$COMPOSE_PROJECT_NAME" logs --tail=100 api web solver >&2 || true

rollback_to "$PREVIOUS_REVISION"

if wait_for_health; then
  echo "[$(date)] Rollback healthy: $PREVIOUS_REVISION"
  exit 1
fi

echo "[$(date)] Rollback also failed health verification. Manual intervention required." >&2
exit 2
