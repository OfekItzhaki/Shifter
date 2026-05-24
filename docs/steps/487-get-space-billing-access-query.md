# 487 — GetSpaceBillingAccessQuery

## Phase

Space-Level Billing — Application Layer Queries

## Purpose

Provides an internal MediatR query that other services can use to check whether a group has premium billing access based on its parent space's subscription status. This is the single source of truth for billing access decisions across the platform.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Queries/GetSpaceBillingAccessQuery.cs` | MediatR query record accepting `SpaceId` and `GroupId` |
| `apps/api/Jobuler.Application/Billing/Queries/GetSpaceBillingAccessHandler.cs` | Handler that loads the `SpaceSubscription` for the space and returns `IsAccessGranted` |

## Key decisions

- **Returns `bool` directly** — no DTO wrapper needed since this is an internal query used by other services, not exposed via API.
- **No permission check** — this is an internal query consumed by other handlers/services to gate feature access, not a user-facing endpoint.
- **Returns `false` when no subscription exists** — no subscription means no access, which is the safe default.
- **GroupId accepted but not used in query** — included for future extensibility (e.g., per-group overrides) and to maintain a clear contract about what's being checked.

## How it connects

- Consumed by any service or handler that needs to verify billing access before allowing premium operations for a group.
- Relies on `SpaceSubscription.IsAccessGranted` computed property which encapsulates the access logic (active, trialing-not-expired, or canceled-within-grace-period).
- Uses `AppDbContext.SpaceSubscriptions` DbSet registered in task 2.1.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build should succeed with no errors in the Application project.

## What comes next

- API layer endpoints (task 10.1) will expose space billing status via REST.
- Other commands/handlers will use this query to gate premium feature access.

## Git commit

```bash
git add -A && git commit -m "feat(billing): add GetSpaceBillingAccessQuery and handler"
```
