-- Migration 066: Double-shift recommendations
-- Stores post-solve recommendations suggesting tasks where enabling AllowsDoubleShift
-- could reduce uncovered slots. Tenant-scoped with RLS protection.

-- ─── 1. Create double_shift_recommendations table ─────────────────────────────
CREATE TABLE IF NOT EXISTS double_shift_recommendations (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    space_id                    UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    group_id                    UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    schedule_run_id             UUID NOT NULL REFERENCES schedule_runs(id) ON DELETE CASCADE,
    group_task_id               UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    task_name                   VARCHAR(200) NOT NULL,
    status                      VARCHAR(20) NOT NULL DEFAULT 'Active',
    additional_slots_covered    INT NOT NULL,
    affected_date_start         TIMESTAMPTZ NOT NULL,
    affected_date_end           TIMESTAMPTZ NOT NULL,
    total_uncovered_slots_in_run INT NOT NULL,
    dismissed_at                TIMESTAMPTZ,
    dismissed_by_user_id        UUID,
    resolved_at                 TIMESTAMPTZ,
    cleared_at                  TIMESTAMPTZ,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─── 2. Indexes ──────────────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS ix_dsr_space_group_status
    ON double_shift_recommendations (space_id, group_id, status);

CREATE INDEX IF NOT EXISTS ix_dsr_space_run
    ON double_shift_recommendations (space_id, schedule_run_id);

CREATE INDEX IF NOT EXISTS ix_dsr_space_task_status
    ON double_shift_recommendations (space_id, group_task_id, status);

CREATE INDEX IF NOT EXISTS ix_dsr_created_at
    ON double_shift_recommendations (created_at);

-- ─── 3. Unique constraint for upsert pattern ─────────────────────────────────
ALTER TABLE double_shift_recommendations
    ADD CONSTRAINT uq_dsr_space_run_task
    UNIQUE (space_id, schedule_run_id, group_task_id);

-- ─── 4. Row-Level Security ───────────────────────────────────────────────────
ALTER TABLE double_shift_recommendations ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE tablename = 'double_shift_recommendations'
          AND policyname = 'dsr_tenant_isolation'
    ) THEN
        CREATE POLICY dsr_tenant_isolation ON double_shift_recommendations
            USING (space_id = current_setting('app.current_space_id', TRUE)::UUID);
    END IF;
END $$;

-- ─── Track migration ─────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('066') ON CONFLICT DO NOTHING;
