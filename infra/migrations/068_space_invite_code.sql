-- Add invite_code to spaces table
ALTER TABLE spaces ADD COLUMN IF NOT EXISTS invite_code VARCHAR(8);

-- Create unique index on invite_code (only for non-null values)
CREATE UNIQUE INDEX IF NOT EXISTS ix_spaces_invite_code ON spaces (invite_code) WHERE invite_code IS NOT NULL;

-- Backfill existing spaces with invite codes
UPDATE spaces
SET invite_code = UPPER(SUBSTRING(md5(random()::text || id::text) FROM 1 FOR 8))
WHERE invite_code IS NULL;
