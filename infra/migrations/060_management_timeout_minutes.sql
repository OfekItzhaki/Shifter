-- Migration 060: Add management_timeout_minutes to groups
-- Configurable session timeout for management mode per group.
-- Default is 15 minutes. Range is [5, 120].

-- ─── 1. Add management_timeout_minutes column ────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'groups' AND column_name = 'management_timeout_minutes'
    ) THEN
        ALTER TABLE groups
            ADD COLUMN management_timeout_minutes INTEGER NOT NULL DEFAULT 15;

        RAISE NOTICE 'Added management_timeout_minutes column to groups';
    ELSE
        RAISE NOTICE 'Column management_timeout_minutes already exists on groups — skipping';
    END IF;
END $$;

-- ─── 2. Add CHECK constraint ─────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'groups' AND constraint_name = 'chk_management_timeout_range'
    ) THEN
        ALTER TABLE groups
            ADD CONSTRAINT chk_management_timeout_range
            CHECK (management_timeout_minutes >= 5 AND management_timeout_minutes <= 120);

        RAISE NOTICE 'Added chk_management_timeout_range constraint';
    ELSE
        RAISE NOTICE 'Constraint chk_management_timeout_range already exists — skipping';
    END IF;
END $$;

-- ─── 3. Backfill existing groups ─────────────────────────────────────────────
-- The DEFAULT 15 on the column already handles existing rows during ALTER TABLE,
-- but we explicitly update to be safe in case the column was added without default.
UPDATE groups SET management_timeout_minutes = 15 WHERE management_timeout_minutes IS NULL;

-- ─── Track migration ─────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('060') ON CONFLICT DO NOTHING;
