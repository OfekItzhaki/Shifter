# Implementation Plan: Schedule Table, Auto-Scheduler Gap Detection, and Role Constraints

## Overview

Implementation is ordered so that each layer is ready before the next depends on it:
1. **DB migrations and domain** — schema changes that everything else builds on
2. **Backend API and application layer** — new endpoints, validation, exception types
3. **Solver (Python)** — constraint expansion functions
4. **Frontend** — components that consume the API
5. **Tests** — property-based and unit tests for all layers

---

## Tasks

- [x] 1. DB migration: add `group_id` to `space_roles` and `person_role_assignments`
  - Create EF Core migration `AddGroupIdToSpaceRolesAndPersonRoleAssignments`
  - Add `group_id UUID NULL REFERENCES groups(id)` to `space_roles`
  - Drop old unique index `(space_id, name)` on `space_roles`; add `(space_id, group_id, name)`
  - Add `group_id UUID NULL REFERENCES groups(id)` to `person_role_assignments`
  - Drop old unique index `(person_id, role_id)` on `person_role_assignments`; add `(person_id, role_id, group_id)`
  - _Requirements: 12.1_

- [x] 2. Domain: extend `SpaceRole` and `PersonRoleAssignment` entities
  - Add `GroupId` (`Guid?`) property to `SpaceRole` with private setter
  - Add `SpaceRole.CreateForGroup(spaceId, groupId, name, createdByUserId, description?)` factory method
  - Add `GroupId` (`Guid?`) property to `PersonRoleAssignment` with private setter
  - Update `PersonRoleAssignment.Create` to accept optional `groupId` parameter
  - Update EF configuration in `SpaceConfiguration` / `PeopleConfiguration` (or wherever `SpaceRole` and `PersonRoleAssignment` are configured) to map `group_id` column and the new unique indexes
  - _Requirements: 12.1, 12.3_

- [x] 3. Application: `DomainValidationException` and middleware mapping
  - Create `apps/api/Jobuler.Application/Common/DomainValidationException.cs`
  - Add `DomainValidationException → HTTP 422 Unprocessable Entity` case to `ExceptionHandlingMiddleware`
  - _Requirements: 9.3_

- [x] 4. Application: group-role commands and query
  - Create `CreateGroupRoleCommand(SpaceId, GroupId, Name, Description?, RequestingUserId)` → `Guid` handler
    - Calls `IPermissionService.RequirePermissionAsync(Permissions.PeopleManage)`
    - Creates `SpaceRole.CreateForGroup(...)` and saves
    - Validates name uniqueness within the group (409 on duplicate)
  - Create `UpdateGroupRoleCommand(SpaceId, GroupId, RoleId, Name, Description?, RequestingUserId)` → `Unit` handler
    - Permission check, load role by `id + space_id + group_id`, call `role.Update(...)`, save
    - Throws `KeyNotFoundException` if role not found
  - Create `DeactivateGroupRoleCommand(SpaceId, GroupId, RoleId, RequestingUserId)` → `Unit` handler
    - Permission check, load role, call `role.Deactivate()`, save
  - Create `GetGroupRolesQuery(SpaceId, GroupId)` → `List<GroupRoleDto>` handler
    - Query `SpaceRoles` where `space_id = SpaceId AND group_id = GroupId`
    - Return `GroupRoleDto(Id, Name, Description, IsActive)` records
  - Define `GroupRoleDto` record in the Application layer
  - _Requirements: 12.1, 12.2, 12.3_

- [x] 5. API: `GroupRolesController`
  - Create `apps/api/Jobuler.Api/Controllers/GroupRolesController.cs`
  - Route: `[Route("spaces/{spaceId}/groups/{groupId}/roles")]`
  - `GET /` → `GetGroupRolesQuery` (requires `SpaceView` permission)
  - `POST /` → `CreateGroupRoleCommand` (requires `PeopleManage` permission)
  - `PUT /{roleId}` → `UpdateGroupRoleCommand` (requires `PeopleManage` permission)
  - `DELETE /{roleId}` → `DeactivateGroupRoleCommand` (requires `PeopleManage` permission)
  - All actions call `IPermissionService.RequirePermissionAsync` before dispatching
  - Add `[Authorize]` attribute to controller
  - _Requirements: 12.1, 12.2, 12.3_

- [x] 6. Application: enhance `CreateConstraintCommandHandler` with scope validation
  - After permission check, add role-scope validation:
    - If `ScopeType == Role`: verify `ScopeId` is non-null (→ 400 if null)
    - Query `SpaceRoles` for `id = ScopeId AND space_id = SpaceId AND is_active = true`
    - Throw `KeyNotFoundException("Role not found in this space.")` if not found
  - Add person-scope validation:
    - If `ScopeType == Person`: verify `ScopeId` is non-null (→ 400 if null)
    - Query `People` for `id = ScopeId AND space_id = SpaceId`
    - Throw `KeyNotFoundException("Person not found in this space.")` if not found
    - Check `linked_user_id IS NOT NULL AND invitation_status = "accepted"`
    - Throw `DomainValidationException("Personal constraints can only be applied to registered members.")` if not registered
  - _Requirements: 8.1, 8.2, 8.3, 9.1, 9.2, 9.3, 9.4_

- [x] 7. Infrastructure: `SolverPayloadNormalizer` — effective-date filtering
  - Replace the unfiltered `ConstraintRules` query with a filtered version:
    ```csharp
    .Where(c => c.SpaceId == spaceId && c.IsActive
        && (c.EffectiveUntil == null || c.EffectiveUntil >= horizonStartDate)
        && (c.EffectiveFrom == null || c.EffectiveFrom <= horizonEndDate))
    ```
  - Applies uniformly to all scope types (group, role, person) — no separate pipeline needed
  - _Requirements: 10.4, 13.1, 13.2, 13.3, 13.4_

- [x] 8. Infrastructure: `AutoSchedulerService` — slot-level gap detection
  - Replace the `latestAssignmentEnd` coverage check in `CheckGroupAsync` with a slot-level gap scan:
    - Query all active `TaskSlots` for the space where `StartsAt` is within `[today, today + horizonDays)`
    - For each slot, check whether a published `Assignment` exists (`ScheduleVersionId = publishedVersion.Id`)
    - If any slot has no assignment → `needsNewSchedule = true`
  - Log gap slot IDs and start times at `Information` level before triggering
  - Trigger solver once per group (not once per gap) — existing `TriggerSolverCommand` call is preserved
  - Pass `publishedVersion.Id` as `baselineVersionId` in `TriggerSolverCommand`
  - Preserve all existing skip guards (active run, draft, recent failure) — check them before the gap scan
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8_

- [x] 9. Checkpoint — ensure API builds and all existing tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Solver: `expand_role_constraints` and `expand_group_constraints` in `constraints.py`
  - Add `expand_role_constraints(hard_constraints, soft_constraints, emergency_constraints, people)`:
    - Build `role_id → [person_id, ...]` map from `people[i].role_ids`
    - For each constraint with `scope_type == "role"` in all three lists:
      - Expand to one person-scoped copy per member of that role
      - Remove the original role-scoped constraint
    - Log a warning (do not raise) if a role has zero members
    - Return the modified (hard, soft, emergency) tuple
  - Add `expand_group_constraints(hard_constraints, soft_constraints, emergency_constraints, people)`:
    - Build `group_id → [person_id, ...]` map from `people[i].group_ids`
    - For each constraint with `scope_type == "group"` in all three lists:
      - Expand to one person-scoped copy per group member
      - Remove the original group-scoped constraint
    - Log a warning if a group has zero members
    - Return the modified (hard, soft, emergency) tuple
  - Call both functions at the top of `solve()` in `engine.py`, before any constraint functions are invoked
  - _Requirements: 10.1, 10.2, 10.3_

- [x] 11. Checkpoint — ensure solver tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Frontend: `ScheduleTable2D` component
  - Create `apps/web/components/schedule/ScheduleTable2D.tsx`
  - Accept props: `assignments: ScheduleAssignment[]`, `currentUserName?: string`, `filterDate?: string`
  - Filter assignments to those overlapping `filterDate` (if provided) using the existing `overlapsDate` logic
  - Derive unique task names → columns (sorted alphabetically)
  - Derive unique time slots (start–end pairs) → rows (sorted by start time)
  - Build `Map<slotKey, Map<taskName, string[]>>` where `slotKey = "${startsAt}|${endsAt}"`
  - Render `<table>` inside an `overflow-x-auto` wrapper
  - Column header for the current user's task gets `bg-blue-50` highlight class
  - Cells with multiple people render names joined by `<br />`
  - Empty cells render `—` in a muted colour (`text-slate-300`)
  - Show Hebrew empty-state message when no assignments match the filter
  - _Requirements: 1.1, 1.2, 1.3, 1.7, 1.8_

- [x] 13. Frontend: update `ScheduleTab` — day view and week view
  - Day view: replace the existing `<table>` list with `<ScheduleTable2D assignments={dayAssignments} filterDate={scheduleDate} currentUserName={currentUserName} />`
  - Week view: replace per-day card list with seven day-name tab buttons (Sun–Sat)
    - Add `selectedWeekDay` state, default to today's day index on mount
    - Clicking a tab sets `selectedWeekDay`; render `<ScheduleTable2D>` for that day
    - Today's tab gets `bg-blue-500 text-white` highlight
  - Remove `"month"` and `"year"` view options from the view toggle (keep only `"day"` and `"week"`)
  - Accept new `currentUserName?: string` prop and pass it down to `ScheduleTable2D`
  - Update `GroupDetailPage` (`apps/web/app/groups/[groupId]/page.tsx`) to pass `currentUserName` to `ScheduleTab`
  - _Requirements: 1.1, 1.4, 1.5, 1.6, 1.8, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [x] 14. Frontend: update admin schedule page to use `ScheduleTable2D`
  - In `apps/web/app/admin/schedule/page.tsx`, replace `<ScheduleTable assignments={selected.assignments} />` with `<ScheduleTable2D assignments={selected.assignments} filterDate={selectedDate} />`
  - Add `selectedDate` state (default to today's ISO date string)
  - Add a date picker / day navigation control (prev/next day buttons + date label) above the table
  - Preserve all existing functionality: version sidebar, publish/rollback/discard, diff card, CSV/PDF export, infeasibility banner, solver trigger buttons
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 15. Frontend: API client for group roles
  - Add `getGroupRoles(spaceId, groupId)`, `createGroupRole(spaceId, groupId, body)`, `updateGroupRole(spaceId, groupId, roleId, body)`, `deactivateGroupRole(spaceId, groupId, roleId)` to `apps/web/lib/api/groups.ts` (or a new `roles.ts` file)
  - Define `GroupRoleDto` TypeScript interface: `{ id: string; name: string; description?: string; isActive: boolean }`
  - _Requirements: 12.1, 12.2, 12.3_

- [x] 16. Frontend: `SettingsTab` — Roles management section
  - In `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx`, add a "תפקידים" section below existing settings
  - Accept new props: `groupRoles: GroupRoleDto[]`, `groupRolesLoading: boolean`, `onCreateRole`, `onUpdateRole`, `onDeactivateRole`
  - Render a list of roles; active roles show rename and deactivate buttons
  - Deactivated roles are shown with strikethrough and no action buttons
  - Render an inline "Add Role" form (name + optional description) with a submit button
  - Wire up `onCreateRole`, `onUpdateRole`, `onDeactivateRole` callbacks
  - Update `GroupDetailPage` to fetch group roles and pass them + callbacks to `SettingsTab`
  - _Requirements: 12.5, 12.6_

- [x] 17. Frontend: `ConstraintsTab` — three-section restructure
  - In `apps/web/app/groups/[groupId]/tabs/ConstraintsTab.tsx`, restructure into three collapsible sections:
    1. **אילוצי קבוצה** — constraints where `scopeType === "group"` and `scopeId === groupId`
    2. **אילוצי תפקיד** — constraints where `scopeType === "role"`
    3. **אילוצים אישיים** — constraints where `scopeType === "person"`
  - Each section has its own "New" button, list, and create/edit modal
  - Role constraint create form: add role selector dropdown populated from active `groupRoles` only
  - Personal constraint create form: add person selector dropdown populated from `members` filtered to `linkedUserId !== null`
  - Accept new props: `groupRoles: GroupRoleDto[]`, `groupRolesLoading: boolean`, `members: GroupMemberDto[]`
  - Update `GroupDetailPage` to pass `groupRoles` and `members` to `ConstraintsTab`
  - Display Hebrew error messages below forms on API errors
  - Re-fetch constraints list after successful create/update/delete without full page reload
  - _Requirements: 5.1–5.8, 6.1–6.9, 7.1–7.8_

- [x] 18. Checkpoint — ensure frontend builds and all existing tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 19. Tests: backend — `CreateConstraintCommandHandler` scope validation
  - [x] 19.1 Write property test for role constraint scope validation (Property 7)
    - **Property 7: Role constraint scope validation**
    - **Validates: Requirements 8.1, 8.2**
    - Use FsCheck to generate random role IDs (existing active, existing inactive, missing)
    - Verify: missing or inactive role → no constraint inserted, non-2xx response
  - [x] 19.2 Write property test for person constraint scope validation (Property 8)
    - **Property 8: Person constraint scope validation**
    - **Validates: Requirements 9.1, 9.2, 9.3**
    - Use FsCheck to generate random person states (missing, unregistered, registered)
    - Verify: missing → 404, unregistered → 422, registered → 201, no insert on error
  - [x] 19.3 Write unit tests for `CreateConstraintCommandHandler` role validation
    - Test: null `ScopeId` with `ScopeType = Role` → 400
    - Test: inactive role → 404
    - Test: valid active role → 201
  - [x] 19.4 Write unit tests for `CreateConstraintCommandHandler` person validation
    - Test: null `ScopeId` with `ScopeType = Person` → 400
    - Test: person not in space → 404
    - Test: person with null `linked_user_id` → 422
    - Test: registered person → 201

- [ ] 20. Tests: backend — `SolverPayloadNormalizer` effective-date filtering
  - [x] 20.1 Write property test for effective-date filtering uniformity (Property 10)
    - **Property 10: Effective-date filtering is uniform across scope types**
    - **Validates: Requirements 10.4, 13.1, 13.2, 13.3, 13.4**
    - Use FsCheck to generate random date ranges relative to horizon
    - Verify: constraint included iff effective window overlaps horizon, for all scope types
  - [x] 20.2 Write property test for payload including all scope types (Property 9)
    - **Property 9: Solver payload includes all three constraint scope levels**
    - **Validates: Requirements 10.1, 10.2, 10.3**
    - Generate constraint sets with group, role, and person scope types
    - Verify all three scope types appear in the built payload
  - [x] 20.3 Write unit tests for `SolverPayloadNormalizer` date filtering
    - Test: constraint with `effective_until < horizonStart` → excluded
    - Test: constraint with `effective_from > horizonEnd` → excluded
    - Test: constraint with null dates → always included
    - Test: constraint overlapping horizon → included

- [ ] 21. Tests: backend — `AutoSchedulerService` gap detection
  - [x] 21.1 Write property test for gap detection triggers solver exactly once (Property 5)
    - **Property 5: Gap detection triggers solver exactly once per group**
    - **Validates: Requirements 4.1, 4.2**
    - Use FsCheck to generate random task slot sets with random coverage gaps
    - Verify: at least one gap → solver triggered exactly once; no gaps → not triggered
  - [x] 21.2 Write unit tests for `AutoSchedulerService.CheckGroupAsync`
    - Test: all slots covered → no trigger
    - Test: one slot uncovered → trigger once
    - Test: all slots uncovered → trigger once (not N times)
    - Test: active run exists → skip (no trigger)
    - Test: draft exists → skip
    - Test: recent failure → skip

- [ ] 22. Tests: backend — `GroupRolesController` and commands
  - [x] 22.1 Write property test for group role creation scoping (Property 11)
    - **Property 11: Group role creation is group-scoped**
    - **Validates: Requirements 12.1, 12.7**
    - Use FsCheck to generate random role names and two groups
    - Verify: created role has `group_id = groupId`; fetching roles for other group does not return it
  - [x] 22.2 Write property test for role update round-trip (Property 12)
    - **Property 12: Role update round-trip**
    - **Validates: Requirements 12.2**
    - Generate random names/descriptions, PUT then GET, verify values match
  - [x] 22.3 Write unit tests for group role CRUD
    - Test: create role → 201 with ID
    - Test: create duplicate name in same group → 409
    - Test: same name in different group → 201 (allowed)
    - Test: update role → 200, GET returns updated values
    - Test: deactivate role → 200, `is_active = false`
    - Test: missing permission → 403

- [ ] 23. Tests: solver — constraint expansion
  - [x] 23.1 Write unit tests for `expand_role_constraints`
    - Test: role with 0 members → warning logged, 0 expanded constraints, original removed
    - Test: role with 1 member → 1 person-scoped constraint
    - Test: role with N members → N person-scoped constraints
    - Test: expansion applies to hard, soft, and emergency lists
  - [x] 23.2 Write unit tests for `expand_group_constraints`
    - Test: group with 0 members → warning logged, 0 expanded constraints
    - Test: group with 1 member → 1 person-scoped constraint
    - Test: group with N members → N person-scoped constraints
    - Test: expansion applies to all three severity lists

- [ ] 24. Tests: frontend — `ScheduleTable2D` property tests
  - [x] 24.1 Write property test for column and row completeness (Property 1)
    - **Property 1: ScheduleTable2D column and row completeness**
    - **Validates: Requirements 1.1**
    - Use fast-check to generate random assignment arrays (1–20 tasks, 1–10 slots)
    - Verify: exactly one column header per unique task name, one row header per unique slot pair
  - [x] 24.2 Write property test for multi-person cell grouping (Property 2)
    - **Property 2: Multi-person cell grouping**
    - **Validates: Requirements 1.2**
    - Generate assignments with 2–5 people sharing the same task and slot
    - Verify: all names appear in the same cell
  - [x] 24.3 Write property test for current user column highlight (Property 3)
    - **Property 3: Current user column highlight**
    - **Validates: Requirements 1.8**
    - Generate random assignments + random current user name
    - Verify: user's task column has highlight class; all others do not
  - [x] 24.4 Write property test for date filter correctness (Property 4)
    - **Property 4: Date filter correctness**
    - **Validates: Requirements 3.2**
    - Generate assignments spanning 1–14 dates, random filter date
    - Verify: only assignments overlapping filter date are rendered

- [ ] 25. Tests: frontend — `ConstraintsTab` property tests
  - [x] 25.1 Write property test for constraint scope filtering in UI (Property 6)
    - **Property 6: Constraint scope filtering in UI**
    - **Validates: Requirements 5.1, 6.1, 7.1**
    - Use fast-check to generate random constraint lists with mixed scope types
    - Verify: group section shows only group constraints, role section only role, personal only person; no constraint in two sections
  - [x] 25.2 Write property test for active-only role selector (Property 13)
    - **Property 13: Active-only role selector**
    - **Validates: Requirements 12.6**
    - Generate role lists with mixed active/inactive roles
    - Verify: role selector in role constraint form shows only active roles

- [x] 26. Final checkpoint — ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

---

## Manual Override Assignments (Requirement 14)

- [x] 27. Backend: manual override domain and command
  - Add `ManualOverride` value object or flag to `Assignment` entity: `is_manual_override: bool`, `overridden_by_user_id: Guid?`
  - Create `ApplyManualOverrideCommand(SpaceId, GroupId, SlotId, NewPersonIds, RequestingUserId)` → `Guid` (returns draft version ID)
    - Requires `schedule.publish` permission
    - If no draft exists for the group, create a new draft version cloned from the current published version
    - Apply the override: remove existing assignments for the slot in the draft, insert new ones with `is_manual_override = true`
    - Write audit log entry: actor, slot ID, previous assignees, new assignees, timestamp
  - Create `RemoveManualOverrideCommand(SpaceId, GroupId, SlotId, RequestingUserId)` → `Unit`
    - Requires `schedule.publish` permission
    - Marks the slot as explicitly unassigned in the draft (no assignment row, but slot is "touched")
  - _Requirements: 14.3, 14.4, 14.5, 14.7_

- [x] 28. Backend: solver respects manual override locks
  - In `SolverPayloadNormalizer`, include a `locked_slot_ids` list in the solver payload: all slot IDs in the baseline version that have `is_manual_override = true`
  - In `engine.py`, add `add_locked_slot_constraints`: for each locked slot, force the solver to keep the same person assignment from the baseline
  - _Requirements: 14.8_

- [x] 29. API: manual override endpoint
  - Add `POST /spaces/{spaceId}/groups/{groupId}/schedule/overrides` → `ApplyManualOverrideCommand`
    - Body: `{ slotId, newPersonIds: string[] }`
  - Add `DELETE /spaces/{spaceId}/groups/{groupId}/schedule/overrides/{slotId}` → `RemoveManualOverrideCommand`
  - Both require `[Authorize]` and `schedule.publish` permission check
  - _Requirements: 14.5_

- [x] 30. Frontend: override modal in admin schedule table
  - In `ScheduleTable2D`, make cells clickable when `onCellClick` prop is provided
  - Create `OverrideModal` component: shows current assignees, person multi-selector (eligible members only), confirm/cancel buttons
  - Wire up in admin schedule page: clicking a cell opens `OverrideModal`, on confirm calls `POST .../overrides`, refreshes schedule data
  - Show Hebrew confirmation and error messages
  - _Requirements: 14.1, 14.2, 14.6_

---

## Live Person Status Panel (Requirement 15)

- [x] 31. Backend: live status query
  - Create `GetGroupLiveStatusQuery(SpaceId, GroupId, RequestingUserId)` → `List<MemberLiveStatusDto>`
    - For each group member, determine current status:
      1. Check `presence_windows` for a record where `starts_at <= now <= ends_at` (manual override wins)
      2. If no manual presence window, check published assignments where `starts_at <= now <= ends_at`
      3. Default to `free_in_base` if neither applies
    - Return `MemberLiveStatusDto(PersonId, DisplayName, Status, TaskName?, SlotEndsAt?, Location?)`
  - _Requirements: 15.6, 15.7, 15.8_

- [x] 32. API: live status endpoint
  - Add `GET /spaces/{spaceId}/groups/{groupId}/live-status` → `GetGroupLiveStatusQuery`
  - Requires `[Authorize]` — any group member can call this
  - _Requirements: 15.8_

- [x] 33. Frontend: live status panel UI
  - Create `LiveStatusPanel` component in `apps/web/components/schedule/LiveStatusPanel.tsx`
  - Display each member with status badge: `on_mission` (blue), `at_home` (yellow), `blocked` (red), `free_in_base` (green)
  - For `on_mission` members: show task name and slot end time
  - Poll `GET .../live-status` every 30 seconds; show last-updated timestamp
  - Add "סטטוס נוכחי" tab to `GroupDetailPage` that renders `LiveStatusPanel`
  - All labels in Hebrew
  - _Requirements: 15.1, 15.2, 15.3, 15.4, 15.5, 15.9_

- [x] 34. Final checkpoint — ensure all tests pass after new features
  - Ensure all tests pass, ask the user if questions arise.

---

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at each layer boundary
- Property tests use fast-check (TypeScript), Hypothesis (Python), and FsCheck (C#)
- Unit tests complement property tests — both are needed
- The existing `ScheduleTable` component at `apps/web/components/schedule/ScheduleTable.tsx` is kept but no longer used by the admin page after task 14
