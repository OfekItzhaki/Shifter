-- Track one-time migration of existing users' groups into a Space
CREATE TABLE IF NOT EXISTS user_space_migrations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id),
    space_id UUID NOT NULL REFERENCES spaces(id),
    migrated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    groups_migrated INT NOT NULL DEFAULT 0,
    CONSTRAINT uq_user_space_migrations_user_id UNIQUE (user_id)
);
