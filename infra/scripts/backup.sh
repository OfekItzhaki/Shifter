#!/bin/bash
# =============================================================================
# Daily PostgreSQL backup script
# Run via cron: 0 3 * * * /opt/shifter/infra/scripts/backup.sh
# Keeps last 7 daily backups, rotates automatically.
# =============================================================================

set -e

BACKUP_DIR="/opt/shifter/backups"
CONTAINER="compose-postgres-1"
DB_USER="jobuler"
DB_NAME="jobuler"
DATE=$(date +%Y-%m-%d_%H%M)
KEEP_DAYS=7

mkdir -p "$BACKUP_DIR"

echo "[$(date)] Starting backup..."

# Dump the database
docker exec "$CONTAINER" pg_dump -U "$DB_USER" -d "$DB_NAME" --format=custom --compress=9 > "$BACKUP_DIR/backup_${DATE}.dump"

echo "[$(date)] Backup created: backup_${DATE}.dump ($(du -h "$BACKUP_DIR/backup_${DATE}.dump" | cut -f1))"

# Remove backups older than KEEP_DAYS
find "$BACKUP_DIR" -name "backup_*.dump" -mtime +$KEEP_DAYS -delete

echo "[$(date)] Old backups cleaned. Current backups:"
ls -lh "$BACKUP_DIR"/backup_*.dump 2>/dev/null || echo "  (none)"
echo "[$(date)] Done."
