# Step 505 — ReAuthenticate Handler Lockout Logic

## Phase

Admin Re-Auth Security — Backend lockout and audit infrastructure

## Purpose

Enhances the `ReAuthenticateCommandHandler` with rate-limiting lockout logic to prevent brute-force attacks on the re-authentication endpoint. After 5 failed attempts within a 15-minute window, the user is locked out and receives a 429 response. All attempts (success and failure) are recorded in the `reauth_attempts` table for tracking, and WebAuthn failure reasons are included in audit log entries.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Auth/Commands/ReAuthenticateCommand.cs` | Enhanced handler with lockout check, attempt recording, WebAuthn failure reason in audit log, and updated result record |
| `apps/api/Jobuler.Api/Controllers/AuthController.cs` | Updated `ReAuthenticateRequest` to accept `WebAuthnFailureReason`, controller passes it to command and returns 429 on lockout |

## Key decisions

1. **Sliding window lockout** — Counts failures in the last 15 minutes using the `reauth_attempts` table. No explicit "reset" needed; successful attempts don't clear old failures, they simply age out of the window naturally.
2. **Lockout check before credential verification** — The lockout query runs before any password hashing or WebAuthn verification to prevent resource consumption during lockout.
3. **Separate audit actions** — Normal attempts use `re_authenticate` action; lockout events use `re_authenticate_lockout` to distinguish them in audit queries.
4. **WebAuthn failure reason in afterJson** — When a WebAuthn attempt fails and the client provides a failure reason (cancelled, timeout, credential_not_recognized), it's included in the audit log's `afterJson` field.
5. **Password > 128 chars rejection** — Already existed; now also records the attempt in `reauth_attempts` for lockout tracking.
6. **Result record extended** — `ReAuthenticateResult` now includes `IsLockedOut` and `RetryAfterSeconds` fields (with defaults for backward compatibility).

## How it connects

- **Depends on**: Task 1.1 (`ReAuthAttempt` domain entity) and Task 1.2 (EF configuration + `DbSet<ReAuthAttempt>` in `AppDbContext`)
- **Used by**: Task 1.4 (controller lockout response) — already wired in this step since the controller change was minimal
- **Frontend**: The 429 response with `retryAfterSeconds` is consumed by the `ReAuthDialog` lockout state (Task 3.3)
- **Audit log**: Entries follow the security rules (actor_user_id, space_id, action, entity_type, entity_id, IP address, after-snapshot)

## How to run / verify

```bash
# Build the API project
cd apps/api
dotnet build Jobuler.Api/Jobuler.Api.csproj

# Run the API and test manually:
# 1. Authenticate, then POST /auth/re-authenticate with wrong password 5 times
# 2. 6th attempt should return 429 with { error: "Too many attempts", retryAfterSeconds: 900 }
# 3. Check audit_logs table for re_authenticate and re_authenticate_lockout entries
# 4. Check reauth_attempts table for recorded attempts
```

## What comes next

- Task 1.5: Property test for audit log method and outcome correctness
- Task 1.6: Property test for audit log entry completeness
- Frontend tasks (3.x, 4.x) consume the 429 response and display lockout UI

## Git commit

```bash
git add -A && git commit -m "feat(reauth): add lockout logic and attempt tracking to ReAuthenticateCommandHandler"
```
