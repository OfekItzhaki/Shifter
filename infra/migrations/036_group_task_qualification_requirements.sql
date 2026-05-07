-- Migration 036: Replace flat qualification names array with structured requirements
-- Adds qualification_requirements JSONB column to group_tasks.
-- Old required_qualification_names column is kept for backward compat but no longer used by the app.

ALTER TABLE tasks
    ADD COLUMN IF NOT EXISTS qualification_requirements JSONB NOT NULL DEFAULT '[]'::jsonb;
