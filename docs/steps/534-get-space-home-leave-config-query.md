# Get Space Home Leave Config Query

## Phase
Space Management — Application Layer Queries

## Purpose
Provides a MediatR query to retrieve the space-level home-leave configuration for a given space. Returns null if no configuration exists, allowing the frontend and other services to determine whether space-level home-leave settings have been configured.

## What was built
- `apps/api/Jobuler.Application/Spaces/Queries/GetSpaceHomeLeaveConfigQuery.cs` — Query record, DTO record, and handler
  - `GetSpaceHomeLeaveConfigQuery(Guid SpaceId)` — MediatR query accepting a space ID
  - `SpaceHomeLeaveConfigDto` — Record with all config fields (Mode, BalanceValue, BaseDays, HomeDays, MinPeopleAtBase, MinRestHours, EligibilityThresholdHours, LeaveCapacity, LeaveDurationHours, EmergencyFreezeActive, EmergencyUseForScheduling, FreezeStartedAt, PreFreezeMode)
  - `GetSpaceHomeLeaveConfigQueryHandler` — Loads config from `SpaceHomeLeaveConfigs` DbSet, returns null if not found

## Key decisions
- Returns `null` instead of throwing when no config exists — allows callers to distinguish "not configured" from "configured with defaults"
- Uses `AsNoTracking()` for read-only performance
- Follows the same pattern as `GetSpaceQuery` and `GetSpaceDetailQuery` (single file with query, DTO, and handler)
- No permission check in the query itself — permission enforcement happens at the controller/endpoint level per architecture rules

## How it connects
- Used by the `SpacesController` (task 11.3) to expose `GET /spaces/{spaceId}/home-leave-config`
- Used by the solver payload normalizer (task 10.1) to read space-level config
- Depends on `SpaceHomeLeaveConfig` entity (task 1.3) and its EF configuration (task 2.1)

## How to run / verify
```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

## What comes next
- Task 8.2: `GetSpacePermissionLevelsQuery` and handler
- Task 11.3: API endpoint wiring in `SpacesController`

## Git commit
```bash
git add -A && git commit -m "feat(space-management): add GetSpaceHomeLeaveConfigQuery and handler"
```
