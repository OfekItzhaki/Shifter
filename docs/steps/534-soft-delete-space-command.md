# 534 — Soft-Delete Space Command

## Phase

Phase: Space Management — Application Layer

## Purpose

Implements the `SoftDeleteSpaceCommand` and its handler, enabling the Space Owner to soft-delete a space and cascade the deletion to all groups within it. This is the mirror operation of `RestoreSpaceCommand` (step 532).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Spaces/Commands/SoftDeleteSpaceCommand.cs` | Command record and MediatR handler for soft-deleting a space |

## Key decisions

- **Permission key: `Permissions.OwnershipTransfer`** — matches the pattern used by `RestoreSpaceCommand` and other owner-only operations. Only the Space Owner can soft-delete a space.
- **Cascade via `SoftDeleteBySpace()`** — calls `SoftDeleteBySpace()` on each group, which internally skips groups that are already individually deleted (preserving the `DeletedBySpaceDeletion` tracking flag for selective restore).
- **Audit after save** — audit log is written after `SaveChangesAsync` to ensure we only log successful operations.
- **Single-file command + handler** — follows the established co-location pattern (e.g., `RestoreSpaceCommand.cs`).

## How it connects

- Uses `Space.SoftDelete()` domain method (step 526)
- Uses `Group.SoftDeleteBySpace()` domain method (step 526/527)
- Mirror of `RestoreSpaceCommand` (step 532) — together they form the soft-delete/restore round trip
- Will be wired to `DELETE /spaces/{spaceId}` endpoint in task 11.1
- Audit logging via `IAuditLogger` with action `space.soft_delete`

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no errors.

## What comes next

- Task 5.2: `RestoreSpaceCommand` (already implemented)
- Task 5.3: Property tests for soft-delete/restore (Properties 1, 2, 3)
- Task 11.1: API endpoint wiring (`DELETE /spaces/{spaceId}`)

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add SoftDeleteSpaceCommand and handler"
```
