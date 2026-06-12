#!/usr/bin/env bash
# Load demo seed data into the customer/local Docker Compose PostgreSQL service.
#
# Usage:
#   SHIFTER_DIR=/opt/shifter /opt/shifter/infra/scripts/seed-compose.sh
#   ENV_FILE=/opt/shifter/infra/compose/.env COMPOSE_PROJECT_NAME=shifter /opt/shifter/infra/scripts/seed-compose.sh

set -euo pipefail

SHIFTER_DIR="${SHIFTER_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
COMPOSE_DIR="${COMPOSE_DIR:-$SHIFTER_DIR/infra/compose}"
ENV_FILE="${ENV_FILE:-$COMPOSE_DIR/.env}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-shifter}"
SEED_FILE="${SEED_FILE:-$SHIFTER_DIR/infra/scripts/seed.sql}"
DRY_RUN="${DRY_RUN:-0}"

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing env file: $ENV_FILE" >&2
  echo "Copy infra/compose/.env.customer.example to infra/compose/.env first." >&2
  exit 1
fi

if [ ! -f "$SEED_FILE" ]; then
  echo "Missing seed file: $SEED_FILE" >&2
  exit 1
fi

env_value() {
  local key="$1"
  local value

  value="$(grep -E "^[[:space:]]*${key}=" "$ENV_FILE" | tail -n 1 | cut -d '=' -f 2- || true)"
  value="${value%%#*}"
  echo "$value" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'
}

POSTGRES_DB="${POSTGRES_DB:-$(env_value POSTGRES_DB)}"
POSTGRES_USER="${POSTGRES_USER:-$(env_value POSTGRES_USER)}"

if [ -z "$POSTGRES_DB" ] || [ -z "$POSTGRES_USER" ]; then
  echo "POSTGRES_DB and POSTGRES_USER must be set in $ENV_FILE." >&2
  exit 1
fi

compose() {
  docker compose --env-file "$ENV_FILE" --project-name "$COMPOSE_PROJECT_NAME" -f "$COMPOSE_DIR/docker-compose.yml" "$@"
}

echo "Loading seed data into Compose project '$COMPOSE_PROJECT_NAME' database '$POSTGRES_DB'..."
if [ "$DRY_RUN" = "1" ]; then
  echo "Seed dry run passed."
  echo "Env file: $ENV_FILE"
  echo "Compose file: $COMPOSE_DIR/docker-compose.yml"
  echo "Seed file: $SEED_FILE"
  echo "Project: $COMPOSE_PROJECT_NAME"
  echo "Database: $POSTGRES_DB"
  echo "User: $POSTGRES_USER"
  echo "Command: docker compose --env-file '$ENV_FILE' --project-name '$COMPOSE_PROJECT_NAME' -f '$COMPOSE_DIR/docker-compose.yml' exec -T postgres psql -U '$POSTGRES_USER' -d '$POSTGRES_DB' < '$SEED_FILE'"
  exit 0
fi

compose exec -T postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" < "$SEED_FILE"
echo "Seed complete."
