# 511 — Re-Auth Audit Log Method & Outcome Property Test

## Phase

Admin Re-Auth Security — Property-Based Testing

## Purpose

Validates that the `ReAuthenticateCommandHandler` correctly records the authentication method ("password" or "webauthn") and the outcome (success or failure) in the audit log for every re-authentication attempt. This ensures audit trail integrity per Requirements 6.1, 6.2, and 6.4.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Application/ReAuthAuditLogMethodOutcomePropertyTests.cs` | FsCheck property-based tests (3 properties, 100 iterations each) verifying audit log method and outcome correctness |

## Key decisions

- Used FsCheck `Property` return type with `Prop.ForAll` for true property-based testing with 100+ iterations
- Three complementary test methods: password-only, WebAuthn-only, and combined (random method + outcome)
- Used NSubstitute to mock `IAuditLogger` with argument capture to inspect the `afterJson` payload
- Used InMemory EF Core database for isolation between test iterations
- WebAuthn success/failure controlled via mock `IWebAuthnService` that either returns a valid result or throws
- Password success/failure controlled by providing correct vs incorrect password against BCrypt hash

## How it connects

- Tests the `LogAttempt` method in `ReAuthenticateCommandHandler` (task 1.3)
- Validates the audit log contract defined in the security rules and Requirements 6.1, 6.2, 6.4
- Complements the audit log entry completeness test (task 1.6) which checks all required fields

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~ReAuthAuditLogMethodOutcomePropertyTests" --verbosity normal
```

All 3 tests should pass (100 iterations each = 300 total scenarios verified).

## What comes next

- Task 1.6: Property test for audit log entry completeness (all required fields present)

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): property test for audit log method and outcome correctness"
```
