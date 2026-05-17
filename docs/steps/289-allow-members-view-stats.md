# 289 — Allow Members View Stats Setting

## Phase
Feature — Group Settings

## Purpose
Adds a group-level toggle that controls whether non-admin members can view the statistics tab. By default, stats are hidden from regular members (only space owners and group owners can see them). Admins can enable the toggle to grant all members access.

## What was built

### Domain
- `apps/api/Jobuler.Domain/Groups/Group.cs` — Added `AllowMembersViewStats` property (default: `false`) and `SetAllowMembersViewStats(bool)` method.

### Database
- `infra/migrations/059_allow_members_view_stats.sql` — Adds `allow_members_view_stats BOOLEAN NOT NULL DEFAULT FALSE` column to `groups` table.

### EF Core
- `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs` — Maps `AllowMembersViewStats` to `allow_members_view_stats` column with default value `false`.

### Application Layer
- `apps/api/Jobuler.Application/Groups/Commands/UpdateGroupSettingsCommand.cs` — Added `AllowMembersViewStats` optional parameter to the command record and handler.
- `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs` — Added `AllowMembersViewStats` to `GroupDto` record and projection.

### API Layer
- `apps/api/Jobuler.Api/Controllers/GroupsController.cs` — Added `AllowMembersViewStats` to `UpdateGroupSettingsRequest` record and passes it to the command.
- `apps/api/Jobuler.Api/Controllers/StatsController.cs` — Added `RequireStatsAccessAsync` private method that checks: space owner → allow, group owner → allow, `AllowMembersViewStats == true` → allow, otherwise → 403. Applied to all stats endpoints that accept a `groupId`.

### Frontend
- `apps/web/lib/api/groups.ts` — Added `allowMembersViewStats` to the Group interface and `updateGroupSettings` function signature.
- `apps/web/app/groups/[groupId]/types.ts` — Removed `"stats"` from `ADMIN_ONLY_TABS` (now conditionally shown based on setting).
- `apps/web/app/groups/[groupId]/page.tsx` — Updated `visibleTabs` filter to hide stats tab for non-admins when `allowMembersViewStats` is false. Added `handleAllowMembersViewStatsChange` handler and passes new props to SettingsTab.
- `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` — Added `allowMembersViewStats` prop and toggle UI (identical pattern to `allowMembersViewHistory`).

### Translations
- `apps/web/messages/he.json` — Added `allowMembersViewStats` / `allowMembersViewStatsDesc`
- `apps/web/messages/en.json` — Added `allowMembersViewStats` / `allowMembersViewStatsDesc`
- `apps/web/messages/ru.json` — Added `allowMembersViewStats` / `allowMembersViewStatsDesc`

## Key decisions
- Follows the exact same pattern as `AllowMembersViewHistory` for consistency.
- Default is `false` — stats are hidden from regular members by default (more restrictive than history which defaults to `true`).
- Space owners and group owners always have access regardless of the setting (enforced server-side).
- The stats tab is removed from `ADMIN_ONLY_TABS` and instead conditionally filtered based on the group setting + admin status.
- Backend guard uses `RequireStatsAccessAsync` which checks person → group membership → ownership before falling back to the setting.

## How it connects
- Extends the group settings system alongside `AllowMembersViewHistory`, `AutoPublish`, `MinRestBetweenShiftsHours`.
- The stats tab visibility in the frontend is now data-driven rather than purely role-based.
- The backend guard protects all stats endpoints that accept a `groupId` parameter.

## How to run / verify
1. Run migration: `psql -f infra/migrations/059_allow_members_view_stats.sql`
2. Build API: `cd apps/api && dotnet build` (verified — succeeds)
3. As a group owner, open group settings → see the "Allow members to view statistics" toggle (default OFF)
4. As a regular member, verify the stats tab is hidden
5. Toggle ON as admin → regular members can now see the stats tab
6. Verify API returns 403 for stats endpoints when setting is OFF and user is not owner

## What comes next
- No immediate dependencies. This is a standalone group permission feature.

## Git commit
```bash
git add -A && git commit -m "feat(groups): add allow_members_view_stats group setting toggle"
```
