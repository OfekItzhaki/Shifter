-- Migration: Add reauth_attempts table for lockout tracking
-- Feature: admin-reauth-security

CREATE TABLE IF NOT EXISTS reauth_attempts (
    id uuid NOT NULL PRIMARY KEY,
    user_id uuid NOT NULL,
    attempted_at timestamp with time zone NOT NULL,
    success boolean NOT NULL,
    method varchar(20) NOT NULL,
    created_at timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_reauth_attempts_user_id_attempted_at"
    ON reauth_attempts (user_id, attempted_at DESC);
