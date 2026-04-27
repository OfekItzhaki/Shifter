-- Migration 022: Drop FK constraint on assignments.task_slot_id
-- The solver now assigns GroupTasks (from the 'tasks' table) as well as
-- legacy TaskSlots (from 'task_slots'). Since task_slot_id can reference
-- either table, the FK to task_slots must be dropped.
-- The column is retained as a plain UUID — application logic enforces integrity.

ALTER TABLE assignments DROP CONSTRAINT IF EXISTS assignments_task_slot_id_fkey;

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('022') ON CONFLICT DO NOTHING;
