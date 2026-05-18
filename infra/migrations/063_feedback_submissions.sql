-- Migration 063: Create feedback_submissions table
-- Tracks successful feedback/bug report submissions per user for rate limiting.
-- Not tenant-scoped — feedback is per-user, not per-space.

-- ─── 1. Create feedback_submissions table ────────────────────────────────────
CREATE TABLE IF NOT EXISTS feedback_submissions (
    id               UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id          UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    submitted_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─── 2. Create composite index for rate-limit queries ────────────────────────
CREATE INDEX IF NOT EXISTS idx_feedback_submissions_user_time
    ON feedback_submissions (user_id, submitted_at_utc);

-- ─── Track migration ─────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('063') ON CONFLICT DO NOTHING;
