#!/usr/bin/env bash
set -euo pipefail

SHIFTER_DIR="${SHIFTER_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
COMPOSE_DIR="${COMPOSE_DIR:-$SHIFTER_DIR/infra/compose}"
ENV_FILE="${ENV_FILE:-$COMPOSE_DIR/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-$COMPOSE_DIR/docker-compose.yml}"
BUNDLE_DIR="${BUNDLE_DIR:-$SHIFTER_DIR/artifacts/customer-hosted}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-}"
DRY_RUN="${DRY_RUN:-0}"

if [ ! -f "$ENV_FILE" ]; then
  echo "ERROR: Env file not found: $ENV_FILE" >&2
  exit 1
fi

if [ ! -f "$COMPOSE_FILE" ]; then
  echo "ERROR: Compose file not found: $COMPOSE_FILE" >&2
  exit 1
fi

if [ -z "$COMPOSE_PROJECT_NAME" ]; then
  COMPOSE_PROJECT_NAME="$(grep -E '^[[:space:]]*COMPOSE_PROJECT_NAME=' "$ENV_FILE" | tail -n 1 | cut -d= -f2- | tr -d '"' | tr -d "'" || true)"
fi

COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-shifter}"
BUNDLE_NAME="${BUNDLE_NAME:-shifter-${COMPOSE_PROJECT_NAME}-images.tar}"
BUNDLE_PATH="$BUNDLE_DIR/$BUNDLE_NAME"
MANIFEST_PATH="$BUNDLE_PATH.manifest.txt"
SHA_PATH="$BUNDLE_PATH.sha256"

compose_args=(
  compose
  --env-file "$ENV_FILE"
  --project-name "$COMPOSE_PROJECT_NAME"
  -f "$COMPOSE_FILE"
)

echo "Customer-hosted image bundle plan:"
echo "  Shifter dir: $SHIFTER_DIR"
echo "  Compose file: $COMPOSE_FILE"
echo "  Env file: $ENV_FILE"
echo "  Project: $COMPOSE_PROJECT_NAME"
echo "  Bundle: $BUNDLE_PATH"

if [ "$DRY_RUN" = "1" ]; then
  echo "DRY_RUN=1; skipping docker build/pull/save."
  docker "${compose_args[@]}" config --images | sort -u
  echo "Image bundle dry run passed."
  exit 0
fi

mkdir -p "$BUNDLE_DIR"

echo "Building Shifter application images..."
docker "${compose_args[@]}" build api solver web

echo "Pulling bundled infrastructure images..."
docker "${compose_args[@]}" pull postgres redis minio seq

mapfile -t images < <(docker "${compose_args[@]}" config --images | sort -u)
if [ "${#images[@]}" -eq 0 ]; then
  echo "ERROR: Compose did not resolve any images." >&2
  exit 1
fi

printf "%s\n" "${images[@]}" > "$MANIFEST_PATH"

echo "Saving ${#images[@]} image(s) to $BUNDLE_PATH..."
docker save -o "$BUNDLE_PATH" "${images[@]}"

if command -v sha256sum >/dev/null 2>&1; then
  (cd "$BUNDLE_DIR" && sha256sum "$BUNDLE_NAME") > "$SHA_PATH"
else
  (cd "$BUNDLE_DIR" && shasum -a 256 "$BUNDLE_NAME") > "$SHA_PATH"
fi

echo "Image bundle created:"
echo "  $BUNDLE_PATH"
echo "  $MANIFEST_PATH"
echo "  $SHA_PATH"
echo
echo "Load on the target host with:"
echo "  docker load -i '$BUNDLE_PATH'"
