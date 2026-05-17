# Step 308 — ReAuthenticateCommand and Handler

## Phase

Admin Session Timeout — Backend re-authentication

## Purpose

Implements the core re-authentication logic that verifies a user's identity before entering elevated privilege modes (Management Mode or Super Platform Mode). This is the security gate that prevents unattended sessions from being exploited.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Auth/Commands/ReAuthenticateCommand.cs` | Command record, result record, and handler implementing credential verification via BCrypt or WebAuthn, with audit logging on every attempt |

## Key decisions

- **Generic error on failure**: The handler returns `ReAuthenticateResult(false)` for all failure cases (wrong password, non-existent user, disabled account, invalid WebAuthn) without distinguishing the cause — prevents information leakage.
- **Password length guard**: Passwords exceeding 128 characters are rejected immediately without BCrypt hashing to prevent DoS via expensive hash computation.
- **Audit on every path**: Both success and failure create an audit log entry with method (password/webauthn) and success/failure status, satisfying security audit requirements.
- **WebAuthn sign count update**: On successful WebAuthn verification, the credential's sign count is updated to detect cloned credentials.
- **Reused patterns**: Follows the same `ExtractCredentialIdFromAssertion` and `Base64UrlDecode` patterns from `WebAuthnLoginCompleteCommand` for consistency.

## How it connects

- Used by the `POST /auth/re-authenticate` API endpoint (task 4.1)
- Depends on `IWebAuthnService` (Infrastructure layer) for WebAuthn verification
- Depends on `IAuditLogger` for audit trail entries
- The `ReAuthenticateCommandValidator` (task 2.2) provides input validation before this handler runs
- Frontend `ReAuthDialog` component (task 7.1) calls this via the API

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build
```

Build should succeed with no errors.

## What comes next

- Task 2.2: `ReAuthenticateCommandValidator` (FluentValidation rules)
- Task 4.1: API endpoint wiring in `AuthController`
- Task 2.3–2.5: Property-based tests for this handler

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): add ReAuthenticateCommand and handler"
```
