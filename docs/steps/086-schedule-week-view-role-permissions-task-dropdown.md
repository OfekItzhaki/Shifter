# Step 086 — Schedule Week View, Role Permission Levels, Task Dropdown, Member Filter Fix

## Phase
Post-MVP Improvements

## Purpose
Four targeted improvements requested after the personal-and-role-constraints feature:
1. Simplify the schedule tab to week-only view (remove day/week toggle)
2. Add permission levels to group roles (view / view+edit / owner)
3. Replace the task_type_id text input with a task dropdown in constraint forms
4. Fix the personal constraints member list showing only 1 member

## What was built

### 1. Schedule tab — week-only view
**`apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx`** — complete rewrite:
- Removed the day/week view toggle entirely
- Now shows only the week view: 7 day-name tabs (Sun–Sat) + 2D table for the selected day
- Added week navigation (prev/next week buttons + "השבוע" button)
- Week range label shown (e.g. "12 ינואר – 18 ינואר")
- Today's tab highlighted in blue; today's dot indicator on non-selected today tab

### 2. Role permission levels
**Backend:**
- `Domain/Spaces/SpaceRole.cs` — added `RolePermissionLevel` enum (`View`, `ViewAndEdit`, `Owner`) and `PermissionLevel` property with default `View`
- `SpaceRole.CreateForGroup()` now accepts optional `permissionLevel` parameter
- `SpaceRole.Update()` now accepts optional `permissionLevel` parameter
- `Infrastructure/Configurations/SpaceConfiguration.cs` — maps `permission_level` column with snake_case conversion
- `Application/Groups/Commands/GroupRoleCommands.cs` — `CreateGroupRoleCommand` and `UpdateGroupRoleCommand` now include `PermissionLevel` field; handlers parse and apply it
- `Application/Groups/Queries/GetGroupRolesQuery.cs` — `GroupRoleDto` now includes `PermissionLevel` string
- `Api/Controllers/GroupRolesController.cs` — `GroupRoleRequest` now includes `PermissionLevel?`
- `infra/migrations/028_role_permission_level.sql` — adds `permission_level TEXT NOT NULL DEFAULT 'view'` with CHECK constraint

**Frontend:**
- `lib/api/groups.ts` — `GroupRoleDto` now includes `permissionLevel: "View" | "ViewAndEdit" | "Owner"`; `createGroupRole` and `updateGroupRole` payloads include `permissionLevel`
- `app/groups/[groupId]/tabs/SettingsTab.tsx` — role create/edit forms now include a permission level dropdown; role list shows a colored badge (צפייה / צפייה + עריכה / בעלים)
- `app/groups/[groupId]/page.tsx` — `handleCreateRole` and `handleUpdateRole` pass `permissionLevel` through

### 3. Task type dropdown in constraint forms
**`apps/web/components/ConstraintPayloadEditor.tsx`** — updated:
- Added `taskOptions?: TaskOption[]` prop
- When `ruleType === "no_task_type_restriction"` and `taskOptions` is provided, renders a `<select>` dropdown instead of a text input
- Falls back to text input when no task options are available

**`apps/web/app/groups/[groupId]/tabs/ConstraintsTab.tsx`** — updated:
- Added `taskOptions?: TaskOption[]` prop
- Passes `taskOptions` to all three `SectionCreateForm` instances and to the edit modal's `ConstraintPayloadEditor`

**`apps/web/app/groups/[groupId]/page.tsx`** — updated:
- Added `constraintTaskOptions` state
- Constraints `useEffect` now also calls `listGroupTasks` and builds task options
- Passes `taskOptions={constraintTaskOptions}` to `ConstraintsTab`

### 4. Personal constraints member filter fix
**Root cause:** Members added manually (without email/phone invite) get `invitationStatus = "pending"`. The filter `m.invitationStatus === "accepted"` excluded them from the personal constraint person selector.

**Backend fix:**
- `Application/Groups/Queries/GetGroupsQuery.cs` — `GroupMemberDto` now includes `LinkedUserId: Guid?` (optional, defaults to null for backward compat)
- `GetGroupMembersQueryHandler` now maps `p.LinkedUserId` into the DTO

**Frontend fix:**
- `lib/api/groups.ts` — `GroupMemberDto` now includes `linkedUserId: string | null`
- `app/groups/[groupId]/tabs/ConstraintsTab.tsx` — filter changed from `m.invitationStatus === "accepted"` to `m.linkedUserId != null || m.invitationStatus === "accepted"` — shows all members who have a linked user account OR have accepted an invitation

### 5. Optional tasks → mandatory
- `.kiro/specs/personal-and-role-constraints/tasks.md` — tasks 8.7, 8.8, 8.9 marked complete (they were already implemented as `[Theory]` parameterised tests in `PersonalAndRoleConstraintTests.cs`); `*` markers removed; notes updated

## Key decisions

### Week-only view
The day/week toggle added complexity without clear benefit — the week view with day tabs already gives per-day granularity. Removing the toggle simplifies the UI and the component state.

### Permission level as string in DB
Stored as `TEXT` with a CHECK constraint rather than a PostgreSQL enum. This avoids migration complexity when adding new levels later (just update the CHECK constraint).

### Task dropdown is opt-in
`ConstraintPayloadEditor` only shows the dropdown when `taskOptions` is provided. This keeps the component reusable in contexts where tasks aren't loaded.

### Member filter uses OR logic
`linkedUserId != null || invitationStatus === "accepted"` is more inclusive than the previous filter. The backend still enforces the real guard (HTTP 422 for unregistered persons), so showing more members in the UI is safe.

## How to run / verify

```bash
# Run migration
psql -U jobuler -d jobuler -f infra/migrations/028_role_permission_level.sql

# Build API
cd apps/api && dotnet build Jobuler.sln

# Frontend type check
cd apps/web && npx tsc --noEmit

# Manual verification:
# 1. Open a group → Settings → Roles → create a role with "צפייה + עריכה" level
#    → badge should show "צפייה + עריכה" in blue
# 2. Open Constraints tab → Personal section → "אילוץ אישי חדש"
#    → person selector should show ALL members with linked accounts, not just accepted ones
# 3. Select "הגבלת סוג משימה" rule type → task dropdown should appear with group tasks
# 4. Open Schedule tab → should show week view with day tabs, no day/week toggle
```

## Git commit

```bash
git add -A && git commit -m "feat: week-only schedule view, role permission levels, task dropdown, member filter fix"
```
