-- Migration 050: Add home_leave_priority to group_memberships
-- Allows per-person priority for home-leave scheduling.
-- Default 1.0 = normal. Higher = more home time (parents, students).

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'group_memberships' AND column_name = 'home_leave_priority'
    ) THEN
        ALTER TABLE group_memberships
            ADD COLUMN home_leave_priority NUMERIC(3,1) NOT NULL DEFAULT 1.0;

        RAISE NOTICE 'Added home_leave_priority column to group_memberships';
    ELSE
        RAISE NOTICE 'Column home_leave_priority already exists — skipping';
    END IF;
END $$;

-- CHECK constraint: priority must be between 0.5 and 3.0
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'group_memberships' AND constraint_name = 'chk_home_leave_priority_range'
    ) THEN
        ALTER TABLE group_memberships
            ADD CONSTRAINT chk_home_leave_priority_range
            CHECK (home_leave_priority >= 0.5 AND home_leave_priority <= 3.0);

        RAISE NOTICE 'Added chk_home_leave_priority_range constraint';
    ELSE
        RAISE NOTICE 'Constraint already exists — skipping';
    END IF;
END $$;

INSERT INTO schema_migrations (version) VALUES ('050') ON CONFLICT DO NOTHING;
