-- Migration 008: Group settings (solver horizon) + group invitations

-- Add solver horizon to groups (default 7 days)
ALTER TABLE groups
    ADD COLUMN IF NOT EXISTS solver_horizon_days INT NOT NULL DEFAULT 7,
    ADD COLUMN IF NOT EXISTS created_by_user_id UUID REFERENCES users(id);

-- Group invitations table (tracks email-based adds + opt-out tokens)
CREATE TABLE IF NOT EXISTS group_invitations (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id        UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    group_id        UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    email           TEXT NOT NULL,
    person_id       UUID REFERENCES people(id) ON DELETE SET NULL,
    invited_by_user_id UUID REFERENCES users(id),
    opt_out_token   TEXT NOT NULL UNIQUE,
    status          TEXT NOT NULL DEFAULT 'active', -- active | opted_out
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    opted_out_at    TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_group_invitations_group ON group_invitations (group_id);
CREATE INDEX IF NOT EXISTS idx_group_invitations_email ON group_invitations (email);
CREATE INDEX IF NOT EXISTS idx_group_invitations_token ON group_invitations (opt_out_token);
