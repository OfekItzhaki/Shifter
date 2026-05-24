# Step 526 — Space Entity Soft-Delete and Management Timeout

## Phase

Space Management — Domain Layer

## Purpose

Adds soft-delete lifecycle support and a configurable management timeout to the Space entity. Soft-delete allows space owners to archive a space (and later restore it) without permanently removing data. The management timeout defines how long an admin session stays active before requiring re-authentication, applied uniformly across all groups in the space.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Spaces/Space.cs` | Added `DeletedAt` (DateTime?) and `ManagementTimeoutMinutes` (int, default 15) properties; added `SoftDelete()`, `Restore()`, and `SetManagementTimeout(int)` methods |
| `apps/api/Jobuler.Tests/Domain/SpaceTests.cs` | Added unit tests for soft-delete, restore, management timeout default, valid/boundary/invalid timeout values |

## Key decisions

- `DeletedAt` uses a nullable `DateTime?` pattern consistent with the existing `Group.DeletedAt` field — null means active, non-null means soft-deleted.
- `ManagementTimeoutMinutes` defaults to 15 minutes, matching the existing group-level default, ensuring backward compatibility when migrating settings to the space level.
- Validation range [5, 120] is enforced directly in the domain entity via `InvalidOperationException`, keeping business rules in the domain layer per Clean Architecture.
- Both `SoftDelete()` and `Restore()` call `Touch()` to update `UpdatedAt`, maintaining audit trail consistency.

## How it connects

- `SoftDelete()` and `Restore()` are called by `SoftDeleteSpaceCommand` (task 5.1) and `RestoreSpaceCommand` (task 5.2)
- `SetManagementTimeout()` is called by `UpdateManagementTimeoutCommand` (task 7.1)
- `DeletedAt` column will be mapped in EF configuration (task 2.2)
- `ManagementTimeoutMinutes` column will be mapped in EF configuration (task 2.2)
- Listing queries will filter on `DeletedAt == null` (task 11.5)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain/Jobuler.Domain.csproj
dotnet test Jobuler.Tests --filter "FullyQualifiedName~Jobuler.Tests.Domain.SpaceTests"
```

All 16 tests pass (4 existing + 12 new).

## What comes next

- Task 1.2: Enhance Group entity with cascade soft-delete tracking (`DeletedBySpaceDeletion`)
- Task 2.2: EF Core configuration for the new Space columns
- Task 5.1/5.2: Application layer commands that use these domain methods

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add soft-delete and management timeout to Space entity"
```
