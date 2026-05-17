-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 058: Notification Deduplication Hash
-- Adds a deduplication fingerprint column to notifications for idempotent
-- cross-group conflict detection notifications.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE notifications ADD COLUMN IF NOT EXISTS deduplication_hash VARCHAR(64) NULL;

CREATE INDEX IF NOT EXISTS ix_notifications_dedup
ON notifications (user_id, space_id, event_type, deduplication_hash)
WHERE is_read = FALSE;

INSERT INTO schema_migrations (version) VALUES ('058') ON CONFLICT DO NOTHING;
