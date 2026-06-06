-- Migration 082: Add lookup hashes for encrypted auth contact fields.
-- Email and phone values remain in their existing columns, but application code
-- writes them encrypted and uses these HMAC hashes for login/uniqueness lookup.

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS email_lookup_hash VARCHAR(64),
    ADD COLUMN IF NOT EXISTS phone_lookup_hash VARCHAR(64);

DROP INDEX IF EXISTS idx_users_email;
DROP INDEX IF EXISTS uq_users_phone_number;

CREATE UNIQUE INDEX IF NOT EXISTS uq_users_email_lookup_hash
    ON users (email_lookup_hash)
    WHERE email_lookup_hash IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_users_phone_lookup_hash
    ON users (phone_lookup_hash)
    WHERE phone_lookup_hash IS NOT NULL;

INSERT INTO schema_migrations (version)
VALUES ('082_user_contact_field_protection')
ON CONFLICT (version) DO NOTHING;
