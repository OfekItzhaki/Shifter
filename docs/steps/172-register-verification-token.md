# Step 172 — Register Command: Verification Token & Email

## Phase
Email Verification Feature

## Purpose
Modify the `RegisterCommandHandler` to generate an email verification token and send a verification email immediately after user registration. This ensures every new user receives a verification link without blocking the registration flow if email delivery fails.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Application/Auth/Commands/RegisterCommandHandler.cs` | Added `IEmailSender`, `ILogger`, and `IConfiguration` dependencies. After user/space creation, generates a 64-char hex token (32 random bytes), hashes with SHA-256, persists an `EmailVerificationToken`, and sends a verification email wrapped in try-catch. |
| `Jobuler.Tests/Integration/UserRoleAssignmentFlowTests.cs` | Updated test to pass the new constructor dependencies (`IEmailSender`, `ILogger`, `IConfiguration`) using NSubstitute mocks. |

## Key decisions

- **Token generation uses `RandomNumberGenerator.GetBytes(32)`** — cryptographically secure, produces 256 bits of entropy (64 hex chars).
- **SHA-256 hash stored, raw token sent in email** — follows the same pattern as `ForgotPasswordCommand` and `ResendVerificationCommand`.
- **Email sending wrapped in try-catch** — registration must succeed even if email delivery fails (Requirement 3.3). Failure is logged at Warning level.
- **Reused email template from `ResendVerificationCommandHandler`** — consistent look and i18n support (he/ru/en).
- **Frontend base URL from `App:FrontendBaseUrl` config** — same source as password reset and resend verification.

## How it connects

- Depends on: `EmailVerificationToken` entity (step 167), EF Core configuration (step 168), `IEmailSender` interface
- Used by: The `/auth/register` endpoint (AuthController) — no controller changes needed since MediatR resolves the handler via DI
- Related: `ResendVerificationCommand` (step 171) uses the same token generation and email template pattern

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
# Build succeeds with no errors

cd ../Jobuler.Tests
dotnet test --filter "FullyQualifiedName~UserRoleAssignmentFlowTests"
# Test passes (note: NotificationTests has pre-existing unrelated build errors from push-notifications spec)
```

## What comes next

- Task 5.2: Unit tests for registration verification token creation
- Task 5.3: Property test for registration token creation (Property 9)

## Git commit

```bash
git add -A && git commit -m "feat(email-verification): generate verification token and send email on registration"
```
