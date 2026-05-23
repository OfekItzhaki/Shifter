# 504 — ReAuthAttempt EF Configuration & Migration

## Phase

Admin Re-Auth Security — Backend Infrastructure

## Purpose

Creates the EF Core configuration and database migration for the `reauth_attempts` table, enabling persistent storage of re-authentication attempts for lockout tracking and audit purposes.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Infrastructure/Persistence/Configurations/ReAuthAttemptConfiguration.cs` | EF Core `IEntityTypeConfiguration<ReAuthAttempt>` mapping entity to `reauth_attempts` table with snake_case columns and composite index |
| `Jobuler.Application/Persistence/AppDbContext.cs` | Added `DbSet<ReAuthAttempt> ReAuthAttempts` registration |
| `Jobuler.Application/Persistence/Migrations/20260523230727_AddReAuthAttempts.cs` | Migration creating the table and index |
| `Jobuler.Application/Persistence/Migrations/20260523230727_AddReAuthAttempts.Designer.cs` | Migration designer file |
| `Jobuler.Application/Persistence/Migrations/AppDbContextModelSnapshot.cs` | Updated model snapshot |

## Key decisions

- **No FK to users table** — The `ReAuthAttempt` entity is append-only and doesn't need navigation properties. Keeping it decoupled avoids cascade-delete complexity and keeps the audit trail intact even if a user is deleted.
- **Composite descending index** — `IX_reauth_attempts_user_id_attempted_at` with `attempted_at DESC` optimizes the lockout query pattern: "count failures in last 15 minutes for user X" scans the most recent rows first.
- **varchar(20) for method** — Constrains the method column to known values ("password", "webauthn") without using an enum type, keeping the schema simple.
- **CreatedAt mapped to snake_case** — Follows the project convention of mapping the base `Entity.CreatedAt` property to `created_at`.

## How it connects

- **Depends on**: Step 503 (ReAuthAttempt domain entity)
- **Used by**: Task 1.3 (ReAuthenticateCommandHandler lockout logic) will query this table to check failure counts and insert new attempt records
- **Pattern**: Follows the same configuration pattern as `WebAuthnCredentialConfiguration`, `RefreshTokenConfiguration`, etc.

## How to run / verify

```bash
cd apps/api
dotnet build                    # Should succeed with no new errors
dotnet ef migrations list --project Jobuler.Application --startup-project Jobuler.Api --context AppDbContext
# Should show AddReAuthAttempts as the latest migration

# To apply (requires running PostgreSQL):
dotnet ef database update --project Jobuler.Application --startup-project Jobuler.Api --context AppDbContext
```

## What comes next

- Task 1.3: Enhance `ReAuthenticateCommandHandler` with lockout logic that queries `reauth_attempts`
- Task 1.4: Update `AuthController.ReAuthenticate` to return 429 lockout responses

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): add ReAuthAttempt EF configuration and migration"
```
