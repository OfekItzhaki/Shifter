-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 006: In-app Notifications
-- Stores per-user, per-space notifications triggered by solver events.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE notifications (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id        UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    event_type      TEXT NOT NULL,   -- e.g. 'solver_completed', 'solver_failed'
    title           TEXT NOT NULL,
    body            TEXT NOT NULL,
    metadata_json   JSONB,           -- optional: run_id, version_id, etc.
    is_read         BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    read_at         TIMESTAMPTZ
);

CREATE INDEX idx_notifications_user_space ON notifications (user_id, space_id);
CREATE INDEX idx_notifications_unread     ON notifications (user_id, space_id) WHERE is_read = FALSE;
CREATE INDEX idx_notifications_created_at ON notifications (created_at DESC);

ALTER TABLE notifications ENABLE ROW LEVEL SECURITY;

CREATE POLICY notifications_isolation ON notifications
    USING (space_id = current_setting('app.current_space_id', TRUE)::UUID);
