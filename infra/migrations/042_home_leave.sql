-- Migration 042: Home-leave scheduling for closed-base groups
-- Adds is_closed_base flag to groups, creates home_leave_configs and home_leave_templates tables.

-- ─── Add is_closed_base to groups ────────────────────────────────────────────
ALTER TABLE groups
    ADD COLUMN IF NOT EXISTS is_closed_base BOOLEAN NOT NULL DEFAULT FALSE;

-- ─── Home-leave configuration (one per group) ────────────────────────────────
CREATE TABLE IF NOT EXISTS home_leave_configs (
    id                          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    group_id                    UUID NOT NULL UNIQUE REFERENCES groups(id) ON DELETE CASCADE,
    space_id                    UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    min_rest_hours              DECIMAL NOT NULL,
    eligibility_threshold_hours DECIMAL NOT NULL,
    leave_capacity              INTEGER NOT NULL,
    leave_duration_hours        DECIMAL NOT NULL,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_home_leave_configs_group_id
    ON home_leave_configs (group_id);

CREATE INDEX IF NOT EXISTS idx_home_leave_configs_space_id
    ON home_leave_configs (space_id);

-- RLS
ALTER TABLE home_leave_configs ENABLE ROW LEVEL SECURITY;

CREATE POLICY home_leave_configs_tenant_isolation ON home_leave_configs
    USING (space_id = current_setting('app.current_space_id', TRUE)::UUID);

-- Updated-at trigger
CREATE TRIGGER trg_home_leave_configs_updated_at
    BEFORE UPDATE ON home_leave_configs
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ─── Home-leave templates (reusable configs per space) ───────────────────────
CREATE TABLE IF NOT EXISTS home_leave_templates (
    id                          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id                    UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    name                        VARCHAR(100) NOT NULL,
    min_rest_hours              DECIMAL NOT NULL,
    eligibility_threshold_hours DECIMAL NOT NULL,
    leave_capacity              INTEGER NOT NULL,
    leave_duration_hours        DECIMAL NOT NULL,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_home_leave_templates_space_name
    ON home_leave_templates (space_id, name);

CREATE INDEX IF NOT EXISTS idx_home_leave_templates_space_id
    ON home_leave_templates (space_id);

-- RLS
ALTER TABLE home_leave_templates ENABLE ROW LEVEL SECURITY;

CREATE POLICY home_leave_templates_tenant_isolation ON home_leave_templates
    USING (space_id = current_setting('app.current_space_id', TRUE)::UUID);

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('042') ON CONFLICT DO NOTHING;
