-- Migration 048: WebAuthn credentials for biometric/passkey login
-- Stores FIDO2 credential data per user for passwordless authentication.

CREATE TABLE webauthn_credentials (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    credential_id   BYTEA NOT NULL,
    public_key      BYTEA NOT NULL,
    sign_count      INTEGER NOT NULL DEFAULT 0,
    transports      TEXT[] DEFAULT '{}',
    nickname        VARCHAR(100),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_used_at    TIMESTAMPTZ,
    is_disabled     BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX ix_webauthn_credentials_credential_id ON webauthn_credentials(credential_id);
CREATE INDEX ix_webauthn_credentials_user_id ON webauthn_credentials(user_id);
