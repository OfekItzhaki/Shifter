# 171 — Resend Verification Command

## Phase

Email Verification — Application Layer

## Purpose

Implement the `ResendVerificationCommand` and its handler so authenticated unverified users can request a new verification email. This invalidates any existing active tokens, generates a fresh 64-char hex token (stored as SHA-256 hash), and sends a localized verification email.

## What was built

| File | Action | Description |
|------|--------|-------------|
| `apps/api/Jobuler.Application/Auth/Commands/ResendVerificationCommand.cs` | Created | MediatR command record and handler — invalidates old tokens, generates new token, sends email |

## Key decisions

- Followed the `ForgotPasswordCommand` pattern: invalidate existing active tokens before creating a new one.
- Used `IEmailSender` (not `INotificationSender`) since verification is an email-only flow.
- Token generation uses `RandomNumberGenerator.GetBytes(32)` → `Convert.ToHexString().ToLowerInvariant()` for 64-char hex.
- SHA-256 hashing done inline (same as `VerifyEmailCommand`) rather than via `IJwtService.HashToken()` to keep the command self-contained.
- Email sending wrapped in try-catch so the command doesn't fail if email delivery fails — the token is still saved and can be resent.
- Frontend base URL read from `App:FrontendBaseUrl` config (same pattern as `InvitePersonCommand`).
- Localized email subject and body for `he`, `ru`, and English (default).

## How it connects

- Called by `AuthController.ResendVerification` endpoint (task 7.2).
- Depends on `EmailVerificationToken` domain entity (task 1.1) and `AppDbContext.EmailVerificationTokens` DbSet (task 2.1).
- Tokens created here are consumed by `VerifyEmailCommand` (task 3.1).
- Uses `IEmailSender` registered in DI (SendGrid in production, NoOp in dev).

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no errors.

## What comes next

- Task 4.2: Unit tests for `ResendVerificationCommandHandler`
- Task 4.3: Property test for resend invalidation (Property 7)
- Task 5.1: Modify `RegisterCommandHandler` to generate verification token on registration

## Git commit

```bash
git add -A && git commit -m "feat(email-verification): implement ResendVerificationCommand and handler"
```
