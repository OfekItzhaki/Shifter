-- Migration 064: Add country_code and state_code to users
-- Stores the user's geographic location for timezone resolution.
-- CountryCode is ISO 3166-1 alpha-2 (2 chars), StateCode is ISO 3166-2 (up to 6 chars).
-- Both are nullable — users without a location default to Asia/Jerusalem.

-- ─── 1. Add country_code column ──────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'users' AND column_name = 'country_code'
    ) THEN
        ALTER TABLE users
            ADD COLUMN country_code VARCHAR(2) NULL;

        RAISE NOTICE 'Added country_code column to users';
    ELSE
        RAISE NOTICE 'Column country_code already exists on users — skipping';
    END IF;
END $$;

-- ─── 2. Add state_code column ────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'users' AND column_name = 'state_code'
    ) THEN
        ALTER TABLE users
            ADD COLUMN state_code VARCHAR(6) NULL;

        RAISE NOTICE 'Added state_code column to users';
    ELSE
        RAISE NOTICE 'Column state_code already exists on users — skipping';
    END IF;
END $$;

-- ─── Track migration ─────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('064') ON CONFLICT DO NOTHING;
