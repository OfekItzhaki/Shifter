-- Add progress_phase column to schedule_runs for live progress display
ALTER TABLE schedule_runs ADD COLUMN IF NOT EXISTS progress_phase TEXT;
