-- Add auto_publish column to groups table
-- When enabled, the auto-scheduler will publish drafts automatically without admin review
ALTER TABLE groups ADD COLUMN IF NOT EXISTS auto_publish BOOLEAN NOT NULL DEFAULT false;
