# Step 112 — Solver Start Time Fix

## Phase
Phase 4 — Scheduling Correctness

## Purpose
The auto-scheduler was generating draft schedules starting from the task's own `StartsAt` date (e.g. a task starting July 7 produced shifts from July 7) instead of from `DateTime.UtcNow`. Additionally, there was no way for admins to configure a custom start date for the auto-scheduler. This fix adds a `SolverStartDateTime` field to the `Group` entity and wires it through the full stack so the auto-scheduler uses the correct start time.

## What was built

### Backend
- **`apps/api/Jobuler.Domain/Groups/Group.cs`** — Added `SolverStartDateTime DateTime?` property. Updated `UpdateSettings` to accept an optional `solverStartDateTime` parameter.
- **`apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs`** — Added EF Core mapping for `solver_start_date_time` column (nullable, no default).
- **`infra/migrations/035_group_solver_start_datetime.sql`** — Adds `solver_start_date_time TIMESTAMPTZ` nullable column to the `groups` table.
- **`apps/api/Jobuler.Application/Groups/Commands/UpdateGroupSettingsCommand.cs`** — Added `DateTime? SolverStartDateTime = null` parameter. Handler now calls `group.UpdateSettings(req.SolverHorizonDays, req.SolverStartDateTime)`.
- **`apps/api/Jobuler.Api/Controllers/GroupsController.cs`** — Added `DateTime? SolverStartDateTime = null` to `UpdateGroupSettingsRequest`. Controller passes it through to the command.
- **`apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs`** — Added `DateTime? SolverStartDateTime` to `GroupDto`. Projection includes `g.SolverStartDateTime`.
- **`apps/api/Jobuler.Infrastructure/Scheduling/AutoSchedulerService.cs`** — **Core fix.** Projects `SolverStartDateTime` from DB. Passes `GroupId: groupId` and `StartTime: solverStartDateTime` to `TriggerSolverCommand`. Previously both were null/omitted.

### Frontend
- **`apps/web/lib/api/groups.ts`** — Added `solverStartDateTime?: string | null` to `GroupWithMemberCountDto`. Updated `updateGroupSettings` to accept and send the new field.
- **`apps/web/app/groups/[groupId]/useGroupPageState.ts`** — Added `solverStartDateTime` / `setSolverStartDateTime` state.
- **`apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx`** — Added `solverStartDateTime` prop and `onSolverStartDateTimeChange` handler. Added a `datetime-local` input in the Planning Horizon section. Clearing the field sets it back to null (use `DateTime.UtcNow`).
- **`apps/web/app/groups/[groupId]/page.tsx`** — Wired `solverStartDateTime` state through group load, `handleSaveSettings`, and `SettingsTab` render.
- **`apps/web/messages/en.json`** / **`apps/web/messages/he.json`** — Added `solverStartFrom` and `solverStartFromHint` translation keys.

## Key decisions
- **Null = use `DateTime.UtcNow`** — existing groups with no configured start time are fully backward compatible. The `SolverPayloadNormalizer` already handles `startTime = null` by defaulting to `DateTime.UtcNow`.
- **`AutoSchedulerService` now passes `GroupId`** — previously the auto-scheduler triggered a space-wide solver run with no group scope. It now correctly passes the group ID, enabling group-scoped member/task filtering in the normalizer.
- **No changes to `SolverPayloadNormalizer`** — it already handled a non-null `startTime` correctly. The bug was entirely in the auto-scheduler not passing the value.
- **Raw SQL migration** — this project uses raw SQL migrations in `infra/migrations/`, not EF migrations.

## How it connects
- `AutoSchedulerService` → `TriggerSolverCommand` → `SolverJobMessage` → `SolverPayloadNormalizer.BuildAsync` — the `StartTime` now flows correctly through this chain.
- The admin UI (Settings tab → Planning Horizon section) lets admins set a fixed start date for the auto-scheduler, which is persisted to `groups.solver_start_date_time` via `PATCH /spaces/{spaceId}/groups/{groupId}/settings`.

## How to run / verify
1. Run migration `035_group_solver_start_datetime.sql` against the database.
2. Restart the API.
3. Open a group's Settings tab → Planning Horizon section — you should see a new "Auto-scheduler start date" datetime input.
4. Leave it empty and run the solver — shifts should start from `DateTime.UtcNow`.
5. Set a future date (e.g. tomorrow) and save settings, then run the solver — shifts should start from that date.
6. Run tests: `dotnet test Jobuler.Tests --filter "AutoSchedulerBugConditionTests|AutoSchedulerServiceTests|SolverPayloadNormalizerTests"` — all 20 should pass.

## What comes next
- The `SolverStartDateTime` could be exposed in the admin panel as well.
- Consider adding a "clear" button or a "use current time" shortcut in the UI.

## Git commit
```bash
git add -A && git commit -m "fix(scheduling): solver start time uses group SolverStartDateTime via auto-scheduler"
```
