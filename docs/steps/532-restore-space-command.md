# Step 532 — RestoreSpaceCommand and Handler

## Phase

Phase: Space Management — Application Layer (Soft-delete/Restore)

## Purpose

Implements the `RestoreSpaceCommand` and its handler, which allows a Space Owner to restore a previously soft-deleted space. This reverses the soft-delete operation by clearing the `DeletedAt` timestamp on the space and restoring all groups that were cascade-deleted as part of the space deletion (groups individually deleted before the space deletion remain deleted).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Spaces/Commands/RestoreSpaceCommand.cs` | Command record (`RestoreSpaceCommand`) and handler (`RestoreSpaceCommandHandler`) that verifies owner permission, validates the space is in a deleted state, restores the space, restores cascade-deleted groups, persists changes, and logs the action via `IAuditLogger`. |

## Key decisions

- **Permission key**: Uses `Permissions.OwnershipTransfer` — the same owner-only permission key used for other destructive/owner-only space actions.
- **Guard clause**: Throws `InvalidOperationException("Space is not in a deleted state.")` if `DeletedAt` is null, matching the error handling table in the design document.
- **Group restoration**: Calls `RestoreFromSpaceDeletion()` on all groups for the space. This method internally checks `DeletedBySpaceDeletion` and only restores groups that were cascade-deleted (not individually deleted).
- **No global query filter**: Since there's no EF global query filter on `DeletedAt`, the space can be loaded directly without `IgnoreQueryFilters()`.
- **Audit after save**: Audit log is written after `SaveChangesAsync` to ensure we only log successful operations.

## How it connects

- **Domain (task 1.1)**: Calls `Space.Restore()` which sets `DeletedAt = null` and calls `Touch()`.
- **Domain (task 1.2)**: Calls `Group.RestoreFromSpaceDeletion()` which conditionally clears `DeletedAt` and `DeletedBySpaceDeletion`.
- **Infrastructure (task 3.1)**: Permission check via `IPermissionService.RequirePermissionAsync` enforces the four-tier hierarchy.
- **API (task 11.1)**: Will be dispatched by `POST /spaces/{spaceId}/restore` endpoint.
- **Counterpart (task 5.1)**: `SoftDeleteSpaceCommand` performs the inverse operation.

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no errors related to this file.

## What comes next

- Task 5.1: `SoftDeleteSpaceCommand` (the inverse operation, also in-progress)
- Task 5.3: Property tests for soft-delete/restore round trip (Properties 1, 2, 3)
- Task 11.1: API endpoint `POST /spaces/{spaceId}/restore` that dispatches this command

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add RestoreSpaceCommand and handler"
```
