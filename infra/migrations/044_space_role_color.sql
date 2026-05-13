-- Migration 044: Add optional color column to space_roles for visual role identification
-- Allows admins to assign a hex color to roles for visual distinction in schedule views.

ALTER TABLE space_roles
    ADD COLUMN IF NOT EXISTS color TEXT DEFAULT NULL;

-- Ensure only valid hex colors or NULL are stored
ALTER TABLE space_roles
    ADD CONSTRAINT chk_space_roles_color_hex
    CHECK (color IS NULL OR color ~ '^#[0-9a-fA-F]{6}$');

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('044') ON CONFLICT DO NOTHING;
