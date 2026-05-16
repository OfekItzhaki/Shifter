# 281 — Home-Leave Schedule Table & History Permission Setting

## Phase
Phase 5 — Home-Leave & Group Settings Enhancements

## Purpose
Two small features:
1. **Home-Leave Schedule Table** — A view showing who is going home and when, displayed in the schedule tab for closed-base groups.
2. **History Permission Setting** — A group setting that controls whether regular members can view past schedules.

## What was built

### Feature 1: Home-Leave Schedule Table

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Queries/GetHomeLeaveScheduleQuery.cs` | MediatR query that fetches AtHome presence windows for group members, ordered by start date, with status computation |
| `apps/api/Jobuler.Api/Controllers/HomeLeaveConfigController.cs` | Added `GET /spaces/{spaceId}/groups/{groupId}/home-leave-schedule` endpoint |
| `apps/web/lib/api/homeLeave.ts` | Added `getHomeLeaveSchedule` API client function and `HomeLeaveScheduleEntry` interface |
| `apps/web/components/home-leave/HomeLeaveScheduleTable.tsx` | Table component with status badges (active/upcoming/completed) |
| `apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx` | Integrated HomeLeaveScheduleTable below schedule tables for closed-base groups |

### Feature 2: History Permission Setting

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Groups/Group.cs` | Added `AllowMembersViewHistory` property and setter |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs` | EF Core mapping for the new column |
| `infra/migrations/057_allow_members_view_history.sql` | Database migration adding the column |
| `apps/api/Jobuler.Application/Groups/Commands/UpdateGroupSettingsCommand.cs` | Added `AllowMembersViewHistory` parameter |
| `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs` | Added field to `GroupDto` |
| `apps/api/Jobuler.Api/Controllers/GroupsController.cs` | Updated request DTO and handler call |
| `apps/web/lib/api/groups.ts` | Updated `GroupWithMemberCountDto` and `updateGroupSettings` |
| `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` | Added toggle UI |
| `apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx` | Hides prev-week navigation for non-admins when disabled |
| `apps/web/app/groups/[groupId]/page.tsx` | Wired new props and handler |

### i18n

| File | Description |
|------|-------------|
| `apps/web/messages/he.json` | Hebrew translations for both features |
| `apps/web/messages/en.json` | English translations for both features |
| `apps/web/messages/ru.json` | Russian translations for both features |

## Key decisions

- **Home-leave schedule query** shows windows from the last 7 days (completed) plus all upcoming, providing a useful recent+future view.
- **Status logic** is computed server-side: `active` (now between start/end), `upcoming` (starts in future), `completed` (ended in past).
- **History permission** is enforced client-side only (hiding the prev-week button) since the data isn't sensitive — it's just schedule assignments. The setting is stored in the `groups` table.
- The `AllowMembersViewHistory` toggle saves immediately via the existing `PATCH /settings` endpoint rather than requiring a separate save button.

## How it connects

- Feature 1 builds on the existing `PresenceWindow` entity and `GroupMembership` system.
- Feature 2 extends the existing group settings infrastructure (`UpdateGroupSettingsCommand`).
- Both features integrate into the existing ScheduleTab and SettingsTab components.

## How to run / verify

1. Run migration: `psql -f infra/migrations/057_allow_members_view_history.sql`
2. Build backend: `cd apps/api && dotnet build --no-restore -v q`
3. Type check frontend: `cd apps/web && npx tsc --noEmit`
4. Test Feature 1: Navigate to a closed-base group's schedule tab — the home-leave table should appear below the schedule.
5. Test Feature 2: In group settings, toggle "History viewing permission" off, then as a non-admin member verify the prev-week button is hidden when it would navigate to a past week.

## What comes next

- Could add server-side enforcement of history permission if data sensitivity requirements change.
- Could add filtering/pagination to the home-leave schedule table for large groups.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add home-leave schedule table and history permission setting"
```
