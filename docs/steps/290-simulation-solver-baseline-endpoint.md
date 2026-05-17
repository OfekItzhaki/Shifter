# 290 — Simulation Solver Baseline Endpoint

## Phase

Draft Simulation Sandbox — Backend

## Purpose

Provides a read-only API endpoint that returns the current `SolverInputDto` for a group. The frontend uses this as the starting point (baseline) when entering the simulation sandbox — all user overrides are applied on top of this payload.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/SimulationController.cs` | New controller with `GET /spaces/{spaceId}/groups/{groupId}/solver-baseline` endpoint. Uses `ISolverPayloadNormalizer.BuildAsync` to construct the payload and returns it as JSON. |

## Key decisions

- **Permission**: Uses `SpaceAdminMode` permission, consistent with other admin-level schedule endpoints (e.g. `ScheduleRunsController.GetRun`).
- **Trigger mode**: Passes `"simulation"` as the trigger mode to distinguish baseline fetches from real solver runs in logs.
- **No baseline version**: Passes `null` for `baselineVersionId` since the sandbox starts fresh without locked assignments.
- **Controller placement**: Created a dedicated `SimulationController` (rather than adding to `ScheduleRunsController`) to keep simulation concerns isolated. Future simulation endpoints (POST /simulate, POST /publish-sandbox) will be added here.

## How it connects

- Depends on `ISolverPayloadNormalizer` (Infrastructure layer) for payload construction.
- Depends on `IPermissionService` (Application layer) for authorization.
- Will be consumed by the frontend sandbox store (`enterSandbox` action) to initialize baseline state.
- Future tasks 1.1 and 1.3 will add the `POST /simulate` endpoint and DTOs to this same controller.

## How to run / verify

```bash
cd apps/api
dotnet build
# Endpoint: GET /spaces/{spaceId}/groups/{groupId}/solver-baseline
# Requires: valid JWT with SpaceAdminMode permission
```

## What comes next

- Task 1.1: Add `POST /simulate` endpoint to this controller
- Task 1.3: Create DTOs for simulation and publish requests
- Task 4.1: Frontend sandbox store will call this endpoint on sandbox entry

## Git commit

```bash
git add -A && git commit -m "feat(simulation): add GET solver-baseline endpoint for sandbox initialization"
```
