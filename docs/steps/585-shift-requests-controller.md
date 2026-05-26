# 585 — ShiftRequestsController

## Phase

Phase 4 — API Layer (Self-Service Scheduling, Task 14.4)

## Purpose

Exposes shift request submission, cancellation, and listing endpoints for group members. Members can submit requests for available shift slots, cancel previously approved requests, and view their own request history. The controller resolves the authenticated user's person ID from their linked account and delegates processing to the existing `IShiftRequestService`.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/ShiftRequestsController.cs` | REST controller with POST submit, POST cancel, and GET list-mine endpoints. Resolves member identity from JWT claims via `LinkedUserId` lookup. |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Queries/GetMyShiftRequestsQuery.cs` | MediatR query + handler that returns a member's shift requests with associated slot details (date, time, task name), sorted by date descending. |

## Key decisions

- **Direct service injection over MediatR commands for submit/cancel**: The `IShiftRequestService` already encapsulates the full transactional flow (advisory lock, validation, approval/rejection). Wrapping it in another MediatR command would add indirection without value. The GET endpoint uses MediatR for consistency with the query pattern.
- **Person resolution from `LinkedUserId`**: Members don't pass their own `personId` — it's resolved server-side from the authenticated user's JWT `NameIdentifier` claim mapped to the `People` table. This prevents members from acting on behalf of others.
- **Route: `spaces/{spaceId}/groups/{groupId}/shift-requests`**: Follows the existing resource hierarchy pattern. The `mine` sub-path for listing keeps it RESTful while scoping to the current user.
- **422 Unprocessable Entity for business rule violations**: Distinguishes from 400 (malformed request) and 404 (not found). The response includes the rejection reason and alternative slots when applicable.
- **No explicit permission check for members**: Any authenticated user with a linked person in the space can submit/cancel their own requests. The service layer enforces all business constraints (request window, capacity, max shifts, etc.).

## How it connects

- Wires `IShiftRequestService` (implemented in tasks 7.3 and 7.5) for submit and cancel operations
- Uses `GetMyShiftRequestsQuery` (new) for listing the member's requests
- Follows the same controller patterns as `SelfServiceConfigController` (task 14.1) and `ShiftTemplatesController` (task 14.2)
- Tenant isolation enforced via `TenantContextMiddleware` (sets `app.current_space_id` session variable)
- Person resolution ensures members can only act on their own requests

## How to run / verify

```bash
dotnet build --no-restore
dotnet test --no-build
```

The solution builds cleanly with no errors.

## What comes next

- Task 14.5: WaitlistController (join, accept offer, leave, list entries)
- Task 14.6: ShiftSwapsController (propose, accept, decline, cancel, list swaps)
- Task 16: Background jobs for slot generation, waitlist expiry, swap expiry

## Git commit

```bash
git add -A && git commit -m "feat(self-service): add ShiftRequestsController with submit, cancel, and list endpoints"
```
