#!/bin/bash
# Daily PostgreSQL backup script
# Run via cron: 0 3 * * * /opt/shifter/infra/scripts/backup-db.sh

set -e

BACKUP_DIR="/opt/shifter/backups"
COMPOSE_DIR="/opt/shifter/infra/compose"
RETENTION_DAYS=7
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/shifter_db_$TIMESTAMP.sql.gz"

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Dump database via docker compose
cd "$COMPOSE_DIR"
docker compose exec -T postgres pg_dump -U jobuler -d jobuler | gzip > "$BACKUP_FILE"

# Verify backup was created and has content
if [ ! -s "$BACKUP_FILE" ]; then
  echo "ERROR: Backup file is empty or missing: $BACKUP_FILE"
  exit 1
fi

echo "Backup created: $BACKUP_FILE ($(du -h "$BACKUP_FILE" | cut -f1))"

# Remove backups older than retention period
find "$BACKUP_DIR" -name "shifter_db_*.sql.gz" -mtime +$RETENTION_DAYS -delete
echo "Cleaned up backups older than $RETENTION_DAYS days"
