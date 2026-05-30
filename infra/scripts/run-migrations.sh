#!/bin/bash
# Safe migration runner for Shifter
# Tracks which migrations have been applied and only runs new ones.
# Replaces the naive "run all and suppress errors" approach.

set -euo pipefail

MIGRATIONS_DIR="/opt/shifter/infra/migrations"
CONTAINER_NAME="compose-postgres-1"
DB_NAME="jobuler"
DB_USER="jobuler"

# Create tracking table if it doesn't exist
docker exec -i "$CONTAINER_NAME" psql -U "$DB_USER" -d "$DB_NAME" <<'SQL'
CREATE TABLE IF NOT EXISTS _migration_history (
    filename TEXT PRIMARY KEY,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
SQL

echo "[$(date)] Checking for new migrations..."

APPLIED=0
SKIPPED=0

for f in "$MIGRATIONS_DIR"/*.sql; do
    FILENAME=$(basename "$f")
    
    # Check if already applied
    ALREADY_APPLIED=$(docker exec -i "$CONTAINER_NAME" psql -U "$DB_USER" -d "$DB_NAME" -tAc \
        "SELECT COUNT(*) FROM _migration_history WHERE filename = '$FILENAME'")
    
    if [ "$ALREADY_APPLIED" = "1" ]; then
        SKIPPED=$((SKIPPED + 1))
        continue
    fi
    
    echo "  Applying: $FILENAME"
    
    # Run the migration
    if docker exec -i "$CONTAINER_NAME" psql -U "$DB_USER" -d "$DB_NAME" < "$f" 2>&1; then
        # Record as applied
        docker exec -i "$CONTAINER_NAME" psql -U "$DB_USER" -d "$DB_NAME" -c \
            "INSERT INTO _migration_history (filename) VALUES ('$FILENAME')"
        APPLIED=$((APPLIED + 1))
    else
        echo "  ERROR: Migration $FILENAME failed!"
        exit 1
    fi
done

echo "[$(date)] Migrations complete: $APPLIED applied, $SKIPPED already applied"
