-- Migration 015: Pending invitations + invitation_status on people

-- Add invitation_status column to people table
ALTER TABLE people
    ADD COLUMN IF NOT EXISTS invitation_status VARCHAR(20) NOT NULL DEFAULT 'accepted';

-- Create pending_invitations table
CREATE TABLE IF NOT EXISTS pending_invitations (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    space_id            UUID NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
    person_id           UUID NOT NULL REFERENCES people(id) ON DELETE CASCADE,
    contact             TEXT NOT NULL,
    channel             VARCHAR(20) NOT NULL,
    token_hash          VARCHAR(64) NOT NULL,
    is_accepted         BOOLEAN NOT NULL DEFAULT FALSE,
    expires_at          TIMESTAMPTZ NOT NULL,
    invited_by_user_id  UUID REFERENCES users(id),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_invitation_channel CHECK (channel IN ('email', 'whatsapp'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_pending_invitations_token_hash
    ON pending_invitations (token_hash);

CREATE INDEX IF NOT EXISTS idx_pending_invitations_person
    ON pending_invitations (space_id, person_id);

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('015') ON CONFLICT DO NOTHING;
