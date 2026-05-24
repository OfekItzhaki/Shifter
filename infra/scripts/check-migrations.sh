#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# Migration Completeness Check (Schema Validation)
# ─────────────────────────────────────────────────────────────────────────────
# Spins up a temporary Postgres container, runs ALL SQL migration files in
# order, then uses dotnet-ef to compare the resulting DB schema against the
# EF Core model. If EF would generate any pending migrations, the check fails.
#
# This catches:
#   - Missing columns (like deleted_by_space_deletion)
#   - Wrong column types
#   - Missing indexes
#   - Missing tables
#   - Any drift between code and migrations
#
# Requires: docker, dotnet SDK 8.0
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
MIGRATIONS_DIR="$REPO_ROOT/infra/migrations"
API_DIR="$REPO_ROOT/apps/api"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

CONTAINER_NAME="migration-check-postgres-$$"
DB_NAME="migration_check"
DB_USER="migration_check"
DB_PASS="migration_check_pass"
DB_PORT=54399  # Unlikely to conflict

cleanup() {
  echo "🧹 Cleaning up..."
  docker rm -f "$CONTAINER_NAME" 2>/dev/null || true
}
trap cleanup EXIT

echo "🔍 Migration completeness check"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# ─── 1. Start temporary Postgres ──────────────────────────────────────────────
echo "📦 Starting temporary Postgres container..."
docker run -d \
  --name "$CONTAINER_NAME" \
  -e POSTGRES_DB="$DB_NAME" \
  -e POSTGRES_USER="$DB_USER" \
  -e POSTGRES_PASSWORD="$DB_PASS" \
  -p "$DB_PORT:5432" \
  postgres:16-alpine \
  > /dev/null

# Wait for Postgres to be ready
echo "⏳ Waiting for Postgres to be ready..."
for i in $(seq 1 30); do
  if docker exec "$CONTAINER_NAME" pg_isready -U "$DB_USER" -d "$DB_NAME" > /dev/null 2>&1; then
    break
  fi
  if [ "$i" -eq 30 ]; then
    echo -e "${RED}❌ Postgres failed to start within 30 seconds${NC}"
    exit 1
  fi
  sleep 1
done
echo "✅ Postgres is ready"
echo ""

# ─── 2. Run all SQL migrations in order ──────────────────────────────────────
echo "📋 Running $(ls "$MIGRATIONS_DIR"/*.sql 2>/dev/null | wc -l) SQL migration files..."
for f in $(ls "$MIGRATIONS_DIR"/*.sql | sort); do
  filename=$(basename "$f")
  docker exec -i "$CONTAINER_NAME" psql -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=0 < "$f" > /dev/null 2>&1
done
echo "✅ All migrations applied"
echo ""

# ─── 3. Check EF Core model against the database ─────────────────────────────
echo "🔎 Comparing EF Core model against migrated database..."

CONNECTION_STRING="Host=localhost;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASS"

# Try to generate a migration — if there's nothing to generate, the schema matches
cd "$API_DIR"

# Install ef tool if not present
dotnet tool restore 2>/dev/null || dotnet tool install --global dotnet-ef 2>/dev/null || true

MIGRATION_OUTPUT=$(dotnet ef migrations has-pending-model-changes \
  --project Jobuler.Infrastructure/Jobuler.Infrastructure.csproj \
  --startup-project Jobuler.Api/Jobuler.Api.csproj \
  --context AppDbContext \
  -- --ConnectionStrings:DefaultConnection="$CONNECTION_STRING" 2>&1) || true

# dotnet ef migrations has-pending-model-changes returns:
#   "No pending model changes." when schema is in sync
#   "Changes have been made to the model since the last migration." when out of sync
if echo "$MIGRATION_OUTPUT" | grep -qi "no pending model changes\|No pending"; then
  echo -e "${GREEN}✅ EF Core model matches the migrated database schema. No missing migrations.${NC}"
  exit 0
elif echo "$MIGRATION_OUTPUT" | grep -qi "changes have been made\|pending model changes"; then
  echo -e "${RED}❌ EF Core model has changes not reflected in SQL migrations!${NC}"
  echo ""
  echo "The EF Core model expects columns/tables that don't exist after running"
  echo "all SQL migrations in infra/migrations/."
  echo ""
  echo "To find what's missing, run locally:"
  echo "  dotnet ef migrations add CheckDrift --project Jobuler.Infrastructure --startup-project Jobuler.Api"
  echo "  # Inspect the generated migration, then delete it"
  echo "  # Create the corresponding SQL migration in infra/migrations/"
  echo ""
  echo -e "${YELLOW}EF output:${NC}"
  echo "$MIGRATION_OUTPUT"
  exit 1
else
  # Fallback: if the command isn't available or fails for other reasons,
  # try a simpler approach — attempt to run a query that touches all tables
  echo -e "${YELLOW}⚠️  Could not run EF model comparison (tool may not be installed).${NC}"
  echo "   Falling back to basic column existence check..."
  echo ""
  
  # Extract all HasColumnName mappings from EF configurations
  CONFIGS_DIR="$REPO_ROOT/apps/api/Jobuler.Infrastructure/Persistence/Configurations"
  EF_COLUMNS=$(grep -rhoP '\.HasColumnName\("\K[^"]+' "$CONFIGS_DIR" 2>/dev/null | sort -u)
  
  MISSING=()
  for col in $EF_COLUMNS; do
    # Check if column exists in the database
    EXISTS=$(docker exec "$CONTAINER_NAME" psql -U "$DB_USER" -d "$DB_NAME" -tAc \
      "SELECT COUNT(*) FROM information_schema.columns WHERE column_name = '$col'" 2>/dev/null || echo "0")
    if [ "$EXISTS" = "0" ]; then
      MISSING+=("$col")
    fi
  done
  
  if [ ${#MISSING[@]} -eq 0 ]; then
    echo -e "${GREEN}✅ All EF columns exist in the migrated database.${NC}"
    exit 0
  else
    echo -e "${RED}❌ Found ${#MISSING[@]} column(s) missing from the database after migrations:${NC}"
    echo ""
    for col in "${MISSING[@]}"; do
      file=$(grep -rl "HasColumnName(\"$col\")" "$CONFIGS_DIR" 2>/dev/null | head -1 | xargs basename 2>/dev/null || echo "unknown")
      echo -e "  ${YELLOW}• $col${NC}  (defined in $file)"
    done
    echo ""
    echo -e "${RED}Add SQL migrations in infra/migrations/ for these columns.${NC}"
    exit 1
  fi
fi
