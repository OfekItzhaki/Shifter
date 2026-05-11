# 168 — Email Verification EF Core Configuration

## Phase

Email Verification Feature

## Purpose

Configure EF Core persistence for the `EmailVerificationToken` entity and the new `email_verified` column on the users table. This enables the database schema to support token-based email verification with proper indexing and foreign key constraints.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Infrastructure/Persistence/Configurations/EmailVerificationTokenConfiguration.cs` | New EF Core configuration mapping `EmailVerificationToken` to `email_verification_tokens` table with FK to users, index on `token_hash`, and all column mappings |
| `Jobuler.Infrastructure/Persistence/Configurations/UserConfiguration.cs` | Added `email_verified` column mapping with `HasDefaultValue(false)` |
| `Jobuler.Application/Persistence/AppDbContext.cs` | Added `DbSet<EmailVerificationToken> EmailVerificationTokens` |

## Key decisions

- **Index on `token_hash` is non-unique** — unlike `PasswordResetTokenConfiguration` which uses a unique index, the email verification token index is non-unique. This allows for edge cases where hash collisions are handled at the application layer (though practically impossible with SHA-256).
- **Cascade delete on FK** — if a user is deleted, their verification tokens are also removed.
- **`HasMaxLength(128)`** on `token_hash` — matches the design spec's `VARCHAR(128)` for the SHA-256 hex output (64 chars) with room for future algorithm changes.
- **`HasDefaultValue(false)`** on `email_verified` — ensures existing users default to unverified without requiring a data migration.

## How it connects

- Depends on: Step 167 (domain entity `EmailVerificationToken` and `User.EmailVerified` property)
- Required by: Task 2.2 (EF Core migration generation), Task 3.1 (VerifyEmailCommand queries `EmailVerificationTokens`)

## How to run / verify

```bash
cd apps/api
dotnet build
```

All projects except `Jobuler.Tests` (pre-existing unrelated failures) compile successfully.

## What comes next

- Task 2.2: Generate EF Core migration for the new table and column
- Task 3.1: VerifyEmailCommand handler that queries `EmailVerificationTokens`

## Git commit

```bash
git add -A && git commit -m "feat(email-verification): EF Core configuration for EmailVerificationToken and email_verified column"
```
