# 265 — Home-Leave Controller Endpoints Overhaul

## Phase

Home-Leave Overhaul — API Endpoint Updates (Tasks 4.2–4.5)

## Purpose

Update the `HomeLeaveConfigController` to support the new three-mode home-leave system. This replaces the old request/response DTOs with mode-aware versions, adds an optimal-ratio endpoint, enriches the GET response with computed optimal values, and extends the preview endpoint with feasibility feedback.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/HomeLeaveConfigController.cs` | Rewrote controller: new `UpsertHomeLeaveConfigRequest` with Mode/BaseDays/HomeDays/SliderValue/LeaveDurationHours/LeaveCapacity/EmergencyFreezeActive/EmergencyUseForScheduling; new `GET optimal-ratio` endpoint; enriched GET response with optimal ratio fields; extended preview endpoint with feasibility result |
| `apps/api/Jobuler.Application/HomeLeave/Queries/GetHomeLeaveConfigQuery.cs` | Updated `HomeLeaveConfigDto` to include all new fields (Id, SpaceId, Mode, BaseDays, HomeDays, EmergencyFreezeActive, EmergencyUseForScheduling, FreezeStartedAt) |
| `apps/api/Jobuler.Application/HomeLeave/Queries/GetGroupMemberCountQuery.cs` | New query to retrieve group member count for optimal ratio and feasibility calculations |

## Key decisions

1. **Permission check in controller** — The PUT, GET, optimal-ratio, and preview endpoints all call `IPermissionService.RequirePermissionAsync` before dispatching commands, per security rules.
2. **Optimal ratio computed on read** — The GET endpoint computes the optimal ratio on every read (via `IOptimalRatioCalculator`) so the frontend always has fresh values without a separate call.
3. **Separate optimal-ratio endpoint** — Added for cases where the frontend needs just the optimal ratio (e.g., slider initialization) without the full config.
4. **Preview returns feasibility alongside solver result** — The preview endpoint now wraps both the solver preview response and the `FeasibilityResult` in a single response DTO.
5. **MinRestHours always 0** — The new PUT endpoint always passes `MinRestHours = 0` to the command since the new mode system handles rest implicitly via day-based ratios.
6. **Backward-compatible command** — The `UpsertHomeLeaveConfigCommand` still accepts all old fields; the controller translates the new request shape into the command's parameters.

## How it connects

- **Upstream**: `UpsertHomeLeaveConfigCommand` (task 4.1) handles the actual persistence and mode logic
- **Services**: `IOptimalRatioCalculator` and `IFeasibilityEngine` (tasks 2.1, 2.2) provide computation
- **Downstream**: Frontend components (tasks 8.x, 9.x) will call these endpoints
- **Validation**: `UpsertHomeLeaveConfigValidator` (already exists) validates the command-level fields

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Both `Jobuler.Api` and `Jobuler.Tests` projects build successfully with 0 errors.

## What comes next

- Task 4.6: Property test for day input validation
- Task 4.7: Property test for leave duration validation
- Task 6.1: SolverPayloadNormalizer mode-based payload construction

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): overhaul controller endpoints with mode system, optimal-ratio, and feasibility"
```
