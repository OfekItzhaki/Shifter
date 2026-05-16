# 272 — Minimum Rest Between Shifts as Group Setting

## Phase
Feature Enhancement — Solver Constraints

## Purpose
Moves the "minimum rest time between shifts" from being a manually-created hard constraint to a built-in group setting. This ensures the solver always enforces minimum rest between shifts without requiring admins to manually create a constraint. The setting defaults to 8 hours and can be configured per group (0–24 hours).

## What was built

### Backend (Domain)
- `apps/api/Jobuler.Domain/Groups/Group.cs` — Added `MinRestBetweenShiftsHours` property (default 8) and `SetMinRestBetweenShifts(int hours)` method with 0–24 validation.

### Database
- `infra/migrations/055_min_rest_between_shifts.sql` — Adds `min_rest_between_shifts_hours INTEGER NOT NULL DEFAULT 8` to `groups` table with CHECK constraint (0–24). Idempotent.

### EF Core
- `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs` — Maps `MinRestBetweenShiftsHours` to column `min_rest_between_shifts_hours`.

### Application Layer
- `apps/api/Jobuler.Application/Groups/Commands/UpdateGroupSettingsCommand.cs` — Added optional `MinRestBetweenShiftsHours` parameter; handler calls `SetMinRestBetweenShifts` when provided.
- `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs` — Added `MinRestBetweenShiftsHours` to `GroupDto`.

### API
- `apps/api/Jobuler.Api/Controllers/GroupsController.cs` — Added `MinRestBetweenShiftsHours` to `UpdateGroupSettingsRequest` DTO and passes it to the command.

### Solver Payload
- `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` — Reads `MinRestBetweenShiftsHours` from group data. When > 0 and group-scoped, adds a system hard constraint (`system_min_rest`, type `min_rest_between_assignments`) to the payload automatically.

### Frontend
- `apps/web/lib/api/groups.ts` — Added `minRestBetweenShiftsHours` to `GroupWithMemberCountDto` and `updateGroupSettings` function.
- `apps/web/app/groups/[groupId]/useGroupPageState.ts` — Added `minRestBetweenShiftsHours` state.
- `apps/web/app/groups/[groupId]/page.tsx` — Initializes state from API, passes to SettingsTab, includes in save call.
- `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` — Added number input (0–24) with label and warning when set to 0.
- `apps/web/messages/he.json` — Hebrew translations for the new setting.
- `apps/web/messages/en.json` — English translations.
- `apps/web/messages/ru.json` — Russian translations.

## Key decisions
- This is a GROUP-level setting, not a constraint the admin manually creates. The solver always receives it.
- If set to 0, no constraint is added (useful for restaurants/retail).
- Existing user-created "min rest" constraints still work — the solver handles duplicates gracefully.
- The constraint ID is `system_min_rest` to distinguish it from user-created constraints.
- Default is 8 hours, matching the common military/shift-work standard.

## How it connects
- The solver already supports `min_rest_between_assignments` as a constraint type — this just makes it automatic.
- The `HomeLeaveConfig.MinRestHours` is a different concept (rest after returning from home leave). This is rest between any two shifts.

## How to run / verify
1. Run migration: `psql -f infra/migrations/055_min_rest_between_shifts.sql`
2. Build: `dotnet build --no-restore -v q` (from `apps/api`)
3. TypeScript: `npx tsc --noEmit` (from `apps/web`)
4. In the UI: go to group settings → see "Minimum Rest Between Shifts" input
5. Save settings → verify the solver payload includes the `system_min_rest` hard constraint

## What comes next
- No follow-up required. The feature is self-contained.

## Git commit
```bash
git add -A && git commit -m "feat(solver): min rest between shifts as built-in group setting"
```
