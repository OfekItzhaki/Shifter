# 488 — GetSpaceSubscriptionQuery

## Phase

Phase: Space-Level Billing — Application Layer Queries

## Purpose

Provides a MediatR query to retrieve the current space subscription status, dates, tier, and computed fields (isActive, daysRemaining) for display on the Space Settings page and Trial Banner. Returns null when no subscription exists so the frontend can show a "no subscription" message.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Queries/GetSpaceSubscriptionQuery.cs` | Query record (`SpaceId`, `UserId`) and `SpaceSubscriptionDto` record with all response fields |
| `apps/api/Jobuler.Application/Billing/Queries/GetSpaceSubscriptionHandler.cs` | Handler that checks `SpaceView` permission, loads the subscription, and maps to DTO |

## Key decisions

- **Permission**: Uses `Permissions.SpaceView` (read-level) rather than `BillingManage` since viewing subscription status is a read operation available to any space member. The design doc says "Require SpaceView permission (or similar read permission)".
- **Null return**: Returns `null` when no subscription exists instead of throwing, allowing the frontend to handle the "no subscription" state gracefully (Requirement 4.5).
- **AsNoTracking**: Query uses `AsNoTracking()` since it's read-only, improving performance.
- **Computed fields**: `IsActive` maps from `IsAccessGranted` and `DaysRemaining` from the domain entity's computed property, keeping business logic in the domain layer.

## How it connects

- Called by `BillingController` via `GET /spaces/{spaceId}/billing/subscription` (Task 10.1)
- Used by the frontend `TrialBanner` and `SpaceBillingCard` components
- Depends on `SpaceSubscription` domain entity (Task 1.1) and EF configuration (Task 2.1)
- Follows the same pattern as `GetSpaceBillingAccessQuery` and `GetSubscriptionQuery`

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no new warnings.

## What comes next

- Task 10.1: Wire this query to the `BillingController` GET endpoint
- Task 13.1: Frontend API client calls this endpoint
- Task 13.2: TrialBanner uses the response
- Task 14.1: SpaceBillingCard uses the response

## Git commit

```bash
git add -A && git commit -m "feat(billing): add GetSpaceSubscriptionQuery and SpaceSubscriptionDto"
```
