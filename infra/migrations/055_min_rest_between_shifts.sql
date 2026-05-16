-- Migration 055: Add min_rest_between_shifts_hours to groups
-- This is the minimum rest time between any two shifts for members of a group.
-- The solver always receives this as a hard constraint in the payload.
-- Default is 8 hours. Set to 0 to disable (e.g. for restaurants).

-- ─── 1. Add min_rest_between_shifts_hours column ─────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'groups' AND column_name = 'min_rest_between_shifts_hours'
    ) THEN
        ALTER TABLE groups
            ADD COLUMN min_rest_between_shifts_hours INTEGER NOT NULL DEFAULT 8;

        RAISE NOTICE 'Added min_rest_between_shifts_hours column to groups';
    ELSE
        RAISE NOTICE 'Column min_rest_between_shifts_hours already exists on groups — skipping';
    END IF;
END $$;

-- ─── 2. Add CHECK constraint ─────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'groups' AND constraint_name = 'chk_min_rest_between_shifts_hours'
    ) THEN
        ALTER TABLE groups
            ADD CONSTRAINT chk_min_rest_between_shifts_hours
            CHECK (min_rest_between_shifts_hours >= 0 AND min_rest_between_shifts_hours <= 24);

        RAISE NOTICE 'Added chk_min_rest_between_shifts_hours constraint';
    ELSE
        RAISE NOTICE 'Constraint chk_min_rest_between_shifts_hours already exists — skipping';
    END IF;
END $$;

-- ─── Track migration ─────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('055') ON CONFLICT DO NOTHING;
