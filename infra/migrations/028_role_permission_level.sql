-- Migration 028: Add permission_level to space_roles
-- Values: view | view_and_edit | owner
-- Default: view (read-only access)

ALTER TABLE space_roles
    ADD COLUMN IF NOT EXISTS permission_level TEXT NOT NULL DEFAULT 'view'
    CHECK (permission_level IN ('view', 'view_and_edit', 'owner'));

COMMENT ON COLUMN space_roles.permission_level IS
    'view = read-only, view_and_edit = can edit schedule/tasks/constraints, owner = full control';
