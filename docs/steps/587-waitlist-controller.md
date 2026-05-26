# Step 587 — Waitlist Controller

## Phase
Phase — Self-Service Scheduling (API Layer)

## Purpose
Exposes the waitlist functionality to the frontend via REST endpoints. Members can join a waitlist for full slots, accept timed offers, leave waitlists, and view their active waitlist entries. This controller wires the existing `IWaitlistService` to HTTP endpoints with proper authentication, tenant isolation, and person resolution.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/WaitlistController.cs` | REST controller with POST join, POST accept offer, DELETE leave, GET my entries |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Commands/JoinWaitlistCommand.cs` | MediatR command + handler wrapping `IWaitlistService.JoinWaitlistAsync` |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Commands/AcceptWaitlistOfferCommand.cs` | MediatR command + handler for accepting a waitlist offer with Max_Shifts validation |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Commands/LeaveWaitlistCommand.cs` | MediatR command + handler wrapping `IWaitlistService.LeaveWaitlistAsync` |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Queries/GetMyWaitlistEntriesQuery.cs` | MediatR query returning active waitlist entries with slot details |

## Key decisions

1. **Direct service injection for join/leave** — The controller calls `IWaitlistService` directly for join and leave operations (same pattern as `ShiftRequestsController`), while accept offer uses MediatR because it has more complex orchestration logic (Max_Shifts validation, shift request creation).

2. **Person resolution from authenticated user** — Uses the same `ResolvePersonIdAsync` pattern as other self-service controllers, mapping `LinkedUserId` to `PersonId` via the People table.

3. **SpaceView permission for all endpoints** — Members only need basic space access to manage their own waitlist entries. The service layer handles ownership enforcement (members can only manage their own entries).

4. **Accept offer validates Max_Shifts at acceptance time** — Per Req 9.5, if Max_Shifts is exceeded at acceptance time, the member is removed from the waitlist and the offer cascades to the next member.

5. **Route structure** — `spaces/{spaceId}/groups/{groupId}/waitlist` with sub-routes for accept (`/accept`) and leave (`/{shiftSlotId}`), plus GET mine (`/mine`).

## How it connects

- **WaitlistService** (task 9.1) — provides the core join/leave/cascade logic
- **ShiftRequestService** — the accept offer handler creates a ShiftRequest on successful acceptance
- **ShiftRequestsController** — follows the same controller pattern (person resolution, error responses)
- **ProcessExpiredWaitlistOffersJob** (task 16.2) — background job that expires timed-out offers

## How to run / verify

```bash
dotnet build apps/api/Jobuler.Api/Jobuler.Api.csproj
dotnet build apps/api/Jobuler.Tests/Jobuler.Tests.csproj
```

Both build with 0 errors and 0 warnings.

## What comes next

- Task 14.6: ShiftSwapsController
- Task 14.7: Scheduling mode endpoint on GroupsController
- Task 14.8: Admin override endpoints
- Task 16.2: ProcessExpiredWaitlistOffersJob (background job for offer expiry)

## Git commit

```bash
git add -A && git commit -m "feat(self-service): waitlist controller with join, accept, leave, and list endpoints"
```
