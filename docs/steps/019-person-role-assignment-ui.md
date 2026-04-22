# Step 019 — Person Role Assignment UI

## Phase
Post-MVP Completion

## Purpose
The API for assigning roles to people already existed (`PersonRoleAssignment` domain entity, `PersonRoleAssignments` DbSet, `SpaceRoles` table) but there was no way to assign or remove roles from the frontend. The person detail page only displayed role names as read-only badges. This step adds full assign/remove capability.

## What was built

### Backend

| File | Description |
|---|---|
| `Application/People/Commands/AssignRoleCommand.cs` | `AssignRoleToPersonCommand` and `RemoveRoleFromPersonCommand` handlers. Both verify person and role belong to the same space before acting. Assign is idempotent (no-op if already assigned). Remove is idempotent (no-op if not assigned). |
| `Application/People/Queries/GetPeopleQuery.cs` | Added `PersonRoleDto(Guid RoleId, string Name)` record. Updated `PersonDetailDto` to include `List<PersonRoleDto> Roles` alongside the existing `List<string> RoleNames`. Updated `GetPersonDetailQueryHandler` to populate both fields with a single join query, filtered by `space_id`. |
| `Api/Controllers/PeopleController.cs` | Added `POST /spaces/{spaceId}/people/{personId}/roles` (assign) and `DELETE /spaces/{spaceId}/people/{personId}/roles/{roleId}` (remove). Both require `PeopleManage` permission. Added `AssignRoleRequest(Guid RoleId)` record. |

### Frontend

| File | Description |
|---|---|
| `lib/api/people.ts` | Added `RoleDto`, `PersonRoleDto` interfaces. Added `getSpaceRoles()`, `assignRole()`, `removeRole()` API client functions. Extended `PersonDetailDto` with `roles: PersonRoleDto[]`. |
| `app/admin/people/[personId]/page.tsx` | Roles section now shows assigned roles as removable badges (× button). A dropdown of unassigned roles + Assign button lets admins add roles. Both actions reload the person detail. Available roles list is derived by filtering out already-assigned role IDs. |

## Key decisions

### Idempotent commands
Both assign and remove are no-ops if the state is already correct. This avoids 409 conflicts on double-clicks and makes the frontend simpler.

### Roles field alongside RoleNames
`PersonDetailDto` keeps the existing `RoleNames: List<string>` for backward compatibility (solver payload normalizer and other consumers use it) and adds `Roles: List<PersonRoleDto>` with IDs for the frontend to use for remove operations. Both are populated from the same join query.

### Available roles derived on the frontend
The frontend fetches all space roles and filters out already-assigned ones client-side. This avoids a dedicated "available roles" endpoint and keeps the API surface minimal.

### Space isolation
`AssignRoleToPersonCommand` verifies both `person.SpaceId == req.SpaceId` and `role.SpaceId == req.SpaceId` before creating the assignment, preventing cross-space role assignment even if a client sends a valid but foreign ID.

## How it connects
- `PersonRoleAssignment` domain entity and DB table were already in place from step 003/005
- `GetPersonDetailQueryHandler` already joined role names — extended to also return IDs
- `SolverPayloadNormalizer` uses `PersonRoleAssignments` directly and is unaffected
- The roles dropdown on the person detail page calls `GET /spaces/{id}/roles` (existing `RolesController`)

## How to run / verify

1. Start the stack: `docker compose -f infra/compose/docker-compose.yml up -d`
2. Login as admin, navigate to Admin → People → click any person
3. The Roles section shows current roles as blue badges with an × button
4. Select a role from the dropdown and click Assign — badge appears
5. Click × on a badge — it disappears
6. Via Swagger: `POST /spaces/{id}/people/{personId}/roles` with `{"roleId": "..."}` → 204
7. Via Swagger: `DELETE /spaces/{id}/people/{personId}/roles/{roleId}` → 204

## What comes next
- Availability windows UI on the person detail page (API exists, no frontend yet)
- Notification system when solver completes
- PDF export

## Git commit

```bash
git add -A && git commit -m "feat(people): person role assignment UI and API"
```
