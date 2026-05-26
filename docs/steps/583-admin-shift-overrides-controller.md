# Step 583 — Admin Shift Overrides Controller

## Phase
Phase 4 — API Layer (Self-Service Scheduling)

## Purpose
Exposes admin override functionality via REST endpoints, allowing group admins with `SchedulePublish` permission to manually assign or remove members from self-service shift slots. This wires the existing `AdminAssignShiftCommand` and `AdminRemoveShiftCommand` (implemented in step 580) to HTTP POST endpoints.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/AdminShiftOverridesController.cs` | Controller with two POST endpoints: assign member to slot and remove member from slot. Requires `[Authorize]` and validates `SchedulePublish` permission before dispatching commands via MediatR. |

## Key decisions

- **Dedicated controller**: Created a separate `AdminShiftOverridesController` rather than adding to the existing `ScheduleOverridesController`, because the existing one handles solver-based schedule overrides (draft versions), while these endpoints are specific to self-service shift slot management.
- **Route structure**: `spaces/{spaceId}/groups/{groupId}/shift-slots/{shiftSlotId}/admin-overrides/{assign|remove}` — nests under the shift slot resource for clarity and RESTful hierarchy.
- **Double permission check**: The controller checks `SchedulePublish` permission before dispatching, and the command handler also validates it. This follows the defense-in-depth pattern used by other controllers (e.g., `ScheduleOverridesController`).
- **Error handling**: The commands throw domain exceptions (`KeyNotFoundException`, `InvalidOperationException`) which are caught by `ExceptionHandlingMiddleware` and mapped to appropriate HTTP status codes (404, 400). The `Success` flag on results handles expected business rejections.

## How it connects

- Depends on `AdminAssignShiftCommand` and `AdminRemoveShiftCommand` (step 580)
- Uses `IPermissionService` for authorization (existing infrastructure)
- Uses `IMediator` for command dispatch (existing pattern)
- Follows the same controller patterns as `ScheduleOverridesController` and `GroupsController`

## How to run / verify

```bash
dotnet build --no-restore
# From apps/api/Jobuler.Api
```

Endpoints:
- `POST /spaces/{spaceId}/groups/{groupId}/shift-slots/{shiftSlotId}/admin-overrides/assign` — body: `{ "personId": "..." }`
- `POST /spaces/{spaceId}/groups/{groupId}/shift-slots/{shiftSlotId}/admin-overrides/remove` — body: `{ "personId": "..." }`

## What comes next

- Other self-service controllers (tasks 14.1–14.7) for config, templates, slots, requests, waitlist, swaps, and scheduling mode
- Background jobs (task 16) for slot generation, waitlist expiry, swap expiry
- Notifications (task 17) for self-service lifecycle events

## Git commit

```bash
git add -A && git commit -m "feat(api): add admin shift override endpoints for self-service scheduling"
```
