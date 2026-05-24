# Step 532 — Transfer Ownership Command Enhancement

## Phase

Space Management — Application Layer

## Purpose

Enhances the existing `TransferOwnershipCommand` handler to enforce proper authorization via `IPermissionService`, validate the target user is an active space member (not the current owner), grant all permission keys to the new owner, and produce an audit log entry for the ownership transfer.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Spaces/Commands/TransferOwnershipCommand.cs` | Replaced manual owner check with `IPermissionService.RequirePermissionAsync`; added membership validation; added self-transfer guard; added permission grants for new owner; added `IAuditLogger` call with before/after snapshot |

## Key decisions

1. **Permission check via `IPermissionService`** — Uses `Permissions.OwnershipTransfer` key rather than a manual `OwnerUserId` comparison. This delegates hierarchy enforcement to the centralized permission service (which already grants all permissions to the space owner implicitly).
2. **Self-transfer check before membership check** — Checking self-transfer first avoids a redundant DB query when the target is the current owner.
3. **Idempotent permission grants** — Existing active grants are loaded first; only missing permission keys are added. This prevents duplicate rows if the new owner already had some permissions.
4. **Audit log after SaveChanges** — The audit log is written after the main transaction succeeds, ensuring we don't log transfers that failed to persist.

## How it connects

- Depends on `IPermissionService` (enhanced in step 529 with four-tier hierarchy)
- Depends on `IAuditLogger` (existing infrastructure service)
- Depends on `SpaceMembership`, `SpacePermissionGrant`, `OwnershipTransferHistory` domain entities
- Will be called from `SpacesController` endpoint `POST /spaces/{spaceId}/transfer-ownership` (task 11.2)
- Property tests for this handler are defined in task 6.2

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --no-build --filter "TransferOwnership"
```

## What comes next

- Task 6.2: Property tests for ownership transfer (Properties 5, 6, 7)
- Task 11.2: API endpoint wiring in SpacesController

## Git commit

```bash
git add -A && git commit -m "feat(space-management): enhance TransferOwnershipCommand with permission checks, membership validation, and audit logging"
```
