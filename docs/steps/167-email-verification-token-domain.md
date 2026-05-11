# 167 — Email Verification Token Domain Entity

## Phase

Email Verification — Domain Layer

## Purpose

Introduce the `EmailVerificationToken` domain entity that stores hashed verification tokens with 24-hour expiry and single-use enforcement. This follows the existing `PasswordResetToken` pattern and provides the foundation for the entire email verification feature.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Identity/EmailVerificationToken.cs` | New domain entity with UserId, TokenHash, ExpiresAt, UsedAt properties, computed IsExpired/IsUsed/IsValid, static `Create` factory, and `MarkUsed()` method |

## Key decisions

- **Follows PasswordResetToken pattern exactly** — same structure, same base class (`Entity`), same private constructor + static factory approach
- **24-hour expiry** — longer than the 1-hour password reset window, since email verification is less security-critical
- **Hash-only storage** — only the SHA-256 hash is stored; the raw token is never persisted (set by the caller via the `tokenHash` parameter)
- **Single-use via UsedAt** — `MarkUsed()` sets the timestamp, and `IsValid` checks both expiry and usage

## How it connects

- Extends `Entity` (provides `Id` and `CreatedAt`)
- Will be configured in EF Core in task 2.1 (table `email_verification_tokens`)
- Used by `VerifyEmailCommandHandler` (task 3.1) and `ResendVerificationCommandHandler` (task 4.1)
- Created during registration in `RegisterCommandHandler` (task 5.1)

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build --no-restore
```

Build should succeed with no errors or warnings.

## What comes next

- Task 1.2: Add `EmailVerified` flag to the `User` entity
- Task 2.1: EF Core configuration for the new entity

## Git commit

```bash
git add -A && git commit -m "feat(email-verification): add EmailVerificationToken domain entity"
```
