# 368 — Admin Re-Auth Gate Handler Verification

## Phase

Security Hardening — Admin Re-Authentication Gate

## Purpose

Verify and confirm that the existing `ReAuthenticateCommandHandler` meets all security requirements for the admin re-authentication gate feature. This is a verification-only step — no code changes were needed.

## What was verified

### Files reviewed (no modifications required)

| File | Verification |
|------|-------------|
| `apps/api/Jobuler.Application/Auth/Commands/ReAuthenticateCommand.cs` | Handler logic, all failure paths, audit logging |
| `apps/api/Jobuler.Application/Auth/Commands/RegisterCommandHandler.cs` | BCrypt work factor = 12 at registration |
| `apps/api/Jobuler.Application/Auth/Commands/ResetPasswordCommand.cs` | BCrypt work factor = 12 at password reset |
| `apps/api/Jobuler.Application/Auth/Validators/ReAuthenticateCommandValidator.cs` | Validator ensures either password OR WebAuthn |
| `apps/api/Jobuler.Api/Controllers/AuthController.cs` | Endpoint response shape, attributes |
| `apps/api/Jobuler.Application/Common/IAuditLogger.cs` | Audit logger interface signature |

### Requirements verified

1. **BCrypt work factor ≥ 12 (Req 8.1)** ✅ — `RegisterCommandHandler` uses `workFactor: 12`. `ResetPasswordCommand` also uses `workFactor: 12`. Consistent across all password hashing paths.

2. **Generic 401 for all failure paths (Req 3.3, 8.2)** ✅ — Handler returns `ReAuthenticateResult(false)` for:
   - Password > 128 characters (early rejection)
   - User not found or inactive
   - No valid credential provided
   - WebAuthn verification failure (disabled credential, bad assertion, exception)
   
   Controller maps `Success == false` → `Unauthorized(new { error = "Authentication failed." })`. No information leakage.

3. **Password > 128 char early rejection without BCrypt (Req 3.5)** ✅ — Check occurs before DB lookup or BCrypt.Verify call. Returns failure immediately with audit log.

4. **Audit log for every attempt (Req 8.5)** ✅ — `LogAttempt` called in all paths:
   - Password too long → logged as failure
   - User not found → logged as failure
   - No valid credential → logged as failure
   - After verification → logged with actual result
   
   Audit entry includes: `actorUserId`, `spaceId`, `action` ("re_authenticate"), `entityType` ("user"), `entityId`, `afterJson` (contains `method` and `success`), `ipAddress`.

5. **Validator ensures either password OR WebAuthn (Req 3.1, 4.2)** ✅ — FluentValidation rule:
   ```csharp
   .Must(x => !string.IsNullOrEmpty(x.Password) ||
              (!string.IsNullOrEmpty(x.WebAuthnChallengeId) &&
               !string.IsNullOrEmpty(x.WebAuthnAssertionJson)))
   ```

6. **Rate limiting (Req 8.4)** ✅ — Controller class has `[EnableRateLimiting("auth")]` attribute. The `re-authenticate` endpoint inherits this.

7. **WebAuthn disabled credential rejection** ✅ — Query filters with `!c.IsDisabled`, returns generic failure if credential is disabled or not found.

## Key decisions

- No code changes required — the existing implementation already satisfies all acceptance criteria for task 1.1.
- The handler correctly separates concerns: early rejection (password length), user lookup, credential verification, and audit logging.
- The generic error pattern is consistent: all failure paths return the same `ReAuthenticateResult(false)` which the controller maps to identical 401 responses.

## How it connects

- This handler is called by `AuthController.ReAuthenticate` endpoint (`POST /auth/re-authenticate`)
- The frontend `ReAuthDialog` component calls this endpoint during the re-auth flow
- Audit entries are consumed by the platform admin audit log viewer
- Rate limiting is shared with the login endpoint via the "auth" policy

## How to verify

```bash
# Build the API project to confirm no compilation issues
cd apps/api && dotnet build
```

## What comes next

- Task 1.2: Verify AuthController response shape (already confirmed here as well)
- Task 1.3–1.6: Property-based tests for password length boundary, generic error response, credential verification, and audit log completeness

## Git commit

```bash
git add -A && git commit -m "docs(security): verify admin reauth handler meets all security requirements"
```
