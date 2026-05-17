# 311 — Session Timeout Event Endpoint

## Phase
Admin Session Timeout — API Controller Endpoints

## Purpose
Adds the `POST /auth/session-timeout-event` endpoint so the frontend can notify the backend when an elevated mode session (management or platform) is terminated due to inactivity. This creates an audit trail for timeout events.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/AuthController.cs` | Added `SessionTimeoutEvent` action method and `SessionTimeoutEventRequest` record |

### Endpoint details
- **Route:** `POST /auth/session-timeout-event`
- **Auth:** `[Authorize]` (inherits controller-level `[EnableRateLimiting("auth")]`)
- **Body:** `{ spaceId?: Guid, mode: string }` where mode is `"management"` or `"platform"`
- **Dispatches:** `RecordSessionTimeoutCommand(CurrentUserId, SpaceId, Mode)`
- **Returns:** `204 NoContent`

## Key decisions
- Placed before the `DeleteAccount` endpoint to keep auth-related actions grouped together
- Uses the existing `CurrentUserId` pattern from the controller for user identification
- No additional permission check beyond `[Authorize]` — any authenticated user can report their own timeout event
- The `RecordSessionTimeoutCommand` handler (already implemented in task 3.1) handles audit logging

## How it connects
- Depends on `RecordSessionTimeoutCommand` (task 3.1) for the MediatR handler
- Called by the frontend `adminSessionStore` when elevated mode exits due to timeout (task 9.4)
- Satisfies Requirement 7.5: timeout events are recorded in the audit log

## How to run / verify
```bash
cd apps/api/Jobuler.Api
dotnet build
```
Build succeeds with no errors related to this change.

## What comes next
- Task 4.3: Extend group settings PATCH to include `managementTimeoutMinutes`
- Task 4.4: Add platform settings endpoints
- Task 9.4: Frontend wiring that calls this endpoint on timeout

## Git commit
```bash
git add -A && git commit -m "feat(admin-session-timeout): add POST /auth/session-timeout-event endpoint"
```
