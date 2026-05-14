# 192 — Solver Payload Normalizer Home-Leave Config Extension

## Phase

Home-Leave Scheduling — API Backend (Solver Payload)

## Purpose

Extends the `SolverPayloadNormalizer.BuildAsync` method to load and include home-leave configuration in the solver payload for closed-base groups. This ensures the Python solver receives the necessary parameters to generate home-leave schedules.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Modified `BuildAsync` to: (1) load `IsClosedBase` alongside `SolverHorizonDays` when querying group data, (2) query `HomeLeaveConfigs` for closed-base groups, (3) build `HomeLeaveConfigDto` when config is complete, (4) log a warning when config is missing/incomplete, (5) pass the DTO to the `SolverInputDto` constructor. |

## Key decisions

- **Single query for group data**: Combined `SolverHorizonDays` and `IsClosedBase` into one anonymous projection to avoid an extra DB round-trip.
- **Completeness check**: All four config fields must be > 0 to be considered "populated". This guards against partially-saved configs reaching the solver.
- **Warning log with group ID**: Uses structured logging (`{GroupId}`) so operators can identify which group is misconfigured.
- **Null propagation**: When the group is not closed-base or config is incomplete, `homeLeaveConfigDto` stays `null` — the `SolverInputDto` record's optional parameter handles this cleanly.

## How it connects

- Depends on: Task 1.4 (`IsClosedBase` on Group entity), Task 1.5 (EF config for `HomeLeaveConfigs` DbSet), Task 5.1 (`HomeLeaveConfigDto` record).
- Consumed by: The Python solver which reads `home_leave_config` from the JSON payload to activate home-leave constraint generation.
- Related requirements: 7.3, 7.4, 7.5.

## How to run / verify

```bash
dotnet build apps/api/Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
```

Build succeeds with 0 warnings, 0 errors.

## What comes next

- Task 8.x: Solver-side implementation that reads `home_leave_config` and generates constraints.
- Task 10.3: Including published `at_home` presence windows in the solver payload.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): extend SolverPayloadNormalizer to include home-leave config"
```
