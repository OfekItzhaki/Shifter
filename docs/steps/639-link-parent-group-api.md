# 639 — Link Parent Group API

## Phase
Space-First Onboarding — Task 7

## Purpose
Implements the API layer for linking and unlinking parent-child group relationships within a space. This enables single-level group hierarchy where a parent group's schedule can cascade constraints to child groups.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Spaces/Commands/LinkParentGroupCommand.cs` | Command + handler: validates same space, single-level hierarchy, no circular refs, sets parent |
| `apps/api/Jobuler.Application/Spaces/Commands/UnlinkParentGroupCommand.cs` | Command + handler: verifies permission, sets parent_group_id to null |
| `apps/api/Jobuler.Application/Spaces/Validators/LinkParentGroupCommandValidator.cs` | FluentValidation validator for LinkParentGroupCommand |
| `apps/api/Jobuler.Api/Controllers/GroupsController.cs` | Added `POST /spaces/{spaceId}/groups/{groupId}/link-parent` and `DELETE /spaces/{spaceId}/groups/{groupId}/link-parent` endpoints |

## Key decisions

- **Permission**: Uses `space.admin_mode` permission for both link and unlink operations (consistent with design doc stating "Space admin" access).
- **Single-level hierarchy enforcement**: The handler checks two conditions: (1) the proposed parent cannot itself have a parent, and (2) the proposed child cannot already be a parent of other groups.
- **Same-space validation**: Both groups are loaded with a `SpaceId` filter in the query, so a group from another space simply won't be found (404). An explicit check is also included for clarity.
- **Commands placed in `Spaces/Commands/`**: Following the design doc's file structure specification.
- **Endpoints on GroupsController**: The routes are `spaces/{spaceId}/groups/{groupId}/link-parent` which naturally fits the GroupsController's routing pattern.

## How it connects

- The `Group` entity already has `ParentGroupId`, `SetParentGroup(Guid?)`, and `UnlinkFromParent()` methods (Task 3).
- The frontend API client (`apps/web/lib/api/spaces.ts`) already has `linkParentGroup` and `unlinkParentGroup` functions that call these endpoints (Task 9 partial).
- Task 16 (Solver Integration) will use the parent-child relationship to cascade schedule constraints.

## How to run / verify

```bash
cd apps/api
dotnet build   # Should succeed with no errors
dotnet test    # Existing tests should pass
```

Manual API test:
```
POST /spaces/{spaceId}/groups/{childGroupId}/link-parent
Body: { "parentGroupId": "<guid>" }
Authorization: Bearer <token>

DELETE /spaces/{spaceId}/groups/{childGroupId}/link-parent
Authorization: Bearer <token>
```

## What comes next

- Task 8: Migration Service
- Task 14: Frontend — Linked Group UI (uses these endpoints)
- Task 16: Solver Integration — Parent Schedule Cascading

## Git commit

```bash
git add -A && git commit -m "feat(space-onboarding): link parent group commands, validator, and API endpoints"
```
