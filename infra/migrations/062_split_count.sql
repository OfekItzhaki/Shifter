-- Migration 062: Add split_count to tasks (group_tasks)
-- Tracks how many sub-shifts a task is divided into for burden scaling.
-- Default is 1 (no split). Must be >= 1.

-- ─── 1. Add split_count column ───────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'tasks' AND column_name = 'split_count'
    ) THEN
        ALTER TABLE tasks
            ADD COLUMN split_count INTEGER NOT NULL DEFAULT 1;

        RAISE NOTICE 'Added split_count column to tasks';
    ELSE
        RAISE NOTICE 'Column split_count already exists on tasks — skipping';
    END IF;
END $$;

-- ─── 2. Add CHECK constraint ─────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'tasks' AND constraint_name = 'chk_split_count_positive'
    ) THEN
        ALTER TABLE tasks
            ADD CONSTRAINT chk_split_count_positive
            CHECK (split_count >= 1);

        RAISE NOTICE 'Added chk_split_count_positive constraint';
    ELSE
        RAISE NOTICE 'Constraint chk_split_count_positive already exists — skipping';
    END IF;
END $$;

-- ─── Track migration ─────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('062') ON CONFLICT DO NOTHING;
