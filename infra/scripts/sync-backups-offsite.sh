#!/bin/bash
# Sync Shifter local backup artifacts to an rclone remote.
#
# Examples:
#   OFFSITE_REMOTE="hetzner:shifter-prod-backups/prod" /opt/shifter/infra/scripts/sync-backups-offsite.sh
#   BACKUP_DIR=/opt/shifter-staging/backups OFFSITE_REMOTE="r2:shifter-prod-backups/staging" ./sync-backups-offsite.sh

set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/opt/shifter/backups}"
OFFSITE_REMOTE="${OFFSITE_REMOTE:-}"

if [ -z "$OFFSITE_REMOTE" ]; then
  echo "OFFSITE_REMOTE is required, for example: hetzner:shifter-prod-backups/prod" >&2
  exit 1
fi

if ! command -v rclone >/dev/null 2>&1; then
  echo "rclone is not installed. Install it first: curl https://rclone.org/install.sh | sudo bash" >&2
  exit 1
fi

if [ ! -d "$BACKUP_DIR" ]; then
  echo "Backup directory does not exist: $BACKUP_DIR" >&2
  exit 1
fi

echo "[$(date)] Syncing backups from $BACKUP_DIR to $OFFSITE_REMOTE..."
rclone copy "$BACKUP_DIR" "$OFFSITE_REMOTE" \
  --include "postgres_*.dump" \
  --include "uploads_*.tar.gz" \
  --include "backup_*.dump" \
  --include "shifter_*.sql.gz" \
  --exclude "*" \
  --transfers 4 \
  --checkers 8

echo "[$(date)] Offsite backup sync complete."
