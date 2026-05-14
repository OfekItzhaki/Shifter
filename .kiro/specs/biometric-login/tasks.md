# Implementation Plan: Biometric Login (WebAuthn / Passkeys)

## Overview

Add passwordless biometric authentication using WebAuthn/FIDO2. The implementation uses Fido2NetLib for all cryptographic operations — our code is wiring: domain entity, EF Core persistence, MediatR commands, a thin controller, and a frontend utility calling `navigator.credentials`. Organized into 4 phases: backend infrastructure, backend service + commands, API controller, and frontend integration.

## Tasks

- [ ] 1. Phase 1 — Backend Infrastructure
  - [ ] 1.1 Add Fido2NetLib NuGet package and WebAuthn configuration
    - Add `Fido2NetLib` package reference to `Jobuler.Infrastructure.csproj`
    - Add `WebAuthn` configuration section to `appsettings.json` with `RelyingPartyId`, `RelyingPartyName`, `Origin`, `ChallengeTimeoutMinutes`
    - Add corresponding section to `appsettings.Development.json` with `localhost` values
    - _Requirements: 1.5, 9.2_

  - [ ] 1.2 Create WebAuthnCredential domain entity
    - Create `WebAuthnCredential.cs` in `Jobuler.Domain/Identity/`
    - Implement as per design: factory method `Create()`, `UpdateSignCount()` with monotonic check, `UpdateNickname()` with 100-char limit, `Disable()` method
    - Entity inherits from `Entity` base class, has `UserId`, `CredentialId`, `PublicKey`, `SignCount`, `Transports`, `Nickname`, `CreatedAt`, `LastUsedAt`, `IsDisabled`
    - No external dependencies (pure domain logic)
    - _Requirements: 10.1, 4.6, 6.2, 6.3, 9.6_

  - [ ]* 1.3 Write property tests for WebAuthnCredential entity
    - **Property 4: Sign count monotonic invariant** — For any credential with stored sign count N and reported sign count M: M > N succeeds and stores M; M <= N throws and disables credential
    - **Property 8: Nickname length validation** — For any string S: length <= 100 succeeds; length > 100 is rejected
    - **Validates: Requirements 4.3, 4.6, 6.1, 6.2, 6.3, 9.6**

  - [ ] 1.4 Create EF Core configuration and database migration
    - Create `WebAuthnCredentialConfiguration.cs` in `Jobuler.Infrastructure/Persistence/Configurations/` (or equivalent EF config folder)
    - Map to table `webauthn_credentials` with column types: `bytea` for CredentialId/PublicKey, `text[]` for Transports, `varchar(100)` for Nickname
    - Add unique index on `CredentialId`, index on `UserId`
    - Configure cascade delete from User
    - Add `DbSet<WebAuthnCredential>` to `AppDbContext`
    - Generate EF Core migration
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 12.1_

- [ ] 2. Checkpoint — Verify Phase 1
  - Ensure the project builds, migration is generated correctly, and domain entity unit tests pass. Ask the user if questions arise.

- [ ] 3. Phase 2 — Backend Service and Commands
  - [ ] 3.1 Create IWebAuthnService interface and Fido2Service implementation
    - Create `IWebAuthnService.cs` in `Jobuler.Application/Auth/`
    - Define methods: `GenerateRegistrationOptionsAsync`, `CompleteRegistrationAsync`, `GenerateAuthenticationOptionsAsync`, `CompleteAuthenticationAsync`
    - Create `Fido2Service.cs` in `Jobuler.Infrastructure/Auth/` implementing `IWebAuthnService`
    - Wrap `Fido2NetLib` library calls, use `IMemoryCache` for challenge storage with 5-minute TTL
    - Challenges stored with composite key `webauthn:challenge:{challengeId}`, single-use (delete on retrieval)
    - Register `IWebAuthnService` → `Fido2Service` in DI
    - _Requirements: 1.1, 1.5, 1.6, 3.1, 3.3, 3.4, 9.1, 9.2, 9.3, 9.5_

  - [ ]* 3.2 Write property tests for challenge generation and single-use
    - **Property 1: Challenge minimum length** — For any ceremony initiation, generated challenge is at least 16 bytes
    - **Property 9: Challenge single-use guarantee** — For any challenge used in verification, a second use is rejected
    - **Validates: Requirements 1.1, 3.1, 9.5**

  - [ ] 3.3 Create registration commands (options + complete)
    - Create `WebAuthnRegisterOptionsCommand.cs` and handler in `Jobuler.Application/Auth/Commands/`
    - Handler calls `IWebAuthnService.GenerateRegistrationOptionsAsync` with user data and existing credential IDs from DB
    - Create `WebAuthnRegisterCompleteCommand.cs` and handler
    - Handler retrieves challenge, calls `IWebAuthnService.CompleteRegistrationAsync`, persists `WebAuthnCredential` entity
    - Accept optional `nickname` parameter (max 100 chars)
    - Add FluentValidation validators for both commands
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 6.1_

  - [ ]* 3.4 Write property tests for registration options
    - **Property 2: Registration options contain user identity** — For any valid user, options include matching user entity (ID, email, display name)
    - **Property 3: Exclude list completeness** — For any user with N credentials, exclude list has exactly N entries matching existing credential IDs
    - **Validates: Requirements 1.2, 1.4**

  - [ ] 3.5 Create authentication commands (options + complete)
    - Create `WebAuthnLoginOptionsCommand.cs` and handler in `Jobuler.Application/Auth/Commands/`
    - Handler calls `IWebAuthnService.GenerateAuthenticationOptionsAsync`, allows discoverable credentials
    - Create `WebAuthnLoginCompleteCommand.cs` and handler
    - Handler retrieves challenge, looks up credential by ID, calls `IWebAuthnService.CompleteAuthenticationAsync`
    - On success: update sign count, update `LastUsedAt`, record login event on User, issue JWT + refresh token (reuse existing `IJwtService`)
    - On sign count regression: disable credential, reject with error
    - Add FluentValidation validators
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 9.1, 9.5, 9.6_

  - [ ]* 3.6 Write property test for token issuance parity
    - **Property 5: Token issuance parity** — For any valid user authenticating via WebAuthn, JWT claims and expiry match the email+password flow
    - **Validates: Requirements 4.2**

  - [ ] 3.7 Create credential management commands (list, delete, update nickname)
    - Create `ListWebAuthnCredentialsQuery.cs` and handler in `Jobuler.Application/Auth/Queries/`
    - Returns credential ID, nickname, created_at, last_used_at for all credentials belonging to the authenticated user
    - Create `DeleteWebAuthnCredentialCommand.cs` and handler
    - Verify credential belongs to requesting user (return 403 if not), then delete
    - Create `UpdateWebAuthnCredentialNicknameCommand.cs` and handler
    - Verify ownership, validate nickname length, update
    - Add FluentValidation validators
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 6.2, 6.3_

  - [ ]* 3.8 Write property tests for credential management
    - **Property 6: Credential listing completeness** — For any user with N credentials, list returns exactly N items with correct fields
    - **Property 7: Credential deletion ownership enforcement** — For any two distinct users, cross-user delete is always rejected
    - **Validates: Requirements 5.1, 5.2, 5.3**

- [ ] 4. Checkpoint — Verify Phase 2
  - Ensure all tests pass, the project builds, and all commands/queries are wired correctly. Ask the user if questions arise.

- [ ] 5. Phase 3 — API Controller
  - [ ] 5.1 Create WebAuthnController with all endpoints
    - Create `WebAuthnController.cs` in `Jobuler.Api/Controllers/`
    - Implement 7 endpoints as per design: register/options, register/complete, login/options, login/complete, credentials list, credentials delete, credentials patch
    - `[Authorize]` on registration and credential management endpoints
    - `[AllowAnonymous]` on login endpoints
    - Apply existing `"auth"` rate limiting policy
    - Controllers dispatch to MediatR only — no business logic
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 9.7_

  - [ ] 5.2 Wire DI registration for WebAuthn services
    - Register `Fido2` instance from `Fido2NetLib` with configuration from `appsettings.json`
    - Register `IWebAuthnService` → `Fido2Service` as scoped service
    - Ensure `IMemoryCache` is available (likely already registered)
    - _Requirements: 1.5, 3.3_

- [ ] 6. Checkpoint — Verify Phase 3
  - Ensure the project builds, all endpoints are reachable, and rate limiting is applied. Ask the user if questions arise.

- [ ] 7. Phase 4 — Frontend
  - [ ] 7.1 Create `lib/webauthn.ts` utility module
    - Feature detection: `window.PublicKeyCredential !== undefined`
    - Base64url ↔ ArrayBuffer conversion helpers
    - `registerCredential(nickname?: string)` — calls register/options, invokes `navigator.credentials.create()`, calls register/complete
    - `authenticateWithBiometric()` — calls login/options, invokes `navigator.credentials.get()`, calls login/complete
    - `listCredentials()`, `deleteCredential(id)`, `updateCredentialNickname(id, nickname)` — API wrappers
    - Handle `NotAllowedError` (user cancellation) gracefully
    - _Requirements: 7.2, 7.3, 7.4, 8.1, 8.2, 8.3, 8.4_

  - [ ] 7.2 Add "Login with biometric" button to login page
    - Conditionally render button only when WebAuthn is supported (feature detection from `lib/webauthn.ts`)
    - Position prominently above email+password form on mobile viewports
    - On click: call `authenticateWithBiometric()`, store tokens, redirect to app
    - On failure: show dismissible error, keep password form available as fallback
    - Hide entirely if browser doesn't support WebAuthn
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [ ] 7.3 Add biometric registration section to profile/settings page
    - Show prompt to enable biometric login when user has no registered credentials
    - Registration flow: prompt for optional nickname → call `registerCredential(nickname)` → show success confirmation
    - Handle user cancellation of authenticator prompt with dismissible message and retry option
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [ ] 7.4 Add credential management UI (list, delete, rename)
    - Display list of registered credentials with nickname, creation date, last used date
    - Delete button per credential with confirmation dialog
    - Inline edit for credential nickname
    - _Requirements: 5.1, 5.2, 5.5, 6.2_

- [ ] 8. Final Checkpoint — Verify all phases
  - Ensure all tests pass, the full registration and authentication flows work end-to-end, and the frontend correctly handles feature detection and error states. Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation between phases
- Property tests use `FsCheck.Xunit` and validate universal correctness properties from the design
- Unit tests validate specific examples and edge cases
- Fido2NetLib handles all WebAuthn cryptography — our code is wiring and persistence
- The frontend uses no additional WebAuthn libraries; `navigator.credentials` API is called directly
- All endpoints follow existing auth rate limiting policy (Requirement 9.7)
- Cascade delete on `user_id` FK handles account deletion cleanup (Requirement 12.1)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["3.2", "3.3"] },
    { "id": 4, "tasks": ["3.4", "3.5"] },
    { "id": 5, "tasks": ["3.6", "3.7"] },
    { "id": 6, "tasks": ["3.8", "5.1", "5.2"] },
    { "id": 7, "tasks": ["7.1"] },
    { "id": 8, "tasks": ["7.2", "7.3", "7.4"] }
  ]
}
```
