# 293 — Publish Sandbox Endpoint

## Phase

Feature — Draft Simulation Sandbox

## Purpose

Adds the `POST /spaces/{spaceId}/groups/{groupId}/publish-sandbox` endpoint to `SimulationController`. This endpoint wires the existing `PublishSandboxCommand` via MediatR, validates the request body with FluentValidation, and returns 409 Conflict when the version is already published or discarded.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/SimulationController.cs` | Added `IMediator` and `IValidator<PublishSandboxRequest>` to constructor; added `PublishSandbox` action method that validates, dispatches command, and catches `InvalidOperationException` → re-throws as `ConflictException` (mapped to 409 by middleware) |

## Key decisions

- **Catch `InvalidOperationException` and re-throw as `ConflictException`**: The `PublishSandboxCommandHandler` throws `InvalidOperationException` for already-published/discarded versions. The global `ExceptionHandlingMiddleware` maps `InvalidOperationException` to 400, but the spec requires 409 for this case. Catching and re-throwing as `ConflictException` (which the middleware maps to 409) keeps the handler generic while giving the controller endpoint-specific HTTP semantics.
- **Permission check uses `SchedulePublish`**: Consistent with the existing `ScheduleVersionsController.Publish` endpoint and the command handler's own permission check (defense in depth).
- **Renamed `_validator` to `_simulateValidator`**: Avoids ambiguity now that two validators are injected.

## How it connects

- Depends on: `PublishSandboxCommand` (task 2.1), `PublishSandboxRequestValidator` (task 1.3), `ConflictException` (existing)
- Used by: Frontend publish flow (task 8.1) which calls `POST /publish-sandbox`

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with zero errors.

## What comes next

- Frontend publish flow (task 8.1) will call this endpoint
- Backend unit tests (task 11.2) will test the 409 behavior

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): add POST /publish-sandbox endpoint to SimulationController"
```
