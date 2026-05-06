-- Migration 035: Add solver_start_date_time to groups
-- Allows admins to configure a custom start date/time for the auto-scheduler.
-- When set, the auto-scheduler passes this value as StartTime to TriggerSolverCommand.
-- When NULL (default), the solver falls back to DateTime.UtcNow.

ALTER TABLE groups
    ADD COLUMN IF NOT EXISTS solver_start_date_time TIMESTAMPTZ;
