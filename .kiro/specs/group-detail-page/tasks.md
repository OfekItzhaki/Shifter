# Implementation Plan: Group Detail Page

## Overview

Three coordinated changes: extend the groups API client, build the full group detail page, restructure AppShell navigation, randomize seed UUIDs, and add step documentation. Tasks are ordered so each step builds on the previous one with no orphaned code.

## Tasks

- [x] 1. Extend `lib/api/groups.ts` with new DTOs and API functions
  - Add `GroupWithMemberCountDto` interface (`id`, `name`, `memberCount`, `solverHorizonDays`)
  - Add `GroupMemberDto` interface (`personId`, `fullName`, `displayName: string | null`)
  - Implement `getGroups(spaceId)` → `GET /spaces/{spaceId}/groups`
  - Implement `getGroupMembers(spaceId, groupId)` → `GET /spaces/{spaceId}/groups/{groupId}/members`
  - Implement `addGroupMemberByEmail(spaceId, groupId, email)` → `POST /spaces/{spaceId}/groups/{groupId}/members/by-email`
  - Implement `removeGroupMember(spaceId, groupId, personId)` → `DELETE /spaces/{spaceId}/groups/{groupId}/members/{personId}`
  - Implement `updateGroupSettings(spaceId, groupId, solverHorizonDays)` → `PATCH /spaces/{spaceId}/groups/{groupId}/settings`
  - _Requirements: 1.2, 2.3, 3.3, 3.5, 3.11_

- [x] 2. Build the GroupDetailPage — header, not-found state, and admin toggle
  - [x] 2.1 Implement page scaffold with group fetch and header
    - Replace placeholder in `apps/web/app/groups/[groupId]/page.tsx`
    - Import `getGroups` from `lib/api/groups`, `useSpaceStore`, `useAuthStore`, `useParams`, `useRouter`
    - Declare local state: `group`, `notFound`, `members`, `activeTab`, `loading`, `membersLoading`, `addEmail`, `addError`, `settingsError`, `solverHorizon`, `savingSettings`
    - `useEffect` on `currentSpaceId`: call `getGroups`, find by `groupId`, set `group` or `notFound`
    - Render header with group `name` and `memberCount` when found
    - Render "קבוצה לא נמצאה" + back link to `/groups` when `notFound` is true
    - _Requirements: 1.1, 1.2, 1.3_

  - [ ]* 2.2 Write property test for group lookup correctness
    - **Property 1: Group lookup correctness**
    - For any array of `GroupWithMemberCountDto` and any `groupId` string, `list.find(g => g.id === groupId) ?? null` returns the correct group or null
    - **Validates: Requirements 1.1, 1.2**

  - [x] 2.3 Implement admin toggle button and `useEffect` cleanup
    - When `adminGroupId !== groupId`: render button "כניסה למצב מנהל" → calls `enterAdminMode(groupId)`
    - When `adminGroupId === groupId`: render button "יציאה ממצב מנהל" → calls `exitAdminMode()`
    - Add `useEffect` with empty-dep cleanup: `return () => exitAdminMode()`
    - _Requirements: 1.4, 1.5, 1.6, 1.7, 1.8_

- [x] 3. Build the tab bar and base tabs (סידור + חברים read-only)
  - [x] 3.1 Implement tab bar rendering logic
    - Define `type ActiveTab = "schedule" | "members-readonly" | "members-edit" | "tasks" | "constraints" | "settings"`
    - Always render "סידור" and "חברים" tabs
    - When `adminGroupId === groupId`, additionally render "חברים" (edit), "משימות", "אילוצים", "הגדרות" tabs
    - When admin mode exits and `activeTab` is an admin-only tab, reset `activeTab` to `"schedule"` via `useEffect` on `adminGroupId`
    - _Requirements: 2.1, 3.1_

  - [ ]* 3.2 Write property test for base tabs always present
    - **Property 2: Base tabs always present**
    - For any value of `adminGroupId` (null, equal to groupId, or any other string), both "סידור" and "חברים" tab labels are present in the rendered tab bar
    - **Validates: Requirements 2.1**

  - [ ]* 3.3 Write property test for admin tabs conditional on adminGroupId
    - **Property 4: Admin tabs appear exactly when adminGroupId matches**
    - For any `groupId` and any `adminGroupId`: admin tabs present iff `adminGroupId === groupId`; none of the four admin tabs rendered otherwise
    - **Validates: Requirements 3.1**

  - [x] 3.4 Implement the סידור tab panel
    - When `activeTab === "schedule"`: fetch `GET /spaces/{spaceId}/groups/{groupId}/schedule` via `apiClient`
    - Display schedule data; show "שגיאה בטעינת הסידור" on fetch error
    - _Requirements: 2.2_

  - [x] 3.5 Implement the חברים read-only tab panel
    - When `activeTab === "members-readonly"`: call `getGroupMembers` and store in `members` state
    - Display each member as `member.displayName ?? member.fullName`
    - Show "אין חברים בקבוצה זו" when list is empty
    - Show "שגיאה בטעינת החברים" on fetch error
    - _Requirements: 2.3, 2.4, 2.5_

  - [ ]* 3.6 Write property test for displayName fallback
    - **Property 3: DisplayName fallback**
    - For any `GroupMemberDto`, `getDisplayName(m)` returns `m.displayName` when non-null, and `m.fullName` when `displayName` is null
    - **Validates: Requirements 2.4**

- [x] 4. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Build the admin-only tab panels
  - [x] 5.1 Implement the חברים admin-edit tab panel
    - When `activeTab === "members-edit"` and `adminGroupId === groupId`: show same member list as read-only
    - Add "הוסף לפי אימייל" input bound to `addEmail` state + submit button
    - On submit: call `addGroupMemberByEmail`, then re-fetch members via `getGroupMembers`; display `err?.response?.data?.message` below input on error
    - Per-member remove button: call `removeGroupMember(spaceId, groupId, member.personId)`, then re-fetch members
    - _Requirements: 3.2, 3.3, 3.4, 3.5, 3.6_

  - [ ]* 5.2 Write property test for members list re-fetched after mutation
    - **Property 5: Members list re-fetched after any mutation**
    - Mock `addGroupMemberByEmail` and `removeGroupMember` to resolve successfully; assert `getGroupMembers` is called once after each successful mutation
    - **Validates: Requirements 3.6**

  - [x] 5.3 Implement the משימות tab panel
    - When `activeTab === "tasks"`: render the task types table and task slots table reusing the same display logic as `/admin/tasks`
    - Import `getTaskTypes`, `getTaskSlots`, `TaskTypeDto`, `TaskSlotDto` from `lib/api/tasks`
    - _Requirements: 3.7_

  - [x] 5.4 Implement the אילוצים tab panel
    - When `activeTab === "constraints"`: render the constraints table reusing the same display logic as `/admin/constraints`
    - Import `getConstraints`, `ConstraintDto` from `lib/api/constraints`
    - _Requirements: 3.8_

  - [x] 5.5 Implement the הגדרות tab panel
    - When `activeTab === "settings"`: render a slider for `solverHorizon` with `min=1`, `max=90`, bound to `solverHorizon` state (default 14)
    - When `solverHorizon > 30`: display a complexity warning message alongside the slider
    - Save button: call `updateGroupSettings(spaceId, groupId, solverHorizon)`, set `savingSettings` during call; display `err?.response?.data?.message` on error
    - _Requirements: 3.9, 3.10, 3.11, 3.12_

  - [ ]* 5.6 Write property test for solver horizon warning threshold
    - **Property 6: Solver horizon warning threshold**
    - For any integer `v` in range 1–90: warning message is displayed iff `v > 30`
    - **Validates: Requirements 3.10**

- [x] 6. Restructure AppShell navigation
  - [x] 6.1 Remove Admin sidebar section and global admin toggle from AppShell
    - In `apps/web/components/shell/AppShell.tsx`: remove the `{isAdminMode && <> Admin section </>}` block from `<nav>`
    - Remove the `enterAdminMode` button from the topbar
    - Remove the `exitAdminMode` button from the topbar
    - Remove the `adminBadge` div from the topbar
    - Remove `adminBtn` from the `S` style object
    - Replace `isAdminMode` destructure with `adminGroupId` from `useAuthStore`
    - Drive `S.topbar(admin)` with `adminGroupId !== null` instead of `isAdminMode`
    - _Requirements: 4.3, 4.4, 4.5_

  - [x] 6.2 Add קבוצות nav item to AppShell sidebar
    - Add `<NavItem href="/groups" label="קבוצות" icon={...} />` below the סידור section
    - Use a groups/people SVG icon consistent with the existing icon style
    - _Requirements: 4.2_

  - [ ]* 6.3 Write property test for no Admin section in AppShell
    - **Property 7: No Admin section in AppShell for any adminGroupId**
    - For any value of `adminGroupId` (null or any non-null string), the rendered AppShell sidebar contains no "Admin" section label and no `/admin/*` nav links
    - **Validates: Requirements 4.3**

  - [ ]* 6.4 Write property test for amber topbar when adminGroupId is non-null
    - **Property 8: Amber topbar when adminGroupId is non-null**
    - For any non-null `adminGroupId`, topbar background is `#fffbeb`; when null, topbar background is white
    - **Validates: Requirements 4.6**

  - [x] 6.5 Verify NotificationBell and logout button are unchanged
    - Confirm `<NotificationBell />` and the logout button remain in their existing positions in the topbar and sidebar bottom respectively
    - _Requirements: 4.7_

- [x] 7. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Randomize seed UUIDs in `infra/scripts/seed.sql`
  - [x] 8.1 Add UUID mapping comment block at top of seed.sql
    - Insert a comment block listing all 26 old→new UUID mappings from the design doc mapping table
    - _Requirements: 5.5_

  - [x] 8.2 Replace all sequential UUIDs with random-looking UUID v4 values
    - Replace every occurrence of each old sequential UUID with its corresponding new UUID from the design doc mapping table
    - Update all FK references in the same file so relationships are preserved (space_memberships, space_permission_grants, space_roles, group_types, groups, people, task_types all reference the space UUID and user UUIDs)
    - Preserve all `ON CONFLICT DO NOTHING` / `ON CONFLICT DO UPDATE` clauses unchanged
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ]* 8.3 Write property test for seed UUID validity and FK integrity
    - **Property 9: Seed UUID validity and FK integrity**
    - Parse `seed.sql` statically; assert every UUID matches the v4 regex `[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}`; assert every UUID used as a FK reference also appears as a PK definition in the same file
    - **Validates: Requirements 5.1, 5.2**

- [x] 9. Create step documentation
  - Create `docs/steps/027-group-detail-page.md` following the format of `docs/steps/026-*.md`
  - Include: title, phase, purpose, what was built (all 4 changed files), key decisions, how it connects, how to run/verify, what comes next, and git commit command
  - _Requirements: (workspace step-documentation rule)_

- [x] 10. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Property tests use `fast-check` if available in the project; otherwise write example-based tests covering the same properties
- Properties 9 and 10 (seed UUID validity and seed idempotence) are best verified by running the seed script against a test DB — Property 9's static parse check is included in task 8.3
- AppShell uses the inline `S` object pattern (no Tailwind); GroupDetailPage uses Tailwind CSS classes matching `groups/page.tsx`
