-- Migration 043: Unavailability reasons for structured presence tracking
-- Creates unavailability_reasons table and adds optional FK to presence_windows.

-- ─── Unavailability reasons (per space) ──────────────────────────────────────
CREATE TABLE IF NOT EXISTS unavailability_reasons (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id        UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    display_name    VARCHAR(100) NOT NULL,
    sort_order      INTEGER NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_unavailability_reasons_space_active
    ON unavailability_reasons (space_id, is_active) WHERE is_active = TRUE;

-- RLS
ALTER TABLE unavailability_reasons ENABLE ROW LEVEL SECURITY;

CREATE POLICY unavailability_reasons_tenant_isolation ON unavailability_reasons
    USING (space_id = current_setting('app.current_space_id', TRUE)::UUID);

-- Updated-at trigger
CREATE TRIGGER trg_unavailability_reasons_updated_at
    BEFORE UPDATE ON unavailability_reasons
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ─── Add optional FK to presence_windows ─────────────────────────────────────
ALTER TABLE presence_windows
    ADD COLUMN IF NOT EXISTS unavailability_reason_id UUID
        REFERENCES unavailability_reasons(id) ON DELETE SET NULL;

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('043') ON CONFLICT DO NOTHING;
