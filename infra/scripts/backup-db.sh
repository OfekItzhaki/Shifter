#!/bin/bash
# Database backup script for Shifter
# Run via cron: 0 3 * * * /opt/shifter/infra/scripts/backup-db.sh
#
# Keeps the last 7 daily backups. Older backups are automatically deleted.

set -euo pipefail

BACKUP_DIR="/opt/shifter/backups"
CONTAINER_NAME="compose-postgres-1"
DB_NAME="jobuler"
DB_USER="jobuler"
RETENTION_DAYS=7
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/shifter_${TIMESTAMP}.sql.gz"

# Ensure backup directory exists
mkdir -p "$BACKUP_DIR"

# Create compressed backup
echo "[$(date)] Starting database backup..."
docker exec "$CONTAINER_NAME" pg_dump -U "$DB_USER" "$DB_NAME" | gzip > "$BACKUP_FILE"

# Verify backup is not empty
if [ ! -s "$BACKUP_FILE" ]; then
    echo "[$(date)] ERROR: Backup file is empty!"
    rm -f "$BACKUP_FILE"
    exit 1
fi

BACKUP_SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
echo "[$(date)] Backup completed: $BACKUP_FILE ($BACKUP_SIZE)"

# Remove backups older than retention period
find "$BACKUP_DIR" -name "shifter_*.sql.gz" -mtime +$RETENTION_DAYS -delete
echo "[$(date)] Cleaned up backups older than $RETENTION_DAYS days"

# List current backups
echo "[$(date)] Current backups:"
ls -lh "$BACKUP_DIR"/shifter_*.sql.gz 2>/dev/null || echo "  (none)"
