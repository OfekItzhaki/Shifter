# Step 085 — Personal and Role Constraints: UI and Backend Guards

## Phase

Phase 8 — Constraint System Completion

## Purpose

This step surfaces the personal (`ScopeType.Person`) and role-based (`ScopeType.Role`) constraint capabilities that already existed in the backend domain but were not yet exposed in the UI. It also fixes a data-loss bug in `page.tsx` that silently stripped personal and role constraints from the frontend state after every create operation.

The backend validation chain (`CreateConstraintCommandHandler`) was already complete from the previous spec. This step closes the remaining gaps:

1. A filter bug that discarded non-group constraints from state.
2. Missing role loading when the constraints tab activates.
3. No delete confirmation dialog (constraints were deleted on first click).
4. The edit modal showed no scope context for personal/role constraints.
5. Per-section create errors were shared across all three sections.
6. API error messages were swallowed instead of surfaced to the user.

## What Was Built

### Spec artifacts

| File | Description |
|---|---|
| `.kiro/specs/personal-and-role-constraints/tasks.md` | Implementation task list with 10 top-level tasks and property test sub-tasks |

### Frontend changes

| File | Change |
|---|---|
| `apps/web/app/groups/[groupId]/page.tsx` | **Bug fix**: `handleCreateConstraint` now calls `setConstraints(updated)` instead of `setConstraints(updated.filter(c => c.scopeId === groupId))` — personal and role constraints are no longer stripped |
| `apps/web/app/groups/[groupId]/page.tsx` | **Role loading**: constraints `useEffect` extended to `Promise.all([getConstraints, getGroupRoles])` so roles are available when the tab first opens |
| `apps/web/app/groups/[groupId]/page.tsx` | **Error propagation**: `onCreateWithScope` now extracts `error.response?.data?.error` and re-throws with the server message so `SectionCreateForm` can display it |
| `apps/web/app/groups/[groupId]/tabs/ConstraintsTab.tsx` | **Delete confirmation**: `ConstraintRow` now has a two-step inline confirmation ("האם למחוק?") before calling `onDeleteConstraint` |
| `apps/web/app/groups/[groupId]/tabs/ConstraintsTab.tsx` | **Edit modal scope display**: personal and role constraints show a read-only label (person name / role name) in the edit modal; `scope_type`, `scope_id`, and `rule_type` are not editable |
| `apps/web/app/groups/[groupId]/tabs/ConstraintsTab.tsx` | **Per-section error isolation**: `SectionCreateForm` now manages its own `saving`/`error` state internally; the shared `sectionSaving`/`sectionError` state in the parent is removed |

### Backend tests

| File | Description |
|---|---|
| `apps/api/Jobuler.Tests/Application/PersonalAndRoleConstraintTests.cs` | 15 xUnit tests covering all scope validation paths in `CreateConstraintCommandHandler`: null `linked_user_id`, pending invitation, non-existent person, inactive role, non-existent role, group scope success, registered person + active role success. Includes parameterised property tests for Properties 7, 8, and 9. |

### Frontend tests

| File | Description |
|---|---|
| `apps/web/__tests__/personal-and-role-constraints.test.ts` | 22 pure-logic tests covering: person selector filtering (Property 4), role selector filtering (Property 5), person name resolution with displayName/fullName fallback (Property 3), and constraint partitioning by scopeType (Task 9.4). |

## Key Decisions

- **No new API endpoints.** The existing `GET /spaces/{spaceId}/constraints` already returns all scope types. The frontend partitions the flat list client-side by `scopeType`.
- **`DomainValidationException` → HTTP 422 was already mapped** in `ExceptionHandlingMiddleware`. No backend change was needed.
- **`SectionCreateForm` owns its own error state.** Each section's create form manages `saving` and `error` independently, so a failure in the role section does not affect the person section's UI state.
- **Delete confirmation is inline, not a modal.** The two-step confirm/cancel pattern inside `ConstraintRow` avoids adding a new modal component and keeps the interaction close to the delete button.
- **Role loading is lazy and idempotent.** The constraints `useEffect` only calls `getGroupRoles` when `groupRoles.length === 0`, so switching away and back to the constraints tab does not re-fetch roles unnecessarily.
- **Backend tests use `[Theory]` + `[InlineData]`** (xUnit parameterised) rather than FsCheck, consistent with the existing test project which has no FsCheck dependency.

## How It Connects

- `ConstraintsTab` receives `groupRoles` and `members` from `page.tsx` and uses them to build `roleMap` and `memberMap` for display and for the scope selectors in `SectionCreateForm`.
- `onCreateWithScope` in `page.tsx` is the single write path for all three section forms. It calls `createConstraint` (existing API function) and then re-fetches the full constraint list.
- The backend `CreateConstraintCommandHandler` enforces all scope validation rules independently of the frontend selectors — the frontend filtering is a UX convenience only.
- The solver payload builder already reads all active constraints by scope type; personal and role constraints will be included automatically once created.

## How to Run / Verify

**Backend tests:**
```bash
dotnet test apps/api/Jobuler.Tests/Jobuler.Tests.csproj --filter "PersonalAndRoleConstraintTests"
# Expected: 15 passed, 0 failed
```

**Frontend logic tests:**
```bash
cd apps/web
$env:TS_NODE_PROJECT="tsconfig.tests.json"
node --require ts-node/register __tests__/personal-and-role-constraints.test.ts
# Expected: 22 passed, 0 failed
```

**Manual verification:**
1. Open a group detail page and switch to the "אילוצים" tab.
2. Verify three sections appear: "אילוצי קבוצה", "אילוצי תפקיד", "אילוצים אישיים".
3. In the Role section, click "אילוץ תפקיד חדש" — the role selector should show only active roles.
4. In the Personal section, click "אילוץ אישי חדש" — the person selector should show only registered members (invitationStatus = "accepted").
5. Create a personal constraint — it should appear in the Personal section (not disappear).
6. Click the delete button on any constraint — a confirmation prompt should appear before deletion.
7. Click the edit button on a personal or role constraint — the edit modal should show the person/role name as a read-only label.
8. Try creating a personal constraint for an unregistered person via the API directly — expect HTTP 422 with "Personal constraints can only be applied to registered members."

## What Comes Next

- **Solver integration**: The solver payload builder should be verified to include personal and role constraints in the hard/soft/emergency lists (Requirement 7). This is expected to work automatically since the builder reads all active `ConstraintRule` records.
- **Custom role creation**: When the owner creates custom member roles (future feature), role constraints will work against them automatically via the existing `space_roles` / `person_role_assignments` tables.
- **Property tests 1, 2, 6**: Frontend rendering properties (ConstraintRow display fields, edit form pre-population) can be added with React Testing Library once the project has a Jest setup.

## Git Commit

```bash
git add -A && git commit -m "feat(personal-role-constraints): implement personal and role constraint UI and backend guards"
```
