# Step 027 — Group Detail Page: API Layer

## Phase
Phase 8 — Group Detail Page

## Purpose
Extend the frontend API client with the DTOs and functions needed to support the new Group Detail Page feature. This provides typed access to group listing, member management, and solver settings endpoints.

## What was built

- `apps/web/lib/api/groups.ts` — appended the following:
  - `GroupWithMemberCountDto` interface: group summary including member count and solver horizon
  - `GroupMemberDto` interface: member info with fallback display name support
  - `getGroups(spaceId)` — `GET /spaces/{spaceId}/groups`
  - `getGroupMembers(spaceId, groupId)` — `GET /spaces/{spaceId}/groups/{groupId}/members`
  - `addGroupMemberByEmail(spaceId, groupId, email)` — `POST /spaces/{spaceId}/groups/{groupId}/members/by-email`
  - `removeGroupMember(spaceId, groupId, personId)` — `DELETE /spaces/{spaceId}/groups/{groupId}/members/{personId}`
  - `updateGroupSettings(spaceId, groupId, solverHorizonDays)` — `PATCH /spaces/{spaceId}/groups/{groupId}/settings`

## Key decisions
- All functions use the existing `apiClient` (axios instance with auth interceptors) — no new HTTP setup needed.
- Existing interfaces (`GroupTypeDto`, `GroupDto`) and functions (`getSpaceRoles`, `createSpaceRole`) were preserved unchanged.
- `displayName` on `GroupMemberDto` is typed as `string | null` to match the fallback logic required by Requirement 2.4.

## How it connects
- The Group Detail Page component (next task) will import these functions to fetch and mutate group data.
- `spaceId` is sourced from `SpaceStore.currentSpaceId` in the UI layer.
- All write operations (`addGroupMemberByEmail`, `removeGroupMember`, `updateGroupSettings`) are gated server-side by `IPermissionService`.

## How to run / verify
```bash
# TypeScript compile check
cd apps/web && npx tsc --noEmit
```

## What comes next
- Task 2: Create the `GroupDetailPage` component at `app/groups/[groupId]/page.tsx` consuming these API functions.

## Git commit
```bash
git add -A && git commit -m "feat(group-detail): extend groups API client with DTOs and member/settings functions"
```
