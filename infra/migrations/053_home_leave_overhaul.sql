-- Migration 053: Home-leave overhaul — mode system and emergency freeze
-- Adds mode, base_days, home_days, emergency freeze columns to home_leave_configs.
-- Supports three-mode system: automatic, manual, and emergency freeze.

-- ─── 1. Add mode column ──────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'home_leave_configs' AND column_name = 'mode'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD COLUMN mode TEXT NOT NULL DEFAULT 'automatic';

        RAISE NOTICE 'Added mode column to home_leave_configs';
    ELSE
        RAISE NOTICE 'Column mode already exists on home_leave_configs — skipping';
    END IF;
END $$;

-- ─── 2. Add base_days column ─────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'home_leave_configs' AND column_name = 'base_days'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD COLUMN base_days INTEGER NOT NULL DEFAULT 7;

        RAISE NOTICE 'Added base_days column to home_leave_configs';
    ELSE
        RAISE NOTICE 'Column base_days already exists on home_leave_configs — skipping';
    END IF;
END $$;

-- ─── 3. Add home_days column ─────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'home_leave_configs' AND column_name = 'home_days'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD COLUMN home_days INTEGER NOT NULL DEFAULT 2;

        RAISE NOTICE 'Added home_days column to home_leave_configs';
    ELSE
        RAISE NOTICE 'Column home_days already exists on home_leave_configs — skipping';
    END IF;
END $$;

-- ─── 4. Add emergency_freeze_active column ───────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'home_leave_configs' AND column_name = 'emergency_freeze_active'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD COLUMN emergency_freeze_active BOOLEAN NOT NULL DEFAULT FALSE;

        RAISE NOTICE 'Added emergency_freeze_active column to home_leave_configs';
    ELSE
        RAISE NOTICE 'Column emergency_freeze_active already exists on home_leave_configs — skipping';
    END IF;
END $$;

-- ─── 5. Add emergency_use_for_scheduling column ──────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'home_leave_configs' AND column_name = 'emergency_use_for_scheduling'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD COLUMN emergency_use_for_scheduling BOOLEAN NOT NULL DEFAULT FALSE;

        RAISE NOTICE 'Added emergency_use_for_scheduling column to home_leave_configs';
    ELSE
        RAISE NOTICE 'Column emergency_use_for_scheduling already exists on home_leave_configs — skipping';
    END IF;
END $$;

-- ─── 6. Add freeze_started_at column (nullable) ─────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'home_leave_configs' AND column_name = 'freeze_started_at'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD COLUMN freeze_started_at TIMESTAMPTZ;

        RAISE NOTICE 'Added freeze_started_at column to home_leave_configs';
    ELSE
        RAISE NOTICE 'Column freeze_started_at already exists on home_leave_configs — skipping';
    END IF;
END $$;

-- ─── 7. Add pre_freeze_mode column ──────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'home_leave_configs' AND column_name = 'pre_freeze_mode'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD COLUMN pre_freeze_mode TEXT NOT NULL DEFAULT 'automatic';

        RAISE NOTICE 'Added pre_freeze_mode column to home_leave_configs';
    ELSE
        RAISE NOTICE 'Column pre_freeze_mode already exists on home_leave_configs — skipping';
    END IF;
END $$;

-- ─── 8. Add CHECK constraints ────────────────────────────────────────────────

-- chk_mode_valid: mode must be 'automatic' or 'manual'
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'home_leave_configs' AND constraint_name = 'chk_mode_valid'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD CONSTRAINT chk_mode_valid
            CHECK (mode IN ('automatic', 'manual'));

        RAISE NOTICE 'Added chk_mode_valid constraint';
    ELSE
        RAISE NOTICE 'Constraint chk_mode_valid already exists — skipping';
    END IF;
END $$;

-- chk_pre_freeze_mode_valid: pre_freeze_mode must be 'automatic' or 'manual'
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'home_leave_configs' AND constraint_name = 'chk_pre_freeze_mode_valid'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD CONSTRAINT chk_pre_freeze_mode_valid
            CHECK (pre_freeze_mode IN ('automatic', 'manual'));

        RAISE NOTICE 'Added chk_pre_freeze_mode_valid constraint';
    ELSE
        RAISE NOTICE 'Constraint chk_pre_freeze_mode_valid already exists — skipping';
    END IF;
END $$;

-- chk_base_days_min: base_days must be at least 1
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'home_leave_configs' AND constraint_name = 'chk_base_days_min'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD CONSTRAINT chk_base_days_min
            CHECK (base_days >= 1);

        RAISE NOTICE 'Added chk_base_days_min constraint';
    ELSE
        RAISE NOTICE 'Constraint chk_base_days_min already exists — skipping';
    END IF;
END $$;

-- chk_home_days_min: home_days must be at least 1
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage
        WHERE table_name = 'home_leave_configs' AND constraint_name = 'chk_home_days_min'
    ) THEN
        ALTER TABLE home_leave_configs
            ADD CONSTRAINT chk_home_days_min
            CHECK (home_days >= 1);

        RAISE NOTICE 'Added chk_home_days_min constraint';
    ELSE
        RAISE NOTICE 'Constraint chk_home_days_min already exists — skipping';
    END IF;
END $$;

-- ─── 9. Data migration: compute base_days and home_days from existing data ───
-- Converts existing threshold-based configs to the new day-based system.
-- Formula: base_days = GREATEST(1, ROUND(eligibility_threshold_hours / 24))
--          home_days = GREATEST(1, ROUND(leave_duration_hours / 24))
-- Also sets min_rest_hours = 0 since the new day-based ratio system handles
-- rest implicitly via the eligibility threshold.

UPDATE home_leave_configs
SET base_days = GREATEST(1, ROUND(eligibility_threshold_hours / 24))::INTEGER,
    home_days = GREATEST(1, ROUND(leave_duration_hours / 24))::INTEGER,
    min_rest_hours = 0;

-- ─── Track migration ─────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('053') ON CONFLICT DO NOTHING;
