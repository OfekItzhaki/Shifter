-- Migration 019: Convert all remaining PostgreSQL custom enum columns to TEXT
-- Fixes EF Core ValueConverter compatibility issues

-- ── schedule_runs.status ─────────────────────────────────────────────────────
ALTER TABLE schedule_runs ADD COLUMN IF NOT EXISTS status_text TEXT;
UPDATE schedule_runs SET status_text = status::TEXT;
ALTER TABLE schedule_runs DROP COLUMN status;
ALTER TABLE schedule_runs RENAME COLUMN status_text TO status;
ALTER TABLE schedule_runs ALTER COLUMN status SET NOT NULL;
ALTER TABLE schedule_runs ALTER COLUMN status SET DEFAULT 'queued';
ALTER TABLE schedule_runs ADD CONSTRAINT chk_schedule_run_status
    CHECK (status IN ('queued', 'running', 'completed', 'failed', 'timed_out'));

-- ── schedule_runs.trigger_type ───────────────────────────────────────────────
ALTER TABLE schedule_runs ADD COLUMN IF NOT EXISTS trigger_type_text TEXT;
UPDATE schedule_runs SET trigger_type_text = trigger_type::TEXT;
ALTER TABLE schedule_runs DROP COLUMN trigger_type;
ALTER TABLE schedule_runs RENAME COLUMN trigger_type_text TO trigger_type;
ALTER TABLE schedule_runs ALTER COLUMN trigger_type SET NOT NULL;
ALTER TABLE schedule_runs ALTER COLUMN trigger_type SET DEFAULT 'standard';
ALTER TABLE schedule_runs ADD CONSTRAINT chk_schedule_run_trigger
    CHECK (trigger_type IN ('standard', 'emergency', 'rollback'));

-- ── task_slots.status ────────────────────────────────────────────────────────
ALTER TABLE task_slots ADD COLUMN IF NOT EXISTS status_text TEXT;
UPDATE task_slots SET status_text = status::TEXT;
ALTER TABLE task_slots DROP COLUMN status;
ALTER TABLE task_slots RENAME COLUMN status_text TO status;
ALTER TABLE task_slots ALTER COLUMN status SET NOT NULL;
ALTER TABLE task_slots ALTER COLUMN status SET DEFAULT 'open';

-- ── assignments.assignment_source ────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'assignments' AND column_name = 'assignment_source'
        AND udt_name != 'text'
    ) THEN
        ALTER TABLE assignments ADD COLUMN assignment_source_text TEXT;
        UPDATE assignments SET assignment_source_text = assignment_source::TEXT;
        ALTER TABLE assignments DROP COLUMN assignment_source;
        ALTER TABLE assignments RENAME COLUMN assignment_source_text TO assignment_source;
        ALTER TABLE assignments ALTER COLUMN assignment_source SET NOT NULL;
        ALTER TABLE assignments ALTER COLUMN assignment_source SET DEFAULT 'solver';
    END IF;
END $$;

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('019') ON CONFLICT DO NOTHING;
