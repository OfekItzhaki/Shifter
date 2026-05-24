# 539 — Ownership Transfer Endpoint

## Phase

Space Management — API Layer

## Purpose

Aligns the `TransferOwnershipRequest` DTO with the spec-defined naming convention (`TargetUserId` instead of `NewOwnerUserId`), ensuring the API contract matches the design document and frontend expectations.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/SpacesController.cs` | Renamed `TransferOwnershipRequest.NewOwnerUserId` → `TargetUserId` and updated the endpoint to pass `req.TargetUserId` to the command |

The endpoint was already present from a prior implementation step. This step ensures the request DTO matches the spec:

```
POST /spaces/{spaceId}/transfer-ownership
Body: { "targetUserId": "guid", "reason": "optional string" }
Response: 204 No Content
```

## Key decisions

- Renamed the DTO property to `TargetUserId` to match the spec's `TransferOwnershipRequest(Guid TargetUserId, string? Reason)` definition.
- The command parameter remains `NewOwnerUserId` internally — only the public API contract changed.

## How it connects

- The endpoint dispatches `TransferOwnershipCommand` which was enhanced in task 6.1 with membership validation, permission grants, and audit logging.
- The controller-level `[Authorize]` attribute ensures authentication; permission checks happen inside the command handler via `IPermissionService`.

## How to run / verify

```bash
dotnet build apps/api/Jobuler.Api
# Verify the endpoint accepts { "targetUserId": "...", "reason": "..." } in the body
```

## What comes next

- Task 11.3: Settings endpoints (management timeout, home-leave config, invite code)
- Task 11.4: Role assignment endpoint

## Git commit

```bash
git add -A && git commit -m "feat(space-management): rename transfer ownership DTO to TargetUserId"
```
