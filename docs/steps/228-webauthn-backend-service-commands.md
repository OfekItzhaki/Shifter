# 228 — WebAuthn Backend Service & Commands

## Phase
Phase 2 — Backend Service and Commands (Biometric Login)

## Purpose
Implements the application-layer service interface and MediatR commands for WebAuthn credential registration, authentication, and management. This connects the domain entity (from Phase 1) to the Fido2NetLib cryptographic library and exposes the operations that the API controller will dispatch to.

## What was built

### Application Layer (`Jobuler.Application/Auth/`)

| File | Description |
|------|-------------|
| `IWebAuthnService.cs` | Interface defining 4 ceremony operations + result DTOs |
| `Commands/WebAuthnRegisterOptionsCommand.cs` | Initiates registration — loads user + existing credentials, generates options |
| `Commands/WebAuthnRegisterCompleteCommand.cs` | Completes registration — verifies attestation, persists credential entity |
| `Commands/WebAuthnLoginOptionsCommand.cs` | Initiates authentication (anonymous) — generates assertion options |
| `Commands/WebAuthnLoginCompleteCommand.cs` | Completes authentication — verifies assertion, issues JWT + refresh token |
| `Commands/DeleteWebAuthnCredentialCommand.cs` | Deletes a credential with ownership validation |
| `Commands/UpdateWebAuthnCredentialNicknameCommand.cs` | Updates nickname with ownership + length validation |
| `Queries/ListWebAuthnCredentialsQuery.cs` | Lists all credentials for the authenticated user |

### Infrastructure Layer (`Jobuler.Infrastructure/Auth/`)

| File | Description |
|------|-------------|
| `Fido2Service.cs` | Implements `IWebAuthnService` using Fido2NetLib v3.0.1 + IMemoryCache |

### DI Registration (`Jobuler.Api/Program.cs`)

- Registered `IMemoryCache` via `AddMemoryCache()`
- Registered `IFido2` singleton with configuration from `appsettings.json`
- Registered `IWebAuthnService` → `Fido2Service` as scoped service

## Key decisions

1. **Challenge storage**: Full options JSON is stored in `IMemoryCache` (not just the challenge bytes). This allows exact reconstruction of `CredentialCreateOptions`/`AssertionOptions` for verification without re-querying configuration.

2. **Single-use challenges**: Cache entry is removed immediately on retrieval, before verification. Even if verification fails, the challenge cannot be reused.

3. **Token issuance parity**: `WebAuthnLoginCompleteCommand` reuses the exact same `IJwtService` and `RefreshToken.Create` pattern as `LoginCommandHandler`, ensuring identical token format and expiry.

4. **Ownership enforcement**: Delete and update commands check `credential.UserId != request.UserId` and throw `UnauthorizedAccessException` (mapped to 403 by middleware).

5. **Credential ID extraction**: The login complete handler extracts the credential ID from the assertion JSON to look up the stored credential before calling the verification service.

6. **Fido2NetLib v3.0.1 API**: Uses the individual-parameter overloads (not the newer parameter-object API from master). `AuthenticatorSelection` uses `RequireResidentKey` (bool) rather than `ResidentKey` (enum).

## How it connects

- **Depends on**: Phase 1 (WebAuthnCredential entity, EF configuration, Fido2NetLib package)
- **Used by**: Phase 3 (WebAuthnController dispatches these commands via MediatR)
- **Reuses**: `IJwtService`, `RefreshToken`, `AppDbContext`, `LoginResult` record

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build should succeed with 0 errors. Commands are wired via MediatR assembly scanning (already configured for the Application assembly).

## What comes next

- Phase 3: `WebAuthnController` with 7 endpoints dispatching to these commands
- Phase 3: DI wiring verification (already done here)
- Property-based tests for challenge generation, registration options, token parity, and credential management

## Git commit

```bash
git add -A && git commit -m "feat(biometric): add WebAuthn service interface, Fido2Service, and MediatR commands"
```
