-- Migration 018: Convert schedule_version_status from PostgreSQL enum to TEXT
-- This fixes EF Core ValueConverter compatibility issues with custom enum types

-- Step 1: Add a temporary text column
ALTER TABLE schedule_versions ADD COLUMN IF NOT EXISTS status_text TEXT;

-- Step 2: Copy existing values (enum → text)
UPDATE schedule_versions SET status_text = status::TEXT;

-- Step 3: Drop the old enum column
ALTER TABLE schedule_versions DROP COLUMN status;

-- Step 4: Rename the text column
ALTER TABLE schedule_versions RENAME COLUMN status_text TO status;

-- Step 5: Add NOT NULL constraint and default
ALTER TABLE schedule_versions ALTER COLUMN status SET NOT NULL;
ALTER TABLE schedule_versions ALTER COLUMN status SET DEFAULT 'draft';

-- Step 6: Add check constraint for valid values
ALTER TABLE schedule_versions ADD CONSTRAINT chk_schedule_version_status
    CHECK (status IN ('draft', 'published', 'rolled_back', 'archived', 'discarded'));

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('018') ON CONFLICT DO NOTHING;
