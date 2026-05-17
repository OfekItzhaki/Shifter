# 308 — ReAuthenticateCommand & Validator

## Phase
Admin Session Timeout — Backend Re-Authentication

## Purpose
Provides the command record and FluentValidation validator for the re-authentication flow. The validator ensures that requests always include a user identity and at least one credential method (password or WebAuthn assertion), rejecting malformed requests before they reach the handler.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Auth/Commands/ReAuthenticateCommand.cs` | Command record with `UserId`, `Password?`, `WebAuthnChallengeId?`, `WebAuthnAssertionJson?`, `SpaceId?`, `IpAddress?` and result record |
| `apps/api/Jobuler.Application/Auth/Validators/ReAuthenticateCommandValidator.cs` | FluentValidation validator enforcing non-empty UserId and requiring either password or both WebAuthn fields |

## Key decisions
- Validator uses a `Must` rule on the full command to express the "either/or" credential requirement, matching the pattern used in `RegisterCommandValidator`
- WebAuthn requires both `ChallengeId` and `AssertionJson` to be present together — providing only one is invalid
- No maximum length validation on password here; the handler rejects passwords > 128 chars before hashing (per design doc)

## How it connects
- The command is dispatched by `AuthController.POST /auth/re-authenticate` (task 4.1)
- The handler (task 2.1) implements the actual credential verification logic
- FluentValidation pipeline automatically runs this validator before the handler executes

## How to run / verify
```bash
cd apps/api/Jobuler.Application
dotnet build
```
Build should succeed with no new errors.

## What comes next
- Task 2.1 completion: implement the `ReAuthenticateCommandHandler` with BCrypt/WebAuthn verification and audit logging
- Task 2.3–2.5: property tests for password verification, error response, and audit log completeness

## Git commit
```bash
git add -A && git commit -m "feat(admin-session-timeout): add ReAuthenticateCommand record and validator"
```
