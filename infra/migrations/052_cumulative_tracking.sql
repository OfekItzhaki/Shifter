-- Migration 052: Cumulative tracking and subscription periods
-- Adds subscription_periods, cumulative_records, daily_snapshots tables
-- and schedule_history_retention_days column to groups.

-- ─── 1. Create subscription_periods table ─────────────────────────────────────
CREATE TABLE IF NOT EXISTS subscription_periods (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id    UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    group_id    UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    status      TEXT NOT NULL DEFAULT 'active',
    starts_at   TIMESTAMPTZ NOT NULL,
    ends_at     TIMESTAMPTZ,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_subscription_periods_group
    ON subscription_periods (space_id, group_id);

CREATE INDEX IF NOT EXISTS idx_subscription_periods_active
    ON subscription_periods (group_id, status)
    WHERE status = 'active';

ALTER TABLE subscription_periods ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE tablename = 'subscription_periods'
          AND policyname = 'subscription_periods_isolation'
    ) THEN
        CREATE POLICY subscription_periods_isolation ON subscription_periods
            USING (space_id = current_setting('app.current_space_id', TRUE)::UUID);
    END IF;
END $$;

-- ─── 2. Create cumulative_records table ───────────────────────────────────────
CREATE TABLE IF NOT EXISTS cumulative_records (
    id                              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id                        UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    group_id                        UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    person_id                       UUID NOT NULL REFERENCES people(id) ON DELETE CASCADE,
    period_id                       UUID NOT NULL REFERENCES subscription_periods(id),

    -- Consecutive hours tracking (for home-leave eligibility)
    consecutive_hours_at_base       NUMERIC(10,2) NOT NULL DEFAULT 0,
    last_home_leave_end             TIMESTAMPTZ,

    -- Assignment counters: 7d, 14d, 30d, 90d, all-time-within-period
    total_assignments_7d            INT NOT NULL DEFAULT 0,
    total_assignments_14d           INT NOT NULL DEFAULT 0,
    total_assignments_30d           INT NOT NULL DEFAULT 0,
    total_assignments_90d           INT NOT NULL DEFAULT 0,
    total_assignments_period        INT NOT NULL DEFAULT 0,

    hard_tasks_7d                   INT NOT NULL DEFAULT 0,
    hard_tasks_14d                  INT NOT NULL DEFAULT 0,
    hard_tasks_30d                  INT NOT NULL DEFAULT 0,
    hard_tasks_90d                  INT NOT NULL DEFAULT 0,
    hard_tasks_period               INT NOT NULL DEFAULT 0,

    disliked_hated_score_7d         INT NOT NULL DEFAULT 0,
    disliked_hated_score_14d        INT NOT NULL DEFAULT 0,
    disliked_hated_score_30d        INT NOT NULL DEFAULT 0,
    disliked_hated_score_90d        INT NOT NULL DEFAULT 0,
    disliked_hated_score_period     INT NOT NULL DEFAULT 0,

    kitchen_count_7d                INT NOT NULL DEFAULT 0,
    kitchen_count_14d               INT NOT NULL DEFAULT 0,
    kitchen_count_30d               INT NOT NULL DEFAULT 0,
    kitchen_count_90d               INT NOT NULL DEFAULT 0,
    kitchen_count_period            INT NOT NULL DEFAULT 0,

    night_missions_7d               INT NOT NULL DEFAULT 0,
    night_missions_14d              INT NOT NULL DEFAULT 0,
    night_missions_30d              INT NOT NULL DEFAULT 0,
    night_missions_90d              INT NOT NULL DEFAULT 0,
    night_missions_period           INT NOT NULL DEFAULT 0,

    total_hours_assigned_period     NUMERIC(10,2) NOT NULL DEFAULT 0,

    updated_at                      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE (space_id, group_id, person_id, period_id)
);

CREATE INDEX IF NOT EXISTS idx_cumulative_records_lookup
    ON cumulative_records (space_id, group_id, period_id);

CREATE INDEX IF NOT EXISTS idx_cumulative_records_person
    ON cumulative_records (person_id, period_id);

ALTER TABLE cumulative_records ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE tablename = 'cumulative_records'
          AND policyname = 'cumulative_records_isolation'
    ) THEN
        CREATE POLICY cumulative_records_isolation ON cumulative_records
            USING (space_id = current_setting('app.current_space_id', TRUE)::UUID);
    END IF;
END $$;

-- ─── 3. Create daily_snapshots table ──────────────────────────────────────────
CREATE TABLE IF NOT EXISTS daily_snapshots (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id        UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    group_id        UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    person_id       UUID NOT NULL REFERENCES people(id) ON DELETE CASCADE,
    period_id       UUID NOT NULL REFERENCES subscription_periods(id),
    snapshot_date   DATE NOT NULL,
    task_type_id    UUID,
    slot_id         UUID,
    shift_start     TIMESTAMPTZ,
    shift_end       TIMESTAMPTZ,
    burden_level    TEXT,
    version_id      UUID NOT NULL REFERENCES schedule_versions(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE (space_id, group_id, person_id, snapshot_date, slot_id)
);

CREATE INDEX IF NOT EXISTS idx_daily_snapshots_date_range
    ON daily_snapshots (space_id, group_id, snapshot_date);

CREATE INDEX IF NOT EXISTS idx_daily_snapshots_person
    ON daily_snapshots (person_id, snapshot_date);

CREATE INDEX IF NOT EXISTS idx_daily_snapshots_period
    ON daily_snapshots (period_id, snapshot_date);

ALTER TABLE daily_snapshots ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE tablename = 'daily_snapshots'
          AND policyname = 'daily_snapshots_isolation'
    ) THEN
        CREATE POLICY daily_snapshots_isolation ON daily_snapshots
            USING (space_id = current_setting('app.current_space_id', TRUE)::UUID);
    END IF;
END $$;

-- ─── 4. Add schedule_history_retention_days to groups ─────────────────────────
ALTER TABLE groups
    ADD COLUMN IF NOT EXISTS schedule_history_retention_days INT DEFAULT NULL;

-- ─── Track migration ──────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('052') ON CONFLICT DO NOTHING;
