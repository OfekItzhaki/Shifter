# Step 109 — Roles Tab Extraction

## Phase
Phase 9 — Polish & Hardening

## Purpose
The group roles management UI was buried inside the Settings tab, making it hard to discover and mixing permission management with operational settings (rename, solver horizon, delete group). Roles are a distinct concept that deserves its own dedicated space — especially since they need explanation: they define permission levels, have a hierarchy, and each member can hold only one (unlike qualifications which are stackable).

## What was built

### `apps/web/app/groups/[groupId]/tabs/RolesTab.tsx` (new)
A dedicated admin-only tab for role management. Contains:

**Explainer banner** — blue info card that explains:
- What roles are (permission levels, not skill tags)
- The three-level hierarchy with descriptions:
  1. View only — read-only access to schedule, members, alerts
  2. View + Edit — can edit tasks, constraints, member details; cannot publish or manage roles
  3. Owner — full access including publish, role management, ownership transfer
- A note that each member can hold exactly one role (replacing the previous one on reassignment)

**Role definitions section** — create, edit, and deactivate roles. Default roles show a "default" badge and cannot be deactivated. Each role shows its permission level badge with colour coding (slate / blue / purple).

**Member role assignments section** — visible when at least one active role exists. Shows every member with a dropdown to assign/change their role. Owners are shown but their dropdown is disabled. Saving state is per-member (spinner on the row being updated). The permission level badge updates immediately after assignment.

### `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` (modified)
- Removed the entire "Roles management" `<Section>` block (~100 lines)
- Removed `groupRoles`, `groupRolesLoading`, `onCreateRole`, `onUpdateRole`, `onDeactivateRole` from the Props interface and destructuring
- Removed the `handleCreateRole` / `handleUpdateRole` local functions
- Removed the unused `GroupRoleDto` import
- Settings tab now contains only: rename, planning horizon, run schedule, ownership transfer, delete group

### `apps/web/app/groups/[groupId]/types.ts` (modified)
- Added `"roles"` to `ActiveTab` union type
- Added `"roles"` to `ADMIN_ONLY_TABS` (tab is hidden from non-admins)

### `apps/web/app/groups/[groupId]/page.tsx` (modified)
- Imported `RolesTab`
- Added `"roles"` to `ALL_TABS` array (between `qualifications` and `alerts`)
- Added `tabs.roles` to `getTabLabels()`
- Added a `useEffect` load trigger for `activeTab === "roles"` — loads roles + members if not already loaded
- Added the `{activeTab === "roles" && <RolesTab ... />}` render block
- Removed roles props from the `<SettingsTab>` call

### `apps/web/messages/en.json`, `he.json`, `ru.json` (modified)
- Added `"roles"` key to `groups.tabs`
- Added `groups.roles_tab` section with keys: `adminOnly`, `explainerTitle`, `explainerBody`, `singleRoleNote`, `definedRoles`, `memberRoles`, `memberRolesHint`, `default`, `noRole`

## Key decisions
- **Admin-only via `ADMIN_ONLY_TABS`**: The tab is filtered out of the tab bar for non-admins, consistent with how tasks, constraints, and stats are hidden.
- **Hierarchy shown inline**: Rather than linking to docs, the hierarchy is rendered directly in the explainer so admins understand the levels without leaving the page.
- **Single-role enforcement is UI-only**: The backend already enforces one role per member via `PATCH .../members/{personId}/role`. The UI reflects this by using a `<select>` (radio-style) rather than checkboxes.
- **`onUpdateMemberRole` passed from page.tsx**: Reuses the existing `handleUpdateMemberRole` handler already wired for the Members tab role dropdown — no new API calls needed.

## How to run / verify
1. Enter admin mode on any group
2. A "Roles" tab appears between Qualifications and Alerts
3. The tab shows the explainer banner with the 3-level hierarchy
4. Create a new role, assign it to a member — the badge updates immediately
5. Non-admins do not see the Roles tab
6. Settings tab no longer contains a roles section

## What comes next
- Continue with the schedule-table-autoschedule-role-constraints spec tasks

## Git commit

```bash
git add -A && git commit -m "feat(groups): extract roles into dedicated admin-only tab with hierarchy explainer"
```
