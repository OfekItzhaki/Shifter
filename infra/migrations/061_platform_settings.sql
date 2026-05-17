-- Migration 061: Create platform_settings table
-- System-level key-value settings table for platform-wide configuration.
-- Initial seed: platform_timeout_minutes = 15 (session timeout for super platform mode).

-- ─── 1. Create platform_settings table ───────────────────────────────────────
CREATE TABLE IF NOT EXISTS platform_settings (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key         VARCHAR(100) NOT NULL UNIQUE,
    value       TEXT NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─── 2. Seed default platform timeout ────────────────────────────────────────
INSERT INTO platform_settings (key, value)
VALUES ('platform_timeout_minutes', '15')
ON CONFLICT (key) DO NOTHING;

-- ─── Track migration ─────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('061') ON CONFLICT DO NOTHING;
