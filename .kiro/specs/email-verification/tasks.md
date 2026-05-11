# Implementation Plan: Email Verification

## Overview

Implement non-blocking email verification following the existing `ForgotPassword`/`ResetPassword` pattern. Backend uses C# with MediatR commands, EF Core persistence, and SHA-256 hashed tokens. Frontend uses Next.js with a verify-email page, verification banner, and API client additions.

## Tasks

- [ ] 1. Domain layer: EmailVerificationToken entity and User modification
  - [ ] 1.1 Create the EmailVerificationToken domain entity
    - Create `Jobuler.Domain/Identity/EmailVerificationToken.cs` following the `PasswordResetToken` pattern
    - Include properties: UserId, TokenHash, ExpiresAt, UsedAt
    - Include computed properties: IsExpired, IsUsed, IsValid
    - Include static factory method `Create(Guid userId, string tokenHash)` with 24h expiry
    - Include `MarkUsed()` method that sets UsedAt to UtcNow
    - _Requirements: 1.1, 1.2, 2.1, 2.4, 9.1_

  - [ ] 1.2 Add EmailVerified flag to User entity
    - Modify `Jobuler.Domain/Identity/User.cs` to add `EmailVerified` property (default false)
    - Add `MarkEmailVerified()` method that sets the flag and calls `Touch()`
    - _Requirements: 3.4, 4.1, 9.3_

- [ ] 2. Infrastructure layer: EF Core configuration and migration
  - [ ] 2.1 Add EF Core configuration for EmailVerificationToken
    - Add `EmailVerificationTokenConfiguration` in `Jobuler.Infrastructure/Persistence/Configurations/`
    - Configure table name `email_verification_tokens`, columns, FK to users, index on `token_hash`
    - Add `DbSet<EmailVerificationToken>` to `AppDbContext`
    - Configure the new `email_verified` column on the users table (default false)
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [ ] 2.2 Create EF Core migration
    - Generate migration for the new `email_verification_tokens` table and `email_verified` column on users
    - Verify migration creates the index on `token_hash` and FK constraint on `user_id`
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [ ] 3. Application layer: VerifyEmailCommand
  - [ ] 3.1 Implement VerifyEmailCommand and handler
    - Create `Jobuler.Application/Auth/Commands/VerifyEmailCommand.cs` with record `VerifyEmailCommand(string Token) : IRequest`
    - Create `Jobuler.Application/Auth/Commands/VerifyEmailCommandHandler.cs`
    - Handler logic: SHA-256 hash the raw token, find matching token (not expired, not used), mark token used, set User.EmailVerified = true, SaveChanges
    - Throw `InvalidOperationException` for invalid/expired/used tokens (uniform error for anti-enumeration)
    - _Requirements: 4.1, 4.2, 4.4, 8.1_

  - [ ] 3.2 Add FluentValidation validator for VerifyEmailCommand
    - Create validator ensuring Token is not empty
    - _Requirements: 4.1_

  - [ ]* 3.3 Write unit tests for VerifyEmailCommandHandler
    - Test valid token → user marked verified, token marked used
    - Test expired token → InvalidOperationException
    - Test already-used token → InvalidOperationException
    - Test non-existent token → InvalidOperationException
    - Test token for already-verified user → still marks token used and returns success
    - _Requirements: 4.1, 4.2, 4.4, 8.1_

  - [ ]* 3.4 Write property test for single-use enforcement (Property 4)
    - **Property 4: Single-use enforcement**
    - After a successful verification, all subsequent attempts with the same token must fail
    - **Validates: Requirements 2.3, 2.4, 4.4**

- [ ] 4. Application layer: ResendVerificationCommand
  - [ ] 4.1 Implement ResendVerificationCommand and handler
    - Create `Jobuler.Application/Auth/Commands/ResendVerificationCommand.cs` with record `ResendVerificationCommand(Guid UserId) : IRequest`
    - Create `Jobuler.Application/Auth/Commands/ResendVerificationCommandHandler.cs`
    - Handler logic: find user, check not already verified, invalidate existing active tokens, generate new 64-char hex token, hash it, create EmailVerificationToken, save, send email with verification link
    - Throw `InvalidOperationException("Email already verified")` if user is already verified
    - _Requirements: 5.1, 5.2, 5.3_

  - [ ]* 4.2 Write unit tests for ResendVerificationCommandHandler
    - Test unverified user → old tokens invalidated, new token created, email sent
    - Test already-verified user → InvalidOperationException
    - Test non-existent user → KeyNotFoundException
    - _Requirements: 5.1, 5.2, 5.3_

  - [ ]* 4.3 Write property test for resend invalidation (Property 7)
    - **Property 7: Resend invalidates old tokens and creates new**
    - For any unverified user with N active tokens, after resend all N are marked used and exactly one new valid token exists
    - **Validates: Requirements 5.1, 5.2**

- [ ] 5. Application layer: Modify RegisterCommand
  - [ ] 5.1 Modify RegisterCommandHandler to generate verification token and send email
    - After user creation and space setup, generate a 64-char hex token, hash with SHA-256
    - Create EmailVerificationToken entity and persist it
    - Send verification email with raw token link (try-catch so registration succeeds even if email fails)
    - _Requirements: 3.1, 3.2, 3.3_

  - [ ]* 5.2 Write unit tests for registration verification token creation
    - Test successful registration creates exactly one EmailVerificationToken
    - Test registration succeeds even when email sending throws
    - Test new user has EmailVerified = false
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [ ]* 5.3 Write property test for registration token creation (Property 9)
    - **Property 9: Registration creates verification token**
    - For any successful registration, exactly one token exists for the new user and EmailVerified is false
    - **Validates: Requirements 3.1, 3.2, 3.4**

- [ ] 6. Checkpoint - Backend application layer complete
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 7. API layer: AuthController endpoints
  - [ ] 7.1 Add verify-email endpoint to AuthController
    - Add `POST /auth/verify-email` with `[AllowAnonymous]` attribute
    - Accept `VerifyEmailRequest(string Token)` body
    - Dispatch `VerifyEmailCommand` via MediatR
    - Return 204 No Content on success, 400 on failure (via ExceptionHandlingMiddleware)
    - _Requirements: 4.1, 4.2, 4.3_

  - [ ] 7.2 Add resend-verification endpoint to AuthController
    - Add `POST /auth/resend-verification` with `[Authorize]` attribute
    - Extract userId from JWT claims
    - Dispatch `ResendVerificationCommand` via MediatR
    - Return 204 No Content on success, 400 if already verified
    - _Requirements: 5.3, 5.4, 5.5_

  - [ ] 7.3 Modify MeDto to include emailVerified field
    - Add `emailVerified` boolean field to the existing `MeDto` / `/auth/me` response
    - Map from `User.EmailVerified` in the query handler
    - _Requirements: 6.1, 6.2_

- [ ] 8. Frontend: API client and types
  - [ ] 8.1 Add verifyEmail and resendVerification to the API client
    - Add `verifyEmail(token: string): Promise<void>` function to `lib/api/auth.ts`
    - Add `resendVerification(): Promise<void>` function to `lib/api/auth.ts`
    - Update `MeDto` interface to include `emailVerified: boolean`
    - _Requirements: 4.1, 5.2, 7.1_

- [ ] 9. Frontend: Verify email page
  - [ ] 9.1 Create the verify-email page
    - Create `app/verify-email/page.tsx` as a client component
    - Extract `token` from URL search params
    - Call `verifyEmail(token)` on mount
    - Display loading, success, and error states
    - On error/expired, show a "Resend verification email" button (requires login)
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [ ] 9.2 Add i18n strings for verify-email page
    - Add English strings to `messages/en.json` (success, error, loading, resend button text)
    - Add Hebrew strings to `messages/he.json`
    - Add Russian strings to `messages/ru.json`
    - _Requirements: 7.2, 7.3_

- [ ] 10. Frontend: Verification banner
  - [ ] 10.1 Create VerificationBanner component
    - Create `components/shell/VerificationBanner.tsx`
    - Check `emailVerified` from auth context / `/auth/me` response
    - Show dismissible banner with "Verify your email" message and resend button
    - Hide if user is verified or banner is dismissed (session-scoped)
    - Does NOT block any functionality
    - _Requirements: 6.1, 6.2, 6.3_

  - [ ] 10.2 Integrate VerificationBanner into AppShell
    - Import and render `VerificationBanner` in `components/shell/AppShell.tsx`
    - Position it prominently but non-intrusively (e.g., below the header)
    - _Requirements: 6.2_

  - [ ] 10.3 Add i18n strings for verification banner
    - Add banner text, resend button, and dismiss label to all locale files (en, he, ru)
    - _Requirements: 6.2_

- [ ] 11. Final checkpoint - Full integration
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The `verify-email` endpoint is `[AllowAnonymous]` per security rules (user may not be logged in when clicking the link)
- The `resend-verification` endpoint requires `[Authorize]` per security rules
- Email sending failure during registration must not block registration (fire-and-forget with try-catch)
- The verification banner is non-blocking — it never gates access to features

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2"] },
    { "id": 3, "tasks": ["3.1", "3.2", "4.1", "5.1"] },
    { "id": 4, "tasks": ["3.3", "3.4", "4.2", "4.3", "5.2", "5.3"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 6, "tasks": ["8.1"] },
    { "id": 7, "tasks": ["9.1", "9.2", "10.1", "10.3"] },
    { "id": 8, "tasks": ["10.2"] }
  ]
}
```
