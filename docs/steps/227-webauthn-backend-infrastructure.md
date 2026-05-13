# Step 227 — WebAuthn Backend Infrastructure

## Phase
Biometric Login — Phase 1 (Backend Infrastructure)

## Purpose
Set up the foundational backend pieces for WebAuthn/passkey biometric login: the Fido2NetLib NuGet package, configuration, the domain entity for storing credentials, the EF Core persistence mapping, and the database migration.

## What was built

| File | Description |
|---|---|
| `apps/api/Jobuler.Infrastructure/Jobuler.Infrastructure.csproj` | Added `Fido2.AspNet` package reference (includes Fido2NetLib) |
| `apps/api/Jobuler.Api/appsettings.json` | Added `WebAuthn` config section (RP ID, RP name, origin, challenge timeout) |
| `apps/api/Jobuler.Api/appsettings.Development.json` | Added `WebAuthn` config section with localhost values |
| `apps/api/Jobuler.Domain/Identity/WebAuthnCredential.cs` | Domain entity with factory method, sign count validation, nickname validation, disable logic |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/WebAuthnCredentialConfiguration.cs` | EF Core configuration mapping to `webauthn_credentials` table |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Added `DbSet<WebAuthnCredential>` |
| `infra/migrations/048_webauthn_credentials.sql` | SQL migration creating the table with indexes and cascade FK |

## Key decisions

- **Entity inherits from `Entity` (not `AuditableEntity`)** — WebAuthn credentials don't need `UpdatedAt` tracking; they have their own `LastUsedAt` field updated on authentication.
- **Sign count regression disables the credential before throwing** — per the spec, a regression indicates a cloned credential. The entity sets `IsDisabled = true` then throws `InvalidOperationException`, ensuring the credential is flagged even if the caller catches the exception.
- **Nickname validation in both `Create` and `UpdateNickname`** — prevents invalid state at creation time and during updates.
- **`Fido2.AspNet` version `3.*`** — uses the latest 3.x release which includes the ASP.NET Core integration and Fido2NetLib core.
- **No RLS on `webauthn_credentials`** — this table is not tenant-scoped (it's user-scoped via `user_id` FK to `users`). Access control is enforced at the application layer by checking the authenticated user's ID.

## How it connects
- The `WebAuthnCredential` entity will be used by the `IWebAuthnService` implementation (Phase 2) to persist and retrieve credentials.
- The EF Core configuration is auto-discovered via `ApplyConfigurationsFromAssembly` in `AppDbContext.OnModelCreating`.
- The `appsettings.json` WebAuthn section will be read by the `Fido2Service` to configure the relying party.
- The cascade delete on `user_id` ensures account deletion removes all credentials (Requirement 12.1).

## How to run / verify

```bash
# Build the solution
cd apps/api && dotnet build

# Run the migration against the database
psql -U jobuler -d jobuler -f infra/migrations/048_webauthn_credentials.sql

# Verify table exists
psql -U jobuler -d jobuler -c "\d webauthn_credentials"
```

## What comes next
- Task 1.3: Property tests for the `WebAuthnCredential` entity (sign count monotonic invariant, nickname length validation)
- Phase 2: `IWebAuthnService` interface, `Fido2Service` implementation, MediatR commands for registration and authentication

## Git commit

```bash
git add -A && git commit -m "feat(biometric-login): add WebAuthn infrastructure — Fido2 package, domain entity, EF config, migration"
```
