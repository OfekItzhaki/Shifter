# Step 586 — Shift Slots Controller

## Phase
Phase 14 — Self-Service Scheduling API Layer

## Purpose
Exposes shift slot availability and detail endpoints for group members in self-service scheduling mode. Members can query available slots for a cycle and view individual slot details, with a read-only flag indicating when the request window is closed.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/ShiftSlotsController.cs` | API controller with GET available slots and GET slot detail endpoints, using `[Authorize]` and permission checks |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Queries/ShiftSlotQueries.cs` | MediatR queries: `GetAvailableSlotsQuery` (delegates to `ISlotAvailabilityEngine`) and `GetShiftSlotDetailQuery` (returns single slot with read-only flag) |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Models/ShiftSlotDetailDto.cs` | DTO for detailed slot view including task name, capacity, fill count, status, and read-only flag |

## Key decisions

1. **Direct delegation to SlotAvailabilityEngine via MediatR**: The `GetAvailableSlotsQuery` handler resolves the person from the authenticated user and delegates to the existing `ISlotAvailabilityEngine`. This keeps the controller thin and the logic testable.
2. **Person resolution from user context**: The query handler resolves `personId` from `CurrentUserId` by looking up the `People` table where `LinkedUserId == userId`, following the established pattern in the codebase.
3. **Read-only flag on slot detail**: The `GetShiftSlotDetailQuery` checks the scheduling cycle's request window state and includes `IsReadOnly` in the response, satisfying requirement 7.5.
4. **SpaceView permission for read endpoints**: Members only need `SpaceView` permission to query slots, consistent with other read-only group data endpoints.
5. **Route structure**: `spaces/{spaceId}/groups/{groupId}/shift-slots/available?cycleId=...` for listing and `spaces/{spaceId}/groups/{groupId}/shift-slots/{slotId}` for detail, following the existing REST conventions.

## How it connects

- **SlotAvailabilityEngine** (Task 7.1): The available slots endpoint delegates entirely to this engine for filtering, sorting, and read-only flag logic.
- **ShiftRequestsController** (Task 14.4): Members will use the slot IDs returned here to submit shift requests.
- **SelfServiceConfigController** (Task 14.1): Configuration determines request window timing that affects the read-only flag.
- **ShiftTemplatesController** (Task 14.2): Templates generate the slots that this controller exposes.

## How to run / verify

```bash
cd apps/api
dotnet build
```

The build succeeds with no errors in the new files. The controller is wired via MediatR auto-registration.

## What comes next

- Task 14.4: ShiftRequestsController (POST submit request, POST cancel, GET my requests)
- Task 14.5: WaitlistController
- Task 16.x: Background jobs for slot generation and waitlist processing

## Git commit

```bash
git add -A && git commit -m "feat(phase14): shift slots controller with availability and detail endpoints"
```
