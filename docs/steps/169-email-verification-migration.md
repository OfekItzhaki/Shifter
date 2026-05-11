# 169 — Email Verification Migration

## Phase

Feature — Email Verification

## Purpose

Creates the SQL migration that adds the `email_verification_tokens` table and the `email_verified` column to the `users` table. This provides the persistence layer for the email verification feature.

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/041_email_verification.sql` | SQL migration creating the `email_verification_tokens` table with FK to users, index on `token_hash`, and adding `email_verified` boolean column to `users` |

## Key decisions

- Followed the `password_reset_tokens` pattern (migration 013) since both are user-scoped token tables
- Used `VARCHAR(128)` for `token_hash` to match the EF Core configuration
- No RLS policy needed — `email_verification_tokens` is not tenant-scoped (it's user-scoped, same as `password_reset_tokens` and `refresh_tokens`)
- Used `IF NOT EXISTS` / `IF NOT EXISTS` guards for idempotent re-runs
- Added index on `user_id` for efficient lookups when invalidating tokens during resend

## How it connects

- Depends on: `001_core_identity.sql` (users table), task 2.1 (EF Core configuration)
- Used by: Application layer commands (VerifyEmailCommand, ResendVerificationCommand, RegisterCommand)
- The EF Core configuration in `EmailVerificationTokenConfiguration.cs` maps to this table schema

## How to run / verify

```bash
# Apply migration against local PostgreSQL
psql -U postgres -d jobuler -f infra/migrations/041_email_verification.sql

# Verify table was created
psql -U postgres -d jobuler -c "\d email_verification_tokens"

# Verify index exists
psql -U postgres -d jobuler -c "\di ix_email_verification_tokens_token_hash"

# Verify email_verified column on users
psql -U postgres -d jobuler -c "\d users" | grep email_verified
```

## What comes next

- Application layer commands (VerifyEmailCommand, ResendVerificationCommand) that read/write this table
- Modification of RegisterCommand to create tokens on registration

## Git commit

```bash
git add -A && git commit -m "feat(email-verification): add migration for email_verification_tokens table and email_verified column"
```
