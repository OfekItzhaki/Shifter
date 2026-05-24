# 537 — Audit Logging for Space Management Actions

## Phase

Phase 6 — Space Management (Infrastructure — Audit Integration)

## Purpose

Ensures all space management actions (soft-delete, restore, ownership transfer, role assign/revoke) produce complete audit log entries with all required fields: actor_user_id, space_id, action, entity_type, entity_id, before/after snapshot, and timestamp. This satisfies the security rules requiring append-only audit trail entries for destructive and privileged operations.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Spaces/Commands/SoftDeleteSpaceCommand.cs` | Enhanced audit log call to include `beforeJson` (previous DeletedAt state) and `afterJson` (new DeletedAt timestamp + cascade-deleted group count) |
| `apps/api/Jobuler.Application/Spaces/Commands/RestoreSpaceCommand.cs` | Enhanced audit log call to include `beforeJson` (captured DeletedAt before restore) and `afterJson` (null DeletedAt + restored group count); added before-state capture variable |

## Key decisions

- **Before/after snapshots added to soft-delete and restore**: The `TransferOwnershipCommand` and `AssignSpaceRoleCommand` already included before/after JSON snapshots. The soft-delete and restore handlers were missing these, so they were enhanced to include meaningful state transitions.
- **Capture before-state before mutation**: In `RestoreSpaceCommand`, the `DeletedAt` value is captured into a local variable before calling `space.Restore()`, ensuring the audit log accurately reflects the pre-operation state.
- **No interface changes needed**: The existing `IAuditLogger` interface already supports all required fields (spaceId, actorUserId, action, entityType, entityId, beforeJson, afterJson, ipAddress). The `AuditLog` entity inherits `CreatedAt` from `Entity` base class, providing the timestamp. No schema or interface modifications were necessary.
- **Role revoke covered by AssignSpaceRoleCommand**: Setting a user's permission level to `Member` effectively revokes elevated roles, and this is already audited with before/after snapshots showing the permission level change.

## How it connects

- Uses `IAuditLogger` interface (defined in `Application/Common/IAuditLogger.cs`, implemented in `Infrastructure/Logging/AuditLogger.cs`)
- Already registered in DI (`Program.cs`: `builder.Services.AddScoped<IAuditLogger, AuditLogger>()`)
- Satisfies Requirements 1.5, 2.5, 3.7 from the space-management spec
- All audit entries are append-only (never updated or deleted) per security rules
- Audit log entries include: actor_user_id, space_id, action, entity_type, entity_id, before/after snapshot, timestamp (via CreatedAt)

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --no-build
```

Verify audit entries are produced by checking that each handler calls `_audit.LogAsync` with all required parameters including `entityType`, `entityId`, `beforeJson`, and `afterJson`.

## What comes next

- Task 10.3: Property tests for audit logging completeness (Property 12) and home-leave config propagation (Property 13)
- Task 11.x: API layer endpoints that dispatch these commands

## Git commit

```bash
git add -A && git commit -m "feat(space-management): enhance audit logging with before/after snapshots for soft-delete and restore"
```
