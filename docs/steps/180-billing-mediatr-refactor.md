# 180 — Billing & Coupons MediatR Refactor

## Phase

Architecture compliance

## Purpose

The `BillingController` and `CouponsController` violated the architecture rule "Business logic in controllers — controllers dispatch commands/queries only." Both controllers directly injected `AppDbContext` and contained query/mutation logic inline. This step extracts all business logic into MediatR handlers, aligning these controllers with the pattern used by `GroupsController`.

## What was built

### New files (Application layer)

| File | Description |
|------|-------------|
| `Billing/Queries/GetSubscriptionQuery.cs` | Query record + DTO for fetching group subscription status |
| `Billing/Queries/GetSubscriptionHandler.cs` | Handler that queries `GroupSubscriptions` by space+group |
| `Billing/Queries/ValidateCouponQuery.cs` | Query record + result DTO for coupon validation |
| `Billing/Queries/ValidateCouponHandler.cs` | Handler that validates a coupon code |
| `Billing/Queries/ListCouponsQuery.cs` | Query record + DTO for listing all coupons |
| `Billing/Queries/ListCouponsHandler.cs` | Handler that lists coupons (platform admin only) |
| `Billing/Commands/CreateCouponCommand.cs` | Command record + result DTO for coupon creation |
| `Billing/Commands/CreateCouponHandler.cs` | Handler that creates a coupon (platform admin only) |
| `Billing/Commands/DeactivateCouponCommand.cs` | Command record for deactivating a coupon |
| `Billing/Commands/DeactivateCouponHandler.cs` | Handler that deactivates a coupon (platform admin only) |

### Modified files

| File | Description |
|------|-------------|
| `Controllers/BillingController.cs` | Rewritten to inject `IMediator` + `IPermissionService`, dispatches queries/commands only |

## Key decisions

- Used `AppDbContext` directly in handlers (same pattern as all other handlers in this project — no `IAppDbContext` interface exists).
- Platform admin authorization checks remain in the handlers (not the controller) since the handlers throw `UnauthorizedAccessException` which the `ExceptionHandlingMiddleware` maps to 403.
- The `CouponsController` only injects `IMediator` (no `IPermissionService`) because admin checks are done inside the handlers via user lookup.
- Kept both `BillingController` and `CouponsController` in the same file to match the original structure.

## How it connects

- Handlers follow the same pattern as `Groups/Commands/CreateGroupCommand.cs` and other existing handlers.
- The `ExceptionHandlingMiddleware` handles exception-to-HTTP-status mapping (`UnauthorizedAccessException` → 403, `KeyNotFoundException` → 404, `InvalidOperationException` → 400).
- MediatR auto-discovers handlers from the Application assembly (already registered in `Program.cs`).

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
```

Build should succeed with zero errors and zero warnings related to these files.

## What comes next

- Consider adding FluentValidation validators for `CreateCouponCommand` (input validation in Application layer per security rules).
- Other controllers with direct `AppDbContext` usage should be refactored similarly.

## Git commit

```bash
git add -A && git commit -m "refactor(billing): extract business logic into MediatR handlers"
```
