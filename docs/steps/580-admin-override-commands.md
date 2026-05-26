# 580 — Admin Override Commands

## Phase
Self-Service Scheduling — Application Layer

## Purpose
Implements admin manual override capabilities for self-service scheduling. Admins with `SchedulePublish` permission can assign members to shift slots (bypassing capacity and Max_Shifts constraints) or remove members from slots (triggering waitlist processing).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/Commands/AdminAssignShiftCommand.cs` | Command, validator, handler, and result DTO for admin-assigning a member to a shift slot |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Commands/AdminRemoveShiftCommand.cs` | Command, validator, handler, and result DTO for admin-removing a member from a shift slot |

## Key decisions

1. **Permission check in handler**: Both commands validate `SchedulePublish` permission via `IPermissionService.RequirePermissionAsync` before any business logic, following the existing pattern in `ChangeSchedulingModeCommand`.
2. **Admin assign bypasses constraints**: The assign handler skips capacity and Max_Shifts checks entirely (Req 10.1, 10.2). It creates a `ShiftRequest` with `IsAdminOverride = true` and immediately approves it.
3. **Admin remove triggers waitlist**: After cancelling the request with reason `admin_removed` and decrementing fill count, the handler calls `IWaitlistService.ProcessSlotReleasedAsync` to offer the freed slot to the next waitlisted member (Req 10.4).
4. **Group membership validation**: Both commands verify the target member belongs to the group via `GroupMemberships` (Req 10.8).
5. **Duplicate/existence checks**: Assign rejects if member already has an active request on the slot (Req 10.6). Remove rejects if no approved request exists (Req 10.7).

## How it connects
- Uses `IPermissionService` from `Jobuler.Application.Common`
- Uses `IWaitlistService` (already implemented in task 9.1) for slot release cascading
- Uses `ShiftRequest.Create()` and `ShiftSlot.IncrementFillCount()`/`DecrementFillCount()` domain methods
- Will be wired to API endpoints in task 14.8 (`POST admin assign`, `POST admin remove`)
- Property tests in task 10.2 validate Properties 29 and 30

## How to run / verify
```bash
cd apps/api
dotnet build --no-restore
```
Build succeeds with 0 errors.

## What comes next
- Task 10.2: Property tests for admin override (Properties 29, 30)
- Task 14.8: API controller endpoints for admin override

## Git commit
```bash
git add -A && git commit -m "feat(self-service): admin assign and remove shift commands"
```
