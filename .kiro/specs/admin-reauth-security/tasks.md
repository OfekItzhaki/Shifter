# Implementation Plan: Admin Re-Auth Security

## Overview

This plan hardens the admin re-authentication dialog by refactoring `ReAuthDialog.tsx` to prevent password manager autofill, prioritize WebAuthn/biometric as the primary re-auth method, and provide manual password entry as fallback. Backend changes add lockout logic (5 failures / 15 minutes) with a `ReAuthAttempt` entity and enhanced audit logging. The existing login form remains unchanged.

## Tasks

- [x] 1. Backend — ReAuthAttempt entity and lockout infrastructure
  - [x] 1.1 Create `ReAuthAttempt` domain entity in `Jobuler.Domain/Auth/ReAuthAttempt.cs`
    - Add properties: Id (Guid), UserId (Guid), AttemptedAt (DateTime), Success (bool), Method (string)
    - Implement `Create(Guid userId, bool success, string method)` factory method
    - _Requirements: 6.1, 6.2, 6.5_

  - [x] 1.2 Create EF configuration and migration for `reauth_attempts` table
    - Create `ReAuthAttemptConfiguration` in `Jobuler.Infrastructure/Persistence/Configurations/`
    - Map to `reauth_attempts` table with snake_case columns
    - Add composite index on `(user_id, attempted_at DESC)` for efficient lockout queries
    - Register `DbSet<ReAuthAttempt>` in `AppDbContext`
    - Generate migration via `dotnet ef migrations add AddReAuthAttempts`
    - _Requirements: 6.5_

  - [x] 1.3 Enhance `ReAuthenticateCommandHandler` with lockout logic
    - Before verifying credentials: query `reauth_attempts` for failures in last 15 minutes for the user
    - If count >= 5, return 429 with `retryAfterSeconds` and log lockout event to audit log
    - After failed attempt: insert `ReAuthAttempt.Create(userId, false, method)`
    - After successful attempt: insert `ReAuthAttempt.Create(userId, true, method)` (counter resets naturally via time window)
    - Add WebAuthn failure reason (cancelled, timeout, credential not recognized) to audit log `afterJson`
    - Reject passwords > 128 chars without hashing (DoS prevention)
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [x] 1.4 Update `AuthController.ReAuthenticate` to return lockout response
    - When handler returns failure and lockout is active, return 429 with `{ error: "Too many attempts", retryAfterSeconds: 900 }`
    - Distinguish between 401 (auth failed) and 429 (rate limited/locked out)
    - _Requirements: 3.7, 6.5_

  - [x]* 1.5 Write property test for audit log method and outcome correctness
    - **Property 4: Audit log method and outcome correctness**
    - For any re-authentication attempt (password or WebAuthn, success or failure), the audit log entry correctly records the method and outcome
    - **Validates: Requirements 6.1, 6.2, 6.4**

  - [x]* 1.6 Write property test for audit log entry completeness
    - **Property 5: Audit log entry completeness**
    - For any re-authentication attempt, the audit log entry contains: actor_user_id, space_id, action, entity_type, entity_id, IP address, and after-snapshot with method and outcome
    - **Validates: Requirements 6.6**

- [x] 2. Checkpoint — Backend lockout and audit complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Frontend — Credential detection and WebAuthn integration
  - [x] 3.1 Add WebAuthn credential check API call to `ReAuthDialog`
    - On dialog open, call `GET /auth/webauthn/credentials` with a 5-second `AbortController` timeout
    - While loading, display a loading indicator and disable all auth actions
    - On timeout (>5s): abort request, fall back to password form, log warning to console
    - On network error or non-success status: fall back to password form, log error to console
    - On empty credentials list: treat as "no registered credentials"
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 3.2 Implement WebAuthn biometric authentication flow in `ReAuthDialog`
    - Check `navigator.credentials?.get` availability; if unsupported, hide WebAuthn option entirely
    - When credentials exist and browser supports WebAuthn: display biometric button as primary action (larger, filled background) above a secondary "Use password instead" link
    - On biometric button click: call `POST /auth/webauthn/login/options`, then invoke `navigator.credentials.get()` with `timeout: 60000`
    - On assertion success: submit to `POST /auth/re-authenticate` with WebAuthn assertion data, call `onSuccess` on success response
    - On assertion failure/cancel: display error message (cancelled, timed out, or credential not recognized), keep retry available, show password link
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 3.3 Add state management for WebAuthn and lockout
    - Add state fields: `credentialCheckLoading`, `hasWebAuthnCredentials`, `webAuthnSupported`, `activeMethod`, `webAuthnLoading`, `webAuthnError`, `isLockedOut`, `lockoutRemainingSeconds`
    - Implement countdown timer for lockout: disable submit button, re-enable after `retryAfterSeconds` elapses
    - Handle 429 response: parse `retryAfterSeconds`, display "Too many attempts" error, disable submit
    - _Requirements: 2.1, 3.7_

- [x] 4. Frontend — Autofill prevention and password form hardening
  - [x] 4.1 Apply autofill prevention attributes to `ReAuthDialog` password input
    - Set `autocomplete="new-password"` on the password input (replace current `autocomplete="current-password"`)
    - Set `name="reauth-verify"` on the password input
    - Set form-level `autocomplete="off"` on the wrapping `<form>` element
    - Implement readonly trick: render input with `readOnly={true}` on mount, remove on focus via `onFocus` handler
    - Ensure password field value is always empty string when dialog opens
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 4.2 Harden password form validation and error handling
    - Prevent submission when password is empty or whitespace-only; display inline validation error and keep focus on input
    - On 401 response: display "Invalid credentials" error, clear password field, re-enable submit, refocus input
    - On network error: display "Connection problem" error, retain password in field, re-enable submit for retry
    - On 429 response: display "Too many attempts" error, disable submit, re-enable after cooldown
    - _Requirements: 3.3, 3.4, 3.5, 3.6, 3.7_

  - [x] 4.3 Ensure dialog renders no DOM when closed
    - Verify existing `if (!open) return null` behavior is preserved
    - Ensure no input or form elements are rendered when `open` is `false`
    - _Requirements: 5.3_

  - [x]* 4.4 Write property test for dialog state reset on open
    - **Property 1: Dialog state reset on open**
    - For any previous password value, when dialog transitions from closed to open, password field value is empty string
    - **Validates: Requirements 1.3**

  - [x]* 4.5 Write property test for whitespace password rejection
    - **Property 2: Whitespace password rejection**
    - For any string composed entirely of whitespace, attempting to submit is prevented, no API call is made, and a validation error is displayed
    - **Validates: Requirements 3.4**

  - [x]* 4.6 Write property test for no DOM rendering when closed
    - **Property 3: No DOM rendering when closed**
    - For any prop combination with `open=false`, the component renders no DOM elements
    - **Validates: Requirements 5.3**

- [x] 5. Checkpoint — Frontend re-auth dialog complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Scope verification — Login form unchanged
  - [x] 6.1 Verify login form retains password manager support
    - Confirm the login form at `/auth/login` retains `autocomplete="current-password"` on its password input
    - Confirm the login form does not suppress the browser's native credential-save prompt
    - Confirm autofill-prevention attributes (`autocomplete="off"`, `autocomplete="new-password"`, `name="reauth-verify"`) are NOT applied to the login form
    - _Requirements: 5.1, 5.2, 5.4_

  - [x]* 6.2 Write unit tests for scope isolation
    - Test that login form password input has `autocomplete="current-password"`
    - Test that ReAuthDialog is the only component with autofill-prevention attributes
    - _Requirements: 5.1, 5.2_

- [x] 7. Final checkpoint — Full integration
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use **fast-check** for frontend (TypeScript) and **xUnit** for backend (C#)
- The existing `POST /auth/re-authenticate` endpoint already supports both password and WebAuthn — no new endpoints needed
- The existing `GET /auth/webauthn/credentials` endpoint is reused for credential detection
- The `[EnableRateLimiting("auth")]` attribute on `AuthController` satisfies Requirement 6.3 (existing rate limiter)
- Step documentation under `docs/steps/` should be created alongside each implementation task per workspace conventions
- All audit log entries follow the security rules: actor_user_id, space_id, action, entity_type, entity_id, IP address, before/after snapshot

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "1.4"] },
    { "id": 3, "tasks": ["1.5", "1.6", "3.1"] },
    { "id": 4, "tasks": ["3.2", "3.3"] },
    { "id": 5, "tasks": ["4.1", "4.2", "4.3"] },
    { "id": 6, "tasks": ["4.4", "4.5", "4.6", "6.1"] },
    { "id": 7, "tasks": ["6.2"] }
  ]
}
```
