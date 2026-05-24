#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# Migration Completeness Check
# ─────────────────────────────────────────────────────────────────────────────
# Spins up a temporary Postgres container, runs ALL SQL migration files,
# then checks that every column referenced in EF Core configurations
# actually exists in the database.
#
# Catches missing columns before they reach production.
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CONFIGS_DIR="$REPO_ROOT/apps/api/Jobuler.Infrastructure/Persistence/Configurations"
MIGRATIONS_DIR="$REPO_ROOT/infra/migrations"

CONTAINER_NAME="migration-check-postgres-$$"
DB_NAME="migration_check"
DB_USER="migration_check"
DB_PASS="migration_check_pass"
DB_PORT=54399

cleanup() {
  docker rm -f "$CONTAINER_NAME" 2>/dev/null || true
}
trap cleanup EXIT

echo "🔍 Migration completeness check"
echo ""

# ─── 1. Start temporary Postgres ──────────────────────────────────────────────
echo "📦 Starting temporary Postgres..."
docker run -d \
  --name "$CONTAINER_NAME" \
  -e POSTGRES_DB="$DB_NAME" \
  -e POSTGRES_USER="$DB_USER" \
  -e POSTGRES_PASSWORD="$DB_PASS" \
  -p "$DB_PORT:5432" \
  postgres:16-alpine \
  > /dev/null

echo "⏳ Waiting for Postgres to be ready..."
for i in $(seq 1 30); do
  if docker exec "$CONTAINER_NAME" pg_isready -U "$DB_USER" -d "$DB_NAME" > /dev/null 2>&1; then
    break
  fi
  if [ "$i" -eq 30 ]; then
    echo "❌ Postgres failed to start"
    exit 1
  fi
  sleep 1
done
echo "✅ Postgres is ready"

# ─── 2. Run all SQL migrations ────────────────────────────────────────────────
MIGRATION_COUNT=$(ls "$MIGRATIONS_DIR"/*.sql 2>/dev/null | wc -l)
echo "📋 Running $MIGRATION_COUNT SQL migration files..."
for f in $(ls "$MIGRATIONS_DIR"/*.sql | sort); do
  docker exec -i "$CONTAINER_NAME" psql -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=0 < "$f" > /dev/null 2>&1
done
echo "✅ All migrations applied"
echo ""

# ─── 3. Check EF columns exist in the database ───────────────────────────────
echo "🔎 Verifying EF column mappings..."

EF_COLUMNS=$(grep -rhoP '\.HasColumnName\("\K[^"]+' "$CONFIGS_DIR" 2>/dev/null | sort -u)

MISSING=()
for col in $EF_COLUMNS; do
  EXISTS=$(docker exec "$CONTAINER_NAME" psql -U "$DB_USER" -d "$DB_NAME" -tAc \
    "SELECT COUNT(*) FROM information_schema.columns WHERE column_name = '$col'" 2>/dev/null || echo "0")
  EXISTS=$(echo "$EXISTS" | tr -d '[:space:]')
  if [ "$EXISTS" = "0" ]; then
    MISSING+=("$col")
  fi
done

if [ ${#MISSING[@]} -eq 0 ]; then
  echo "✅ All EF columns have corresponding SQL migrations."
  exit 0
else
  echo "❌ Found ${#MISSING[@]} column(s) missing from the database after migrations:"
  echo ""
  for col in "${MISSING[@]}"; do
    file=$(grep -rl "HasColumnName(\"$col\")" "$CONFIGS_DIR" 2>/dev/null | head -1 | xargs basename 2>/dev/null || echo "unknown")
    echo "  • $col  (in $file)"
  done
  echo ""
  echo "Add SQL migrations in infra/migrations/ for these columns."
  exit 1
fi
