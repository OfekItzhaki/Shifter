# 477 — Remove In-App Coupon System

## Phase

Post-billing migration cleanup

## Purpose

LemonSqueezy now handles coupons externally, making the in-app coupon management system redundant. This step removes all coupon-related code from the API, Application, Domain, and Infrastructure layers to reduce maintenance surface and dead code.

## What was removed

| Layer | File / Change | Description |
|-------|--------------|-------------|
| API | `BillingController.cs` — `ValidateCoupon` endpoint | Removed the validate-coupon POST action |
| API | `BillingController.cs` — `CouponsController` class | Removed the entire platform/coupons controller (list, create, deactivate) |
| API | `BillingController.cs` — `ValidateCouponRequest`, `CreateCouponRequest` records | Removed request DTOs |
| Application | `Billing/Commands/CreateCouponCommand.cs` | Deleted |
| Application | `Billing/Commands/CreateCouponHandler.cs` | Deleted |
| Application | `Billing/Commands/DeactivateCouponCommand.cs` | Deleted |
| Application | `Billing/Commands/DeactivateCouponHandler.cs` | Deleted |
| Application | `Billing/Queries/ValidateCouponQuery.cs` | Deleted |
| Application | `Billing/Queries/ValidateCouponHandler.cs` | Deleted |
| Application | `Billing/Queries/ListCouponsQuery.cs` | Deleted |
| Application | `Billing/Queries/ListCouponsHandler.cs` | Deleted |
| Domain | `Billing/Coupon.cs` | Deleted entity |
| DbContext | `AppDbContext.cs` — `Coupons` DbSet | Removed |
| Infrastructure | `BillingConfiguration.cs` — `CouponConfiguration` class | Removed EF configuration |

## What was preserved

- The `coupons` database table (no drop migration created)
- `GroupSubscription.CouponCode` and `GroupSubscription.DiscountPercent` fields
- `GetSubscriptionQuery` and its handler (still used by BillingController)
- `using Jobuler.Application.Billing.Queries;` import (still needed for GetSubscriptionQuery)
- `using Jobuler.Domain.Billing;` in BillingConfiguration (still needed for GroupSubscription and WebhookEventLog)
- All frontend code (handled separately)

## Key decisions

- Left the database table intact to avoid data loss and unnecessary migrations
- Kept the `CouponCode`/`DiscountPercent` fields on `GroupSubscription` since they may still be referenced for historical subscription records
- Did not remove frontend coupon code per instructions (separate task)

## How it connects

- LemonSqueezy webhook handler and checkout flow remain unchanged
- Subscription management (cancel/renew/checkout) is unaffected
- Frontend `CouponManager.tsx` still exists but will be removed in a separate cleanup

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Note: If the API process is running, stop it first or the build will fail with file-lock errors on the output DLLs.

## What comes next

- Remove frontend coupon management UI (`CouponManager.tsx`)
- Optionally create a migration to drop the `coupons` table once confirmed no longer needed

## Git commit

```bash
git add -A && git commit -m "chore(billing): remove in-app coupon system (LemonSqueezy handles externally)"
```
