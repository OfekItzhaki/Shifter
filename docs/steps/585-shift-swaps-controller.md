# Step 585 — Shift Swaps Controller

## Phase

Phase 14 — API Layer (Self-Service Scheduling)

## Purpose

Exposes the shift swap functionality via REST endpoints so members can propose, accept, decline, and cancel shift swaps, as well as view their swap history. This controller wires the `IShiftSwapService` (implemented in task 11.1) to HTTP endpoints with proper authorization and tenant isolation.

## What was built

- **`apps/api/Jobuler.Api/Controllers/ShiftSwapsController.cs`** — New controller with 5 endpoints:
  - `POST /spaces/{spaceId}/groups/{groupId}/shift-swaps/propose` — Propose a swap between two approved shifts
  - `POST /spaces/{spaceId}/groups/{groupId}/shift-swaps/{swapRequestId}/accept` — Accept a pending swap (target only)
  - `POST /spaces/{spaceId}/groups/{groupId}/shift-swaps/{swapRequestId}/decline` — Decline a pending swap (target only)
  - `POST /spaces/{spaceId}/groups/{groupId}/shift-swaps/{swapRequestId}/cancel` — Cancel a pending swap (initiator only)
  - `GET /spaces/{spaceId}/groups/{groupId}/shift-swaps/my` — List all swaps where the current user is initiator or target

## Key decisions

1. **Direct service injection** — Uses `IShiftSwapService` directly rather than MediatR commands, since the service already encapsulates all business logic including validation, conflict detection, and atomic reassignment.
2. **Person resolution from JWT** — Resolves the current user's `PersonId` by querying `People` where `LinkedUserId == CurrentUserId` and `SpaceId` matches the route. Returns 403 if no linked person exists.
3. **Ownership enforcement in service layer** — The controller passes the resolved `personId` to the service, which enforces that only the initiator can propose/cancel and only the target can accept/decline (Requirements 12.8, 12.9).
4. **Tenant isolation** — All queries include `SpaceId` filter. The `TenantContextMiddleware` also sets PostgreSQL RLS session variables.
5. **Route structure** — Nested under `/spaces/{spaceId}/groups/{groupId}/shift-swaps` to maintain consistency with other self-service scheduling controllers and enforce group context.

## How it connects

- Depends on `IShiftSwapService` (task 11.1) for all business logic
- Depends on `AppDbContext` for person resolution and swap listing queries
- Uses the same auth/tenant patterns as other controllers (`[Authorize]`, `CurrentUserId`, `TenantContextMiddleware`)
- The `ExceptionHandlingMiddleware` handles `KeyNotFoundException` (404), `UnauthorizedAccessException` (403), and `InvalidOperationException` (400) thrown by the service

## How to run / verify

```bash
dotnet build apps/api/Jobuler.Api
```

The controller compiles cleanly. Full integration testing requires the database and auth infrastructure.

## What comes next

- Task 14.7: Add scheduling mode endpoint to GroupsController
- Task 14.8: Add admin override endpoints
- Task 16.3: ExpireSwapRequestsJob (background job for 72h expiry)
- Task 17.1: Notification integration for swap events

## Git commit

```bash
git add -A && git commit -m "feat(api): add ShiftSwapsController for shift swap management"
```
