ALTER TABLE users ADD COLUMN IF NOT EXISTS is_platform_admin BOOLEAN NOT NULL DEFAULT FALSE;
UPDATE users SET is_platform_admin = TRUE WHERE id = 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5';
