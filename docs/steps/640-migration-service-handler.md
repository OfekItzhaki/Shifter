# 640 — Migration Service Handler (Task 8)

## Phase
Space-First Onboarding — API Layer

## Purpose
Implements the migration service that moves existing users (who have groups but no space membership) into a newly created space. This ensures backward compatibility for users who existed before the space-first onboarding flow was introduced.

## What was built

### Modified files
- `apps/api/Jobuler.Domain/Groups/Group.cs` — Added `ReassignToSpace(Guid spaceId)` method to allow migration to update a group's SpaceId.
- `apps/api/Jobuler.Application/Spaces/Commands/MigrateUserSpaceCommand.cs` — Rewrote the handler to:
  - Throw `InvalidOperationException` if a migration record already exists (maps to 400 via middleware)
  - Find groups via group memberships (Person → GroupMembership → Group) instead of `CreatedByUserId`
  - Actually reassign groups to the new space via `ReassignToSpace()`
  - Wrap all operations in a database transaction with rollback on failure
  - Log both success and failure for operational review

### Already existed (verified)
- `POST /spaces/migrate` endpoint in `SpacesController.cs`
- `MigrateUserSpaceCommand` record definition
- `UserSpaceMigration` domain entity
- `AppDbContext.UserSpaceMigrations` DbSet

## Key decisions
- Used `InvalidOperationException` for "already migrated" instead of returning a result DTO — aligns with the architecture rule that `InvalidOperationException` → 400 via `ExceptionHandlingMiddleware`.
- Group discovery uses the Person → GroupMembership chain (LinkedUserId → PersonId → GroupId) rather than `CreatedByUserId`, matching the task spec's requirement to find groups "via group memberships".
- Added `ReassignToSpace()` to the Group entity rather than using raw SQL or reflection, keeping domain logic encapsulated.
- Transaction uses `CreateExecutionStrategy()` + `BeginTransactionAsync` pattern consistent with other handlers in the codebase (e.g., `PublishSandboxCommand`).

## How it connects
- The `POST /spaces/migrate` endpoint is called by the frontend redirect logic (Task 13) when an existing user has groups but no space membership.
- The handler creates a Space, SpaceMembership, and permission grants — same entities used by the onboarding wizard (Task 10).
- The `UserSpaceMigration` record prevents duplicate migrations on subsequent logins (Requirement 9.4).

## How to run / verify
```bash
cd apps/api
dotnet build Jobuler.Api/Jobuler.Api.csproj
```
All four projects (Domain, Application, Infrastructure, Api) compile successfully.

## What comes next
- Task 9: Frontend API client extensions (including `migrateUserSpace()`)
- Task 13: Frontend redirect logic that triggers migration for existing users

## Git commit
```bash
git add -A && git commit -m "feat(spaces): implement migration service handler with transaction and logging"
```
