-- Migration 046: Add fairness_counter_snapshots table and expand fairness_counters
-- Adds historical snapshot table for time-series graphs
-- Adds new columns to fairness_counters: hard_tasks_30d, easy_tasks_7d/14d/30d, burden_score_7d/14d/30d

-- ─── Add new columns to fairness_counters ─────────────────────────────────────
ALTER TABLE fairness_counters
    ADD COLUMN IF NOT EXISTS hard_tasks_30d INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS easy_tasks_7d INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS easy_tasks_14d INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS easy_tasks_30d INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS burden_score_7d INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS burden_score_14d INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS burden_score_30d INT NOT NULL DEFAULT 0;

-- ─── Create fairness_counter_snapshots table ──────────────────────────────────
CREATE TABLE IF NOT EXISTS fairness_counter_snapshots (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    space_id UUID NOT NULL REFERENCES spaces(id),
    person_id UUID NOT NULL REFERENCES people(id),
    snapshot_date DATE NOT NULL,
    total_assignments INT NOT NULL DEFAULT 0,
    hard_count INT NOT NULL DEFAULT 0,
    normal_count INT NOT NULL DEFAULT 0,
    easy_count INT NOT NULL DEFAULT 0,
    burden_score INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(space_id, person_id, snapshot_date)
);

-- ─── Add index for efficient space+date queries ───────────────────────────────
CREATE INDEX IF NOT EXISTS idx_fcs_space_date
    ON fairness_counter_snapshots(space_id, snapshot_date);

-- ─── Enable RLS ───────────────────────────────────────────────────────────────
ALTER TABLE fairness_counter_snapshots ENABLE ROW LEVEL SECURITY;

CREATE POLICY fairness_counter_snapshots_space_isolation
    ON fairness_counter_snapshots
    USING (space_id = current_setting('app.current_space_id')::uuid);

-- ─── Track migration ──────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('046') ON CONFLICT DO NOTHING;
