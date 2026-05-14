# 198 — Home-Leave Cancellation Logic

## Phase
Phase 10 — Integration: Publish Service & Presence Windows

## Purpose
Allows group admins to cancel a home-leave assignment (derived `at_home` presence window). Depending on timing, the window is either deleted entirely (future) or truncated to the current timestamp (in-progress).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/People/PresenceWindow.cs` | Added `Truncate(DateTime newEndsAt)` method to allow shortening an in-progress presence window |
| `apps/api/Jobuler.Application/HomeLeave/Commands/CancelHomeLeaveCommand.cs` | New MediatR command + handler: validates permission (`schedule.publish`), loads the derived AtHome window, deletes or truncates based on timing |
| `apps/api/Jobuler.Api/Controllers/HomeLeaveConfigController.cs` | Added `DELETE /spaces/{spaceId}/home-leave-presence/{presenceWindowId}?personId=...` endpoint |
| `apps/api/Jobuler.Tests/Domain/PresenceWindowTests.cs` | Added 3 unit tests for the `Truncate` method (valid truncation, before-starts rejection, after-ends rejection) |

## Key decisions

- **Endpoint route**: Used `DELETE /spaces/{spaceId}/home-leave-presence/{presenceWindowId}` with `personId` as a query parameter. This keeps the route clean and RESTful (deleting a presence resource).
- **Attribute routing override**: Used `[HttpDelete("~/spaces/{spaceId:guid}/home-leave-presence/{presenceWindowId:guid}")]` to break out of the controller's base route since the cancellation endpoint doesn't belong under the group-config path.
- **Truncate as domain method**: Added `Truncate()` directly on the `PresenceWindow` entity with validation guards, keeping business logic in the domain layer.
- **Past windows**: Attempting to cancel a window that has already ended throws `InvalidOperationException` (→ 400).

## How it connects
- Depends on the `PresenceWindow.CreateDerivedAtHome` factory (task 10.1) which creates the windows during publish.
- Uses `Permissions.SchedulePublish` for authorization, consistent with other schedule override operations.
- The cancelled/truncated window affects future solver runs (task 10.3) since the solver payload includes published `at_home` windows.

## How to run / verify
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~PresenceWindowTests"
dotnet build
```

## What comes next
- Frontend UI for cancelling home-leave assignments (task 12/13)
- Integration checkpoint (task 11)

## Git commit
```bash
git add -A && git commit -m "feat(home-leave): implement home-leave cancellation logic with truncate support"
```
