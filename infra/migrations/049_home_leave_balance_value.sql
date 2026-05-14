-- Migration 049: Add balance_value column to home_leave_configs
-- Supports the home-leave slider feature: an integer 0–100 controlling
-- the solver's preference weight between base coverage and home-leave.

-- ─── Add balance_value column ────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'home_leave_configs' AND column_name = 'balance_value'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD COLUMN balance_value INTEGER NOT NULL DEFAULT 50;

        RAISE NOTICE 'Added balance_value column to home_leave_configs';
    ELSE
        RAISE NOTICE 'Column balance_value already exists on home_leave_configs — skipping';
    END IF;
END $$;

-- ─── Add CHECK constraint ────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'home_leave_configs' AND constraint_name = 'chk_balance_value_range'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD CONSTRAINT chk_balance_value_range
            CHECK (balance_value >= 0 AND balance_value <= 100);

        RAISE NOTICE 'Added chk_balance_value_range constraint';
    ELSE
        RAISE NOTICE 'Constraint chk_balance_value_range already exists — skipping';
    END IF;
END $$;

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('049') ON CONFLICT DO NOTHING;
