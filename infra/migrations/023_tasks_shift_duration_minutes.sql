-- Migration 023: Rename duration_hours → shift_duration_minutes on tasks table
-- Converts the decimal hours column to an integer minutes column.
-- Existing data: multiply hours × 60 to get minutes.

DO $$
BEGIN
    -- Only run if the old column exists
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'tasks' AND column_name = 'duration_hours'
    ) THEN
        -- Add new column
        ALTER TABLE tasks ADD COLUMN shift_duration_minutes INT NOT NULL DEFAULT 240;
        -- Migrate existing data (hours × 60, rounded to nearest minute)
        UPDATE tasks SET shift_duration_minutes = ROUND(duration_hours * 60)::INT;
        -- Drop old column
        ALTER TABLE tasks DROP COLUMN duration_hours;
    END IF;

    -- If already migrated (column exists with new name), do nothing
END $$;

-- Ensure constraint: shift must be at least 1 minute
ALTER TABLE tasks DROP CONSTRAINT IF EXISTS chk_task_shift_duration;
ALTER TABLE tasks ADD CONSTRAINT chk_task_shift_duration
    CHECK (shift_duration_minutes >= 1);

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('023') ON CONFLICT DO NOTHING;
