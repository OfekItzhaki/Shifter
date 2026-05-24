# 542 — Space Management API Client Functions

## Phase

Phase — Space Management Frontend

## Purpose

Adds TypeScript API client functions and type definitions for all space management endpoints. These functions provide the frontend with typed access to soft-delete/restore, ownership transfer, management timeout, home-leave config, invite code regeneration, role assignment, and permission level queries.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/spaces.ts` | Added `SpacePermissionLevel` enum, `SpacePermissionLevelDto` interface, `SpaceHomeLeaveMode` type, `SpaceHomeLeaveConfigDto` interface, `UpdateSpaceHomeLeaveConfigPayload` interface, and 9 new API functions: `softDeleteSpace`, `restoreSpace`, `transferOwnership`, `updateManagementTimeout`, `updateHomeLeaveConfig`, `getHomeLeaveConfig`, `regenerateSpaceInviteCode`, `assignSpaceRole`, `getSpacePermissionLevels` |

## Key decisions

1. **Added to existing `spaces.ts`** rather than creating a new file — keeps all space-related API functions co-located, consistent with the project pattern.
2. **Named `regenerateSpaceInviteCode`** (not overwriting existing `regenerateInviteCode`) — the existing function uses `/invite-code/regenerate` and is already imported elsewhere. The new function uses the alternative `/regenerate-invite-code` endpoint.
3. **`getHomeLeaveConfig` returns `null` on 404** — the backend returns 404 when no config exists yet, so the client gracefully handles this case.
4. **`SpacePermissionLevel` as a numeric enum** — matches the backend C# enum values (Member=0, Admin=1, GroupOwner=2, SpaceOwner=3) for direct serialization compatibility.
5. **`SpaceHomeLeaveMode` as string literal type** — matches the existing `HomeLeaveMode` pattern in `homeLeave.ts` where the backend serializes enum values as camelCase strings.

## How it connects

- These functions are consumed by the upcoming frontend components: `ManagementTimeoutCard`, `HomeLeaveConfigCard`, `DangerZoneCard`, `RoleAssignmentCard`, and the invite code section (tasks 14–18).
- The endpoint paths match the `SpacesController` routes defined in the backend (task 11).
- Types align with the backend DTOs: `SpaceHomeLeaveConfigDto` and `SpacePermissionLevelDto`.

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

TypeScript compilation should pass with zero errors.

## What comes next

- Task 14: `ManagementTimeoutCard` component (uses `updateManagementTimeout`)
- Task 15: `HomeLeaveConfigCard` component (uses `updateHomeLeaveConfig`, `getHomeLeaveConfig`)
- Task 16: `DangerZoneCard` component (uses `softDeleteSpace`, `transferOwnership`)
- Task 17: `RoleAssignmentCard` component (uses `assignSpaceRole`, `getSpacePermissionLevels`)
- Task 18: Invite code section (uses `regenerateSpaceInviteCode`)

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add space management API client functions and types"
```
