# 492 — Space-Level Billing Controller Endpoints

## Phase

Space-Level Billing — API Layer

## Purpose

Adds space-level billing endpoints to the existing `BillingController`, enabling the frontend to manage space subscriptions (get status, checkout, cancel, renew, upgrade) through a clean REST API. All endpoints dispatch MediatR commands/queries that were implemented in earlier tasks.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/BillingController.cs` | Added 5 new space-level endpoints and request DTO records |

### New endpoints

| Method | Route | Handler | Response |
|--------|-------|---------|----------|
| GET | `/spaces/{spaceId}/billing/subscription` | `GetSpaceSubscriptionQuery` | 200 with `SpaceSubscriptionDto` or null |
| POST | `/spaces/{spaceId}/billing/checkout` | `CreateSpaceCheckoutCommand` | 200 with `{ checkoutUrl }` |
| POST | `/spaces/{spaceId}/billing/cancel` | `CancelSpaceSubscriptionCommand` | 204 No Content |
| POST | `/spaces/{spaceId}/billing/renew` | `RenewSpaceSubscriptionCommand` | 204 No Content |
| POST | `/spaces/{spaceId}/billing/upgrade` | `UpgradeSpacePlanCommand` | 200 with `{ checkoutUrl }` |

### Request DTOs

- `CreateSpaceCheckoutRequest(string? VariantId)` — optional variant for checkout
- `UpgradeSpacePlanRequest(string VariantId)` — required variant for upgrade

## Key decisions

- **Permission checks in handlers, not controller**: Per architecture rules, the controller dispatches commands and the Application layer handlers call `IPermissionService.RequirePermissionAsync`. The controller stays thin.
- **Nullable body on checkout**: The checkout endpoint accepts an optional body with `variantId` to support both default and variant-specific checkouts.
- **204 for cancel/renew**: These are state-change operations with no meaningful response body, so 204 No Content is appropriate.
- **Legacy endpoints preserved**: Existing group-level endpoints remain unchanged for backward compatibility during migration.

## How it connects

- Dispatches to commands/queries created in tasks 5.1–5.4 and 7.1
- The `ExceptionHandlingMiddleware` maps `InvalidOperationException` → 400, `UnauthorizedAccessException` → 403, `KeyNotFoundException` → 404
- Frontend (task 13.1) will call these endpoints via the API client

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

All 5 endpoints are available under `/spaces/{spaceId}/billing/` when the API is running.

## What comes next

- Task 10.2: Migration endpoint and post-migration group billing rejection (410 Gone)
- Task 13.1: Frontend API client functions for these endpoints

## Git commit

```bash
git add -A && git commit -m "feat(billing): add space-level billing endpoints to BillingController"
```
