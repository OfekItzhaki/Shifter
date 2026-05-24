-- Migration: Add schedule regeneration columns and indexes
-- Feature: schedule-regeneration

-- Add new columns to schedule_versions
ALTER TABLE schedule_versions ADD COLUMN IF NOT EXISTS source_type varchar(50);
ALTER TABLE schedule_versions ADD COLUMN IF NOT EXISTS supersedes_version_id uuid;

-- Add new columns to schedule_runs
ALTER TABLE schedule_runs ADD COLUMN IF NOT EXISTS group_id uuid;
ALTER TABLE schedule_runs ADD COLUMN IF NOT EXISTS result_version_id uuid;

-- Indexes
CREATE INDEX IF NOT EXISTS "IX_schedule_versions_supersedes_version_id"
    ON schedule_versions (supersedes_version_id);

CREATE INDEX IF NOT EXISTS "IX_schedule_runs_group_id"
    ON schedule_runs (group_id);

CREATE INDEX IF NOT EXISTS "IX_schedule_runs_result_version_id"
    ON schedule_runs (result_version_id);

-- Foreign keys
DO $$ BEGIN
    ALTER TABLE schedule_runs
        ADD CONSTRAINT "FK_schedule_runs_groups_group_id"
        FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE SET NULL;
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE schedule_runs
        ADD CONSTRAINT "FK_schedule_runs_schedule_versions_result_version_id"
        FOREIGN KEY (result_version_id) REFERENCES schedule_versions(id) ON DELETE SET NULL;
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE schedule_versions
        ADD CONSTRAINT "FK_schedule_versions_schedule_versions_supersedes_version_id"
        FOREIGN KEY (supersedes_version_id) REFERENCES schedule_versions(id) ON DELETE SET NULL;
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Partial index for concurrency guard (regeneration runs per group)
CREATE INDEX IF NOT EXISTS "ix_schedule_runs_group_regeneration"
    ON schedule_runs (space_id, group_id, status)
    WHERE trigger_type = 'Regeneration' AND status IN ('Queued', 'Running');
