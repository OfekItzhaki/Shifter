# 517 — Schedule Regeneration API Endpoint

## Phase

Feature: Schedule Regeneration (Spec Task 5.1)

## Purpose

Adds the `POST /spaces/{spaceId}/schedule-runs/regenerate` endpoint to the API layer, allowing admins to trigger a schedule regeneration for a specific group. This is the HTTP entry point that dispatches the `TriggerRegenerationCommand` to the Application layer.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/ScheduleRunsController.cs` | Added `[HttpPost("regenerate")]` action method with permission check, MediatR dispatch, and 202 Accepted response. Added `RegenerateRequest` record DTO. |

## Key decisions

- **Follows existing pattern**: The new endpoint mirrors the existing `Trigger` action — permission check first, then MediatR dispatch, then return 202 with `{ runId }`.
- **Permission at API layer**: `ScheduleRecalculate` permission is verified before dispatching the command, consistent with architecture rules requiring permission checks in controllers before command dispatch.
- **Subscription/concurrency checks in handler**: Unlike the `Trigger` endpoint which checks subscription inline, the regeneration endpoint delegates subscription validation and concurrency guards to the `TriggerRegenerationCommandHandler`. This keeps the controller thin and the business logic in the Application layer.
- **Simple request DTO**: `RegenerateRequest(Guid GroupId)` — the start time is always "today in space timezone", resolved by the handler.

## How it connects

- **Upstream**: Called by the frontend `triggerRegeneration` API client function (Task 10.4)
- **Downstream**: Dispatches `TriggerRegenerationCommand` (Task 4.1) which handles subscription checks, concurrency guards, run creation, and job queue dispatch
- **Authorization**: Inherits `[Authorize]` from the controller class attribute; additionally requires `ScheduleRecalculate` permission
- **Polling**: Clients poll the existing `GET /schedule-runs/{runId}` endpoint to track progress

## How to run / verify

1. Build the API project:
   ```bash
   cd apps/api/Jobuler.Api && dotnet build
   ```
2. The endpoint is available at `POST /spaces/{spaceId}/schedule-runs/regenerate` with body `{ "groupId": "<guid>" }`
3. Expected response: `202 Accepted` with `{ "runId": "<guid>" }`
4. Without valid JWT: returns 401
5. Without `ScheduleRecalculate` permission: returns 403

## What comes next

- Task 5.2: Property test for permission enforcement (Property 8)
- Task 7.1: Worker handling of regeneration trigger mode
- Task 10.4: Frontend API client function calling this endpoint

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): add POST /schedule-runs/regenerate endpoint"
```
