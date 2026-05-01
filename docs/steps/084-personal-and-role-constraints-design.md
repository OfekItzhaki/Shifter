# Step 084 — Personal and Role Constraints: Design Document

## Phase

Phase 9 — Spec-Driven Feature Development

## Purpose

Create the design document for the `personal-and-role-constraints` feature. This spec surfaces the `ScopeType.Person` and `ScopeType.Role` constraint capabilities that already exist in the backend domain but are not yet exposed in the UI.

## What was built

| File | Action | Description |
|---|---|---|
| `.kiro/specs/personal-and-role-constraints/design.md` | Created | Full design document covering architecture, components, data models, correctness properties, error handling, and testing strategy |
| `docs/steps/084-personal-and-role-constraints-design.md` | Created | This step documentation file |

## Key decisions

### No new API endpoints
The existing `GET /spaces/{spaceId}/constraints` already returns all scope types without filtering. The frontend partitions the flat list client-side by `scopeType`. All writes use the existing `POST`, `PUT`, and `DELETE` endpoints.

### Backend validation is already complete
`CreateConstraintCommandHandler` already contains the full validation chain (person existence check, registered-member guard, role existence + active check) from the previous spec. The design confirms this and specifies the exact HTTP status codes (400 / 404 / 422) the frontend must handle.

### `DomainValidationException` → HTTP 422
The registered-member guard throws `DomainValidationException`. This must map to HTTP 422 in `ExceptionHandlingMiddleware`. The design flags this as a required check.

### Role loading moved to constraints tab activation
`groupRoles` are currently loaded only when the settings tab opens. The `page.tsx` constraints `useEffect` must be extended to also load roles when `activeTab === "constraints"`, so the role selector is populated without requiring the user to visit settings first.

### Registered-member filter uses `invitationStatus` as proxy
`GroupMemberDto` does not expose `linkedUserId`. The filter `invitationStatus === "accepted"` is used as a proxy, which is correct because `Person.LinkUser()` sets both `linked_user_id` and `invitation_status = "accepted"` atomically.

### Delete confirmation before API call
The current `onDeleteConstraint` calls the API immediately. A Hebrew confirmation step must be added before the call, either via `window.confirm` or inline confirmation state in `ConstraintRow`.

### PBT is applicable
The feature has pure filtering and validation logic (person selector filtering, role selector filtering, name resolution, backend validation guards) with large input spaces. FsCheck (backend) and fast-check (frontend) are specified. 10 correctness properties are defined.

## How it connects

- **`ConstraintsTab`** — the existing component already has the three-section structure and `SectionCreateForm`. This design closes the remaining gaps (role loading, delete confirmation, edit form read-only fields, error isolation).
- **`CreateConstraintCommandHandler`** — already complete. Design confirms the validation chain and error contract.
- **`ExceptionHandlingMiddleware`** — must map `DomainValidationException` to 422 if not already done.
- **`page.tsx`** — two targeted changes: extend the constraints `useEffect` to load roles, and fix `handleCreateConstraint` to not filter out non-group constraints.

## How to run / verify

After implementation:

```bash
# Backend tests
dotnet test apps/api/

# Frontend tests
cd apps/web && npx jest --run --testPathPattern="constraints"

# Manual verification
# 1. Open a group's Constraints tab
# 2. Verify three sections: Group / Personal / Role
# 3. Create a personal constraint — person selector shows only accepted members
# 4. Create a role constraint — role selector shows only active roles
# 5. Edit a constraint — scope fields are read-only
# 6. Delete a constraint — confirmation dialog appears in Hebrew
# 7. Try creating a personal constraint for an unregistered person via API — expect HTTP 422
```

## What comes next

- `085-personal-and-role-constraints-tasks.md` — task list generation
- Implementation of the tasks defined in the spec

## Git commit

```bash
git add -A && git commit -m "feat(spec): personal-and-role-constraints design document"
```
