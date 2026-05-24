# 536 — Get Space Permission Levels Query

## Phase

Space Management — Application Layer Queries

## Purpose

Provides a query to retrieve all active space members with their assigned permission levels. This supports the frontend `RoleAssignmentCard` component and the `GET /spaces/{spaceId}/members/roles` API endpoint, enabling the Space Owner to view and manage permission levels for all members.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Spaces/Queries/GetSpacePermissionLevelsQuery.cs` | Query record, DTO, and handler that loads active SpaceMembership records and returns UserId + PermissionLevel pairs |

## Key decisions

- Followed the same pattern as `GetSpaceHomeLeaveConfigQuery` and `GetSpaceMembersQuery` — simple MediatR query with `AppDbContext` injection.
- Used `AsNoTracking()` for read-only performance.
- Filtered to only active memberships (`IsActive == true`) to exclude deactivated members.
- Projected directly to DTO in the query to avoid loading full entity graphs.
- Did not include user display names in this DTO — the frontend can cross-reference with the existing `GetSpaceMembersQuery` if needed, keeping this query focused on permission data.

## How it connects

- Used by `SpacesController` endpoint `GET /spaces/{spaceId}/members/roles` (task 11.4)
- Consumed by the frontend `RoleAssignmentCard` component (task 17.1)
- Relies on `SpaceMembership.PermissionLevel` field added in task 1.5

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application
```

## What comes next

- Task 9 checkpoint (application layer complete)
- Task 10: Solver payload and audit integration
- Task 11.4: Wire this query to the API endpoint

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add GetSpacePermissionLevelsQuery and handler"
```
