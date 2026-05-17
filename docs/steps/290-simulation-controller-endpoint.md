# 290 — Simulation Controller Endpoint

## Phase

Draft Simulation Sandbox — Backend

## Purpose

Adds the `POST /spaces/{spaceId}/groups/{groupId}/simulate` endpoint that accepts a complete `SolverInputDto` payload, validates it, checks permissions, calls the solver synchronously, and returns the result without creating any database records. This is the core backend component of the simulation sandbox feature.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/SimulationController.cs` | New controller with the `simulate` endpoint. Injects `IPermissionService`, `ISolverClient`, and `IValidator<SimulateRequest>`. Checks `ScheduleRecalculate` permission (space owners implicitly pass). Validates the request body then forwards the payload to the solver. |
| `apps/api/Jobuler.Application/Scheduling/Models/SimulateRequest.cs` | Request DTO record: `SimulateRequest(SolverInputDto Payload)` |
| `apps/api/Jobuler.Application/Scheduling/Validators/SimulateRequestValidator.cs` | FluentValidation validator ensuring required fields are present and headcounts are non-negative on all task slots. |

## Key decisions

1. **Synchronous solver call** — The controller calls `ISolverClient.SolveAsync` directly instead of going through the job queue. This is an intentional exception to the architecture rule because simulation is admin-only with low concurrency and requires immediate feedback.
2. **Permission: `ScheduleRecalculate`** — Reuses the existing permission that gates solver runs. Space owners implicitly have all permissions via `PermissionService`.
3. **Manual validation in controller** — Since this endpoint bypasses MediatR (no command/handler), the validator is injected directly and called before the solver. A `ValidationException` is thrown on failure, which the `ExceptionHandlingMiddleware` maps to 400.
4. **No database records** — The endpoint is purely stateless: validate → call solver → return result.

## How it connects

- Uses `ISolverClient` (implemented by `SolverHttpClient` in Infrastructure) to call the Python solver service.
- Uses `IPermissionService` for authorization (same pattern as `ScheduleRunsController`).
- The `SimulateRequest` validator is auto-registered by `AddValidatorsFromAssembly` in `Program.cs`.
- The `ExceptionHandlingMiddleware` handles `ValidationException` → 400 and `UnauthorizedAccessException` → 403.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The build should succeed with no errors on the new files.

## What comes next

- Task 1.2: Add `GET /solver-baseline` endpoint to the same controller.
- Task 1.3: Create DTOs for the publish sandbox request.

## Git commit

```bash
git add -A && git commit -m "feat(simulation): add SimulationController with POST /simulate endpoint"
```
