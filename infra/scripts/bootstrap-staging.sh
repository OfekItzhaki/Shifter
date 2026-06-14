#!/bin/bash
# Bootstrap a persistent Shifter staging stack on a VPS.
#
# This script is intentionally idempotent:
# - clones the repo if SHIFTER_DIR is missing
# - pulls the requested GIT_REF
# - creates infra/compose/.env from .env.staging.example only when missing
# - fills staging-safe ports/domains/secrets
# - optionally manages Caddy staging routes
#
# Example:
#   WEB_BASE_URL=https://staging.shifter.ofeklabs.com \
#   API_BASE_URL=https://staging-api.shifter.ofeklabs.com \
#   BASIC_AUTH_USERNAME=admin \
#   BASIC_AUTH_PASSWORD='choose-a-strong-password' \
#   APPLY_CADDY=true \
#   /opt/shifter-staging/infra/scripts/bootstrap-staging.sh

set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/OfekItzhaki/Shifter.git}"
GIT_REF="${GIT_REF:-develop}"
SHIFTER_DIR="${SHIFTER_DIR:-/opt/shifter-staging}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-shifter-staging}"
WEB_BASE_URL="${WEB_BASE_URL:-https://staging.shifter.ofeklabs.com}"
API_BASE_URL="${API_BASE_URL:-https://staging-api.shifter.ofeklabs.com}"
CADDY_FILE="${CADDY_FILE:-/etc/caddy/Caddyfile}"
APPLY_CADDY="${APPLY_CADDY:-false}"
BASIC_AUTH_USERNAME="${BASIC_AUTH_USERNAME:-admin}"
BASIC_AUTH_PASSWORD="${BASIC_AUTH_PASSWORD:-}"

random_secret() {
  openssl rand -hex 32
}

set_env_value() {
  local key="$1"
  local value="$2"
  local file="$3"

  if grep -qE "^${key}=" "$file"; then
    sed -i "s|^${key}=.*|${key}=${value}|" "$file"
  else
    printf '%s=%s\n' "$key" "$value" >> "$file"
  fi
}

env_value() {
  local key="$1"
  local file="$2"
  local value

  value="$(grep -E "^[[:space:]]*${key}=" "$file" | tail -n 1 | cut -d '=' -f 2- || true)"
  value="${value%%#*}"
  echo "$value" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'
}

set_env_secret_once() {
  local key="$1"
  local file="$2"
  local current

  current="$(env_value "$key" "$file")"
  if [ -z "$current" ] \
    || echo "$current" | grep -qiE '^(change_me|changeme|your-|replace_with|staging_minio$)'; then
    set_env_value "$key" "$(random_secret)" "$file"
  fi
}

require_command() {
  local name="$1"
  if ! command -v "$name" >/dev/null 2>&1; then
    echo "Missing required command: $name" >&2
    exit 1
  fi
}

require_command git
require_command docker
require_command openssl

if [ ! -d "$SHIFTER_DIR/.git" ]; then
  echo "[$(date)] Cloning $REPO_URL into $SHIFTER_DIR..."
  mkdir -p "$(dirname "$SHIFTER_DIR")"
  git clone "$REPO_URL" "$SHIFTER_DIR"
fi

cd "$SHIFTER_DIR"
echo "[$(date)] Checking out $GIT_REF..."
git fetch origin "$GIT_REF"
if git show-ref --verify --quiet "refs/heads/$GIT_REF"; then
  git checkout "$GIT_REF"
else
  git checkout -B "$GIT_REF" "origin/$GIT_REF"
fi
git pull --ff-only origin "$GIT_REF"

COMPOSE_DIR="$SHIFTER_DIR/infra/compose"
ENV_FILE="$COMPOSE_DIR/.env"
EXAMPLE_ENV_FILE="$COMPOSE_DIR/.env.staging.example"

if [ ! -f "$ENV_FILE" ]; then
  echo "[$(date)] Creating staging env file: $ENV_FILE"
  cp "$EXAMPLE_ENV_FILE" "$ENV_FILE"
fi

set_env_value "COMPOSE_PROJECT_NAME" "$COMPOSE_PROJECT_NAME" "$ENV_FILE"
set_env_value "SHIFTER_DEPLOYMENT_MODE" "saas" "$ENV_FILE"

set_env_value "POSTGRES_HOST" "postgres" "$ENV_FILE"
set_env_value "POSTGRES_PORT" "15432" "$ENV_FILE"
set_env_value "POSTGRES_DB" "shifter_staging" "$ENV_FILE"
set_env_value "POSTGRES_USER" "shifter_staging" "$ENV_FILE"
set_env_secret_once "POSTGRES_PASSWORD" "$ENV_FILE"

set_env_value "REDIS_HOST" "redis" "$ENV_FILE"
set_env_value "REDIS_PORT" "16379" "$ENV_FILE"
set_env_secret_once "REDIS_PASSWORD" "$ENV_FILE"

set_env_value "API_PORT" "15000" "$ENV_FILE"
set_env_value "WEB_PORT" "13000" "$ENV_FILE"
set_env_value "SOLVER_PORT" "8000" "$ENV_FILE"
set_env_value "SOLVER_TIMEOUT_SECONDS" "60" "$ENV_FILE"
set_env_value "SOLVE_TIMEOUT_SECONDS" "120" "$ENV_FILE"

set_env_secret_once "JWT_SECRET" "$ENV_FILE"
set_env_value "JWT_ISSUER" "shifter-staging-api" "$ENV_FILE"
set_env_value "JWT_AUDIENCE" "shifter-staging-web" "$ENV_FILE"
set_env_value "JWT_ACCESS_TOKEN_EXPIRY_MINUTES" "15" "$ENV_FILE"
set_env_value "JWT_REFRESH_TOKEN_EXPIRY_DAYS" "7" "$ENV_FILE"
set_env_secret_once "FIELD_ENCRYPTION_KEY" "$ENV_FILE"

set_env_value "APP_FRONTEND_BASE_URL" "$WEB_BASE_URL" "$ENV_FILE"
set_env_value "APP_API_BASE_URL" "$API_BASE_URL" "$ENV_FILE"
set_env_value "NEXT_PUBLIC_API_URL" "$API_BASE_URL" "$ENV_FILE"
set_env_value "NEXT_PUBLIC_APP_VERSION" "staging" "$ENV_FILE"

set_env_value "MINIO_ROOT_USER" "staging_minio" "$ENV_FILE"
set_env_secret_once "MINIO_ROOT_PASSWORD" "$ENV_FILE"
set_env_value "MINIO_PORT" "19000" "$ENV_FILE"
set_env_value "MINIO_CONSOLE_PORT" "19001" "$ENV_FILE"

set_env_value "AUTO_SCHEDULER_ENABLED" "false" "$ENV_FILE"
set_env_value "SEQ_PORT" "18080" "$ENV_FILE"
set_env_secret_once "SEQ_ADMIN_PASSWORD" "$ENV_FILE"
set_env_value "HEALTH_CHECK_INTERVAL_SECONDS" "300" "$ENV_FILE"
set_env_value "HEALTH_CHECK_ALERT_COOLDOWN_SECONDS" "3600" "$ENV_FILE"

chmod +x "$SHIFTER_DIR"/infra/scripts/*.sh 2>/dev/null || true

if [ "$APPLY_CADDY" = "true" ]; then
  require_command sudo

  if [ -z "$BASIC_AUTH_PASSWORD" ]; then
    echo "BASIC_AUTH_PASSWORD is required when APPLY_CADDY=true." >&2
    exit 1
  fi

  echo "[$(date)] Generating Caddy Basic Auth hash..."
  BASIC_AUTH_HASH="$(docker run --rm caddy:2-alpine caddy hash-password --plaintext "$BASIC_AUTH_PASSWORD")"
  TMP_CADDY="$(mktemp)"
  START_MARKER="# BEGIN SHIFTER STAGING"
  END_MARKER="# END SHIFTER STAGING"

  if [ -f "$CADDY_FILE" ]; then
    awk "/$START_MARKER/{skip=1; next} /$END_MARKER/{skip=0; next} !skip{print}" "$CADDY_FILE" > "$TMP_CADDY"
  else
    touch "$TMP_CADDY"
  fi

  cat >> "$TMP_CADDY" <<CADDY

$START_MARKER
staging.shifter.ofeklabs.com {
    header {
        X-Frame-Options "DENY"
        X-Content-Type-Options "nosniff"
        Referrer-Policy "no-referrer"
        -Server
        -X-Powered-By
    }

    basicauth {
        $BASIC_AUTH_USERNAME $BASIC_AUTH_HASH
    }

    reverse_proxy localhost:13000
}

staging-api.shifter.ofeklabs.com {
    basicauth {
        $BASIC_AUTH_USERNAME $BASIC_AUTH_HASH
    }

    reverse_proxy localhost:15000
}
$END_MARKER
CADDY

  sudo cp "$TMP_CADDY" "$CADDY_FILE"
  rm -f "$TMP_CADDY"
  sudo caddy validate --config "$CADDY_FILE"
  sudo systemctl reload caddy
fi

echo "[$(date)] Staging bootstrap complete."
echo "Directory: $SHIFTER_DIR"
echo "Env file:  $ENV_FILE"
echo "Branch:    $(git rev-parse --abbrev-ref HEAD)"
echo "Revision:  $(git rev-parse --short HEAD)"
echo ""
echo "Next deploy command:"
echo "GIT_REF=$GIT_REF SHIFTER_DIR=$SHIFTER_DIR COMPOSE_PROJECT_NAME=$COMPOSE_PROJECT_NAME $SHIFTER_DIR/infra/scripts/deploy-compose.sh"
