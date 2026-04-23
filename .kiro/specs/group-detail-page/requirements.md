# Requirements Document

## Introduction

This feature delivers three coordinated changes to the Jobuler scheduling app:

1. **Group Detail Page** (`/groups/[groupId]`) — a full tabbed page for viewing and managing a single group. All members see schedule and member-list tabs. Users who activate admin mode for that specific group gain additional tabs for member management, task types/slots, constraints, and solver settings.

2. **AppShell Navigation Restructure** — the global "Admin" sidebar section and the global admin-mode toggle in the topbar are removed. Navigation is simplified to סידור (with sub-items) and קבוצות. Admin functionality is now accessed per-group from within the group detail page.

3. **Seed Data UUID Randomization** — sequential fake UUIDs in `seed.sql` are replaced with random-looking UUIDs to avoid accidental collisions and to better reflect production data.

All admin permission checks remain server-side via `IPermissionService`. The `adminGroupId` field in `authStore` is scoped per group and is never persisted across page loads.

---

## Glossary

- **GroupDetailPage**: The Next.js page at `/groups/[groupId]` that renders group information and tabbed management UI.
- **AppShell**: The shared layout component wrapping all authenticated pages, containing the sidebar and topbar.
- **AuthStore**: The Zustand store (`authStore.ts`) holding authentication state including `adminGroupId`.
- **adminGroupId**: A non-persisted field in AuthStore — `string | null` — representing which group the user has activated admin mode for. `null` means no admin mode is active.
- **Tab**: A selectable panel within the GroupDetailPage. Tabs are conditionally rendered based on admin mode state.
- **IPermissionService**: The server-side permission service in the Application layer. All write operations are gated by it.
- **SpaceStore**: The Zustand store holding `currentSpaceId`, used to scope all API calls.
- **SolverHorizon**: The number of days ahead the solver considers when generating schedules. Configurable per group.
- **TaskType**: A category of work (e.g., "Post 1", "Kitchen") defined at the space level.
- **TaskSlot**: A scheduled instance of a TaskType with a time range and required headcount.
- **Constraint**: A scheduling rule (e.g., min rest between shifts) scoped to a group or person.

---

## Requirements

### Requirement 1: Group Detail Page — Header and Admin Toggle

**User Story:** As a group member, I want to open a group detail page that shows the group name and member count, so that I can quickly identify which group I am viewing.

#### Acceptance Criteria

1. WHEN a user navigates to `/groups/[groupId]`, THE GroupDetailPage SHALL display the group name and member count in a header section.
2. WHEN the GroupDetailPage loads, THE GroupDetailPage SHALL fetch group details from `GET /spaces/{spaceId}/groups` and display the matching group's name and member count.
3. IF the group is not found in the fetched list, THEN THE GroupDetailPage SHALL display a "קבוצה לא נמצאה" message and a back link to `/groups`.
4. WHEN the GroupDetailPage loads and `adminGroupId` in AuthStore does not equal `groupId`, THE GroupDetailPage SHALL display a button labelled "כניסה למצב מנהל".
5. WHEN the user clicks "כניסה למצב מנהל", THE GroupDetailPage SHALL call `enterAdminMode(groupId)` on AuthStore, setting `adminGroupId` to the current `groupId`.
6. WHEN `adminGroupId` in AuthStore equals `groupId`, THE GroupDetailPage SHALL display a button labelled "יציאה ממצב מנהל" in place of the enter-admin button.
7. WHEN the user clicks "יציאה ממצב מנהל", THE GroupDetailPage SHALL call `exitAdminMode()` on AuthStore, setting `adminGroupId` to `null`.
8. WHEN the GroupDetailPage unmounts, THE GroupDetailPage SHALL call `exitAdminMode()` via a `useEffect` cleanup function.

---

### Requirement 2: Group Detail Page — Tabs for All Members

**User Story:** As a group member, I want to see the group schedule and a read-only member list, so that I can stay informed about assignments and who is in my group.

#### Acceptance Criteria

1. THE GroupDetailPage SHALL always render a "סידור" tab and a "חברים" tab regardless of admin mode state.
2. WHEN the "סידור" tab is active, THE GroupDetailPage SHALL fetch and display the group schedule from `GET /spaces/{spaceId}/groups/{groupId}/schedule`.
3. WHEN the "חברים" tab is active and `adminGroupId` does not equal `groupId`, THE GroupDetailPage SHALL display a read-only list of group members fetched from `GET /spaces/{spaceId}/groups/{groupId}/members`.
4. THE read-only members list SHALL display each member's `displayName` (falling back to `fullName` if `displayName` is null).
5. IF the members list is empty, THEN THE GroupDetailPage SHALL display the message "אין חברים בקבוצה זו".

---

### Requirement 3: Group Detail Page — Admin-Only Tabs

**User Story:** As a group admin, I want additional management tabs to appear when I activate admin mode for a group, so that I can manage members, tasks, constraints, and solver settings from one place.

#### Acceptance Criteria

1. WHEN `adminGroupId` equals `groupId`, THE GroupDetailPage SHALL render four additional tabs: "חברים" (edit mode), "משימות", "אילוצים", and "הגדרות".
2. WHEN `adminGroupId` equals `groupId` and the "חברים" tab is active, THE GroupDetailPage SHALL display the member list with an "הוסף לפי אימייל" input and a remove button per member.
3. WHEN the admin submits a valid email in the add-member form, THE GroupDetailPage SHALL call `POST /spaces/{spaceId}/groups/{groupId}/members/by-email` with the email value.
4. IF the add-member API call returns an error, THEN THE GroupDetailPage SHALL display the error message returned by the server below the input field.
5. WHEN the admin clicks the remove button for a member, THE GroupDetailPage SHALL call `DELETE /spaces/{spaceId}/groups/{groupId}/members/{personId}`.
6. WHEN a member is successfully added or removed, THE GroupDetailPage SHALL re-fetch the members list to reflect the updated state.
7. WHEN `adminGroupId` equals `groupId` and the "משימות" tab is active, THE GroupDetailPage SHALL display the list of task types from `GET /spaces/{spaceId}/task-types` and the list of task slots from `GET /spaces/{spaceId}/task-slots`.
8. WHEN `adminGroupId` equals `groupId` and the "אילוצים" tab is active, THE GroupDetailPage SHALL display the list of constraints from `GET /spaces/{spaceId}/constraints`.
9. WHEN `adminGroupId` equals `groupId` and the "הגדרות" tab is active, THE GroupDetailPage SHALL display a slider for `solverHorizonDays` with a range of 1–90 days.
10. WHEN the solver horizon slider value exceeds 30 days, THE GroupDetailPage SHALL display a complexity warning message alongside the slider.
11. WHEN the admin saves the settings, THE GroupDetailPage SHALL call `PATCH /spaces/{spaceId}/groups/{groupId}/settings` with the updated `solverHorizonDays` value.
12. IF the settings save API call returns an error, THEN THE GroupDetailPage SHALL display the error message returned by the server.

---

### Requirement 4: AppShell Navigation Restructure

**User Story:** As a user, I want a simplified navigation sidebar that reflects the new per-group admin model, so that I am not confused by a global admin toggle that no longer applies.

#### Acceptance Criteria

1. THE AppShell SHALL render a "סידור" section in the sidebar containing three sub-items: "היום" (linking to `/schedule/today`), "מחר" (linking to `/schedule/tomorrow`), and "המשימות שלי" (linking to `/schedule/my-missions`).
2. THE AppShell SHALL render a "קבוצות" nav item linking to `/groups`.
3. THE AppShell SHALL NOT render the "Admin" sidebar section regardless of the value of `adminGroupId`.
4. THE AppShell SHALL NOT render the global admin-mode toggle button in the topbar.
5. THE AppShell SHALL NOT render the "מצב מנהל" badge in the topbar.
6. WHILE `adminGroupId` is not null, THE AppShell topbar SHALL apply an amber background (`#fffbeb`) and amber border to indicate that admin mode is active for some group.
7. THE AppShell SHALL continue to render the NotificationBell and the logout button in their existing positions.
8. THE existing `/admin/*` pages SHALL remain in the codebase and SHALL NOT be deleted or modified.

---

### Requirement 5: Seed Data UUID Randomization

**User Story:** As a developer, I want the seed data to use random-looking UUIDs instead of sequential fake ones, so that the development database more closely resembles production data and avoids accidental ID collisions.

#### Acceptance Criteria

1. THE seed.sql file SHALL replace all sequential fake UUIDs (e.g., `00000000-0000-0000-0000-000000000001`) with hardcoded random-looking UUIDs that are valid UUID v4 format.
2. THE seed.sql file SHALL maintain all existing foreign-key relationships — every reference to a replaced UUID SHALL be updated to the new UUID in the same file.
3. THE seed.sql file SHALL preserve all existing data rows, permissions, and relationships — no rows SHALL be added or removed.
4. THE seed.sql file SHALL remain idempotent — re-running it on an already-seeded database SHALL produce no errors (existing `ON CONFLICT DO NOTHING` / `ON CONFLICT DO UPDATE` clauses SHALL be preserved).
5. THE seed.sql file SHALL include a comment block at the top listing the mapping from old sequential UUIDs to new random UUIDs for developer reference.
