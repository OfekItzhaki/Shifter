#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# Migration Completeness Check
# ─────────────────────────────────────────────────────────────────────────────
# Scans EF Core configuration files for .HasColumnName("...") mappings and
# verifies that each column is either:
#   1. Created in a SQL migration file (ALTER TABLE ... ADD COLUMN / CREATE TABLE)
#   2. Listed in the known-columns allowlist (columns from initial schema)
#
# Exits with code 1 if any unmigrated columns are found.
# Run this in CI before deploying to catch missing migrations early.
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CONFIGS_DIR="$REPO_ROOT/apps/api/Jobuler.Infrastructure/Persistence/Configurations"
MIGRATIONS_DIR="$REPO_ROOT/infra/migrations"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "🔍 Checking migration completeness..."
echo ""

# ─── Extract all column names from EF configurations ──────────────────────────
# Pattern: .HasColumnName("column_name")
EF_COLUMNS=$(grep -rhoP '\.HasColumnName\("\K[^"]+' "$CONFIGS_DIR" | sort -u)

# ─── Extract all columns created/added in SQL migrations ─────────────────────
# Patterns:
#   ADD COLUMN [IF NOT EXISTS] column_name
#   column_name TYPE (in CREATE TABLE blocks)
MIGRATION_COLUMNS=$(
  # ADD COLUMN statements
  grep -rhoiP 'ADD COLUMN\s+(IF NOT EXISTS\s+)?(\w+)' "$MIGRATIONS_DIR" | \
    grep -oP '\w+$' | tr '[:upper:]' '[:lower:]'
  
  # CREATE TABLE column definitions (indented lines with type)
  grep -rhoP '^\s+(\w+)\s+(UUID|TEXT|INTEGER|BIGINT|BOOLEAN|TIMESTAMPTZ|TIMESTAMP|NUMERIC|DECIMAL|JSONB|SERIAL|BYTEA|VARCHAR|REAL|DOUBLE)' "$MIGRATIONS_DIR" | \
    grep -oP '^\s*\w+' | tr -d ' ' | tr '[:upper:]' '[:lower:]'
) 

MIGRATION_COLUMNS=$(echo "$MIGRATION_COLUMNS" | sort -u)

# ─── Known columns from initial schema (001-006) that don't have explicit ADD COLUMN ─
# These are created in CREATE TABLE statements in the initial migrations.
# We extract them automatically from CREATE TABLE blocks.
KNOWN_COLUMNS=$(
  # Extract column names from CREATE TABLE blocks in all migration files
  # Lines that start with whitespace followed by a word and a type keyword
  grep -rhP '^\s+\w+\s+(UUID|TEXT|INTEGER|BIGINT|BOOLEAN|TIMESTAMPTZ|TIMESTAMP|NUMERIC|DECIMAL|JSONB|SERIAL|BYTEA|VARCHAR|REAL|DOUBLE|SMALLINT)' "$MIGRATIONS_DIR" | \
    grep -oP '^\s*\w+' | tr -d ' ' | tr '[:upper:]' '[:lower:]' | sort -u
  
  # Also include common implicit columns
  echo "id"
  echo "created_at"
  echo "updated_at"
)

KNOWN_COLUMNS=$(echo "$KNOWN_COLUMNS" | sort -u)

# ─── Combine all known migrated columns ──────────────────────────────────────
ALL_MIGRATED=$(echo -e "$MIGRATION_COLUMNS\n$KNOWN_COLUMNS" | sort -u)

# ─── Check each EF column against migrated columns ───────────────────────────
MISSING=()
for col in $EF_COLUMNS; do
  col_lower=$(echo "$col" | tr '[:upper:]' '[:lower:]')
  if ! echo "$ALL_MIGRATED" | grep -qx "$col_lower"; then
    MISSING+=("$col")
  fi
done

# ─── Report results ──────────────────────────────────────────────────────────
if [ ${#MISSING[@]} -eq 0 ]; then
  echo -e "${GREEN}✅ All EF columns have corresponding SQL migrations.${NC}"
  exit 0
else
  echo -e "${RED}❌ Found ${#MISSING[@]} column(s) in EF configurations without SQL migrations:${NC}"
  echo ""
  for col in "${MISSING[@]}"; do
    # Find which configuration file defines this column
    file=$(grep -rl "HasColumnName(\"$col\")" "$CONFIGS_DIR" | head -1 | xargs basename)
    echo -e "  ${YELLOW}• $col${NC}  (in $file)"
  done
  echo ""
  echo -e "${RED}Add a SQL migration in infra/migrations/ for these columns before deploying.${NC}"
  echo "Example: ALTER TABLE <table> ADD COLUMN IF NOT EXISTS $col <TYPE>;"
  exit 1
fi
