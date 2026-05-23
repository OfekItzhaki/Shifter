-- Add parent_group_id to groups table for parent-child hierarchy
ALTER TABLE groups ADD COLUMN IF NOT EXISTS parent_group_id UUID REFERENCES groups(id) ON DELETE SET NULL;

-- Drop the PascalCase column if it was created by mistake
ALTER TABLE groups DROP COLUMN IF EXISTS "ParentGroupId";

-- Index for efficient child group lookups
CREATE INDEX IF NOT EXISTS ix_groups_parent_group_id ON groups (parent_group_id) WHERE parent_group_id IS NOT NULL;
