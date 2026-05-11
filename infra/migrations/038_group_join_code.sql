-- Add join_code column to groups table for shareable invitation links
ALTER TABLE groups ADD COLUMN IF NOT EXISTS join_code VARCHAR(8);

-- Create unique index on join_code (only for non-null values)
CREATE UNIQUE INDEX IF NOT EXISTS ix_groups_join_code ON groups (join_code) WHERE join_code IS NOT NULL;

-- Backfill existing groups with join codes
UPDATE groups SET join_code = UPPER(SUBSTRING(gen_random_uuid()::text, 1, 8))
WHERE join_code IS NULL;
