-- Migration 054: Add min_people_at_base to home_leave_configs
-- The admin sets how many people MUST stay at base at all times.
-- leave_capacity is derived: memberCount - min_people_at_base.
-- This is more intuitive than configuring "how many can be home simultaneously."

-- ─── 1. Add min_people_at_base column ────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'home_leave_configs' AND column_name = 'min_people_at_base'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD COLUMN min_people_at_base INTEGER NOT NULL DEFAULT 1;

        RAISE NOTICE 'Added min_people_at_base column to home_leave_configs';
    ELSE
        RAISE NOTICE 'Column min_people_at_base already exists on home_leave_configs — skipping';
    END IF;
END $$;

-- ─── 2. Add CHECK constraint ─────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'home_leave_configs' AND constraint_name = 'chk_min_people_at_base'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD CONSTRAINT chk_min_people_at_base
            CHECK (min_people_at_base >= 1);

        RAISE NOTICE 'Added chk_min_people_at_base constraint';
    ELSE
        RAISE NOTICE 'Constraint chk_min_people_at_base already exists — skipping';
    END IF;
END $$;

-- ─── Track migration ─────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('054') ON CONFLICT DO NOTHING;
