-- Migration 025: Add 'emergency' severity to constraint_rules
-- Emergency constraints bypass all hard/soft constraints in the solver.
--
-- At this point in the migration sequence, constraint_rules.severity is still
-- a native PostgreSQL enum type (constraint_severity). Migration 029 will later
-- convert it to TEXT. So here we must use ALTER TYPE to add the new enum value.

ALTER TYPE constraint_severity ADD VALUE IF NOT EXISTS 'emergency';

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('025') ON CONFLICT DO NOTHING;
