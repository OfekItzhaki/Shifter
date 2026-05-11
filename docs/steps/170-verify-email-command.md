# 170 — Verify Email Command and Handler

## Phase
Email Verification Feature

## Purpose
Implements the `VerifyEmailCommand` and its handler, which validates a raw verification token and marks the associated user's email as verified. This is the core verification logic that the API endpoint will invoke when a user clicks their verification link.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Auth/Commands/VerifyEmailCommand.cs` | MediatR command record and handler that hashes the incoming token, looks up the matching `EmailVerificationToken`, marks it used, and sets `User.EmailVerified = true` |

## Key decisions

- **Reuses `IJwtService.HashToken()`** for SHA-256 hashing, consistent with the `ResetPasswordCommand` pattern.
- **Uniform error message** ("Invalid or expired verification token") for all failure cases (non-existent, expired, already used) to prevent token enumeration.
- **Single file** contains both the record and handler, matching the project convention for simple commands.
- **No validator class** — the token is a simple non-empty string; validation happens in the handler logic itself.

## How it connects

- Depends on `EmailVerificationToken` domain entity (step 167) and `User.MarkEmailVerified()` method (step 167).
- Depends on `AppDbContext.EmailVerificationTokens` DbSet and EF configuration (step 168).
- Will be called by the `AuthController` verify-email endpoint (upcoming task).
- Follows the same pattern as `ResetPasswordCommand` / `ResetPasswordCommandHandler`.

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no errors.

## What comes next

- API endpoint (`POST /auth/verify-email`) in `AuthController` that dispatches this command.
- `ResendVerificationCommand` for re-sending verification emails.
- Frontend verify-email page that calls the endpoint.

## Git commit

```bash
git add -A && git commit -m "feat(email-verification): implement VerifyEmailCommand and handler"
```
