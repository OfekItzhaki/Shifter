-- Migration 080: Create space_home_leave_configs table
-- Required by SolverPayloadNormalizer for home-leave scheduling configuration.

CREATE TABLE IF NOT EXISTS space_home_leave_configs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id UUID NOT NULL UNIQUE,
    mode TEXT NOT NULL DEFAULT 'disabled',
    balance_value INTEGER NOT NULL DEFAULT 50,
    base_days INTEGER NOT NULL DEFAULT 14,
    home_days INTEGER NOT NULL DEFAULT 7,
    min_people_at_base INTEGER NOT NULL DEFAULT 2,
    min_rest_hours NUMERIC NOT NULL DEFAULT 8,
    eligibility_threshold_hours INTEGER NOT NULL DEFAULT 168,
    leave_capacity INTEGER NOT NULL DEFAULT 3,
    leave_duration_hours INTEGER NOT NULL DEFAULT 24,
    emergency_freeze_active BOOLEAN NOT NULL DEFAULT FALSE,
    emergency_use_for_scheduling BOOLEAN NOT NULL DEFAULT FALSE,
    freeze_started_at TIMESTAMPTZ,
    pre_freeze_mode TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_space_home_leave_configs_space_id ON space_home_leave_configs (space_id);

-- RLS policy for tenant isolation
ALTER TABLE space_home_leave_configs ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON space_home_leave_configs
    USING (space_id::text = current_setting('app.current_space_id', TRUE));
