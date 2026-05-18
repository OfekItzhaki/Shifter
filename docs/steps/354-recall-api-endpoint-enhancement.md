# 354 — Recall API Endpoint Enhancement

## Phase
Home Leave Protection — API Layer Updates

## Purpose
Update the HomeLeaveConfigController to expose a new recall endpoint that accepts enhanced parameters (Confirmed, Reason, ExpectedReturnAt) in the request body, and returns a travel-time warning when recalling a person with an active AtHome window. This enables the two-step confirmation UX: first request returns a warning, second request (with Confirmed=true) executes the recall.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/HomeLeaveConfigController.cs` | Added `POST .../recall` endpoint with `RecallHomeLeaveRequest` body DTO; returns `RecallWarningResponse` when unconfirmed, dispatches `CancelHomeLeaveCommand` when confirmed. Kept legacy DELETE endpoint for backward compatibility. Added `RecallHomeLeaveRequest` and `RecallWarningResponse` DTOs. |
| `apps/api/Jobuler.Application/HomeLeave/Queries/GetRecallWarningQuery.cs` | New MediatR query that checks whether the target presence window is currently active (in-progress) or future, and returns an appropriate warning message about travel time. |

## Key decisions

1. **Two-step flow via Confirmed flag**: When `Confirmed = false`, the endpoint returns a warning without executing the recall. When `Confirmed = true`, it dispatches the command. This keeps the controller thin while supporting the confirmation UX.
2. **Separate query for warning**: The warning logic lives in a dedicated `GetRecallWarningQuery` rather than in the controller, following the architecture rule that controllers dispatch commands/queries only.
3. **Legacy endpoint preserved**: The existing `[HttpDelete]` endpoint is kept for backward compatibility with existing clients.
4. **Permission check before warning**: The `SchedulePublish` permission is verified even for the warning step, so unauthorized users cannot probe window status.

## How it connects
- The new `POST .../recall` endpoint maps to the `CancelHomeLeaveCommand` (task 4) with all enhanced parameters.
- The `GetRecallWarningQuery` uses the same `PresenceWindows` table queried by the command handler.
- The `[Authorize]` attribute at class level ensures JWT authentication (security rules).
- The `IPermissionService.RequirePermissionAsync` call ensures authorization (architecture rules).

## How to run / verify
```bash
cd apps/api
dotnet build --no-restore
```
Build should succeed. The endpoint is available at:
- `POST /spaces/{spaceId}/home-leave-presence/{presenceWindowId}/recall`
  - Body: `{ "personId": "...", "confirmed": false }` → returns warning
  - Body: `{ "personId": "...", "confirmed": true, "reason": "...", "expectedReturnAt": "..." }` → executes recall

## What comes next
- Frontend integration to call the new recall endpoint with the two-step confirmation flow
- Final integration verification (task 12)

## Git commit
```bash
git add -A && git commit -m "feat(home-leave): add recall API endpoint with confirmation UX and travel-time warning"
```
