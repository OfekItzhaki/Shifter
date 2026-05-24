# 562 — Dynamic Pricing Page

## Phase
Phase 8 — Billing & Monetization

## Purpose
Replace the hardcoded pricing page with a dynamic one that fetches real product variants from LemonSqueezy and creates checkouts with the correct variant ID. This enables the pricing page to stay in sync with LemonSqueezy dashboard changes without code deploys.

## What was built

### Backend (API)

| File | Description |
|------|-------------|
| `Jobuler.Application/Billing/PlanDto.cs` | New DTO record representing a subscription plan (variantId, name, priceInCents, interval, description, sortOrder) |
| `Jobuler.Application/Billing/ILemonSqueezyClient.cs` | Added `GetPlansAsync` method to the interface |
| `Jobuler.Infrastructure/Billing/LemonSqueezyClient.cs` | Implemented `GetPlansAsync` — fetches variants from LemonSqueezy API, filters for published subscriptions, caches results for 1 hour via `IMemoryCache` |
| `Jobuler.Api/Controllers/PlansController.cs` | New `[AllowAnonymous]` controller with `GET /billing/plans` endpoint |

### Frontend (Web)

| File | Description |
|------|-------------|
| `apps/web/lib/api/billing.ts` | Added `PlanDto` interface and `getPlans()` API function |
| `apps/web/app/pricing/page.tsx` | Rewrote pricing page to fetch plans dynamically, handle auth state, and create checkouts with the correct variant ID |
| `apps/web/messages/he.json` | Added Hebrew translations for loading, fetchError, checkoutError |
| `apps/web/messages/en.json` | Added English translations for loading, fetchError, checkoutError |
| `apps/web/messages/ru.json` | Added Russian translations for loading, fetchError, checkoutError |

## Key decisions

1. **Separate controller for public endpoint**: The existing `BillingController` is scoped to `spaces/{spaceId}/billing` and requires auth. Created a new `PlansController` at `/billing/plans` with `[AllowAnonymous]` to keep the pricing page public.

2. **1-hour in-memory cache**: Plans rarely change, so we cache the LemonSqueezy API response for 1 hour using `IMemoryCache` (already registered in the DI container). This avoids hitting the API on every page load.

3. **Fallback plans on error**: If the API is unreachable, the frontend falls back to hardcoded estimated plans (with empty variant IDs). These show a "coming soon" alert instead of creating a checkout.

4. **Auth-aware checkout flow**: 
   - Anonymous users → redirected to login with `?redirect=/pricing`
   - Logged-in users without a space → redirected to `/spaces`
   - Logged-in users with a space → checkout created with the selected variant ID

5. **JSON:API parsing**: LemonSqueezy returns JSON:API format. We parse it manually with `JsonDocument` to avoid adding a JSON:API library dependency, filtering for `status=published` and `is_subscription=true`.

6. **Popular plan badge**: Dynamically assigned to the middle plan in the list (index = floor(length/2)) rather than hardcoded to index 2.

## How it connects

- Uses the existing `ILemonSqueezyClient` / `LemonSqueezyClient` pattern (defined in Application, implemented in Infrastructure)
- The checkout flow calls the existing `createSpaceCheckout(spaceId, variantId)` which dispatches `CreateSpaceCheckoutCommand` — no changes needed there
- The `IMemoryCache` is already registered in DI (used by Redis caching layer and other services)
- Pricing page uses the existing `useAuthStore` and `useSpaceStore` for auth/space context

## How to run / verify

1. **Backend**: `dotnet build` in `apps/api` — should compile with 0 errors
2. **API test**: `GET http://localhost:5000/billing/plans` (no auth header needed) — should return a JSON array of plans from LemonSqueezy
3. **Frontend**: Navigate to `/pricing` — should show a loading state, then render plans fetched from the API
4. **Checkout flow**: Click "Select Plan" while logged in with a space — should redirect to LemonSqueezy checkout with the correct variant

## What comes next

- Add response caching headers (`Cache-Control`) on the endpoint for CDN caching
- Add plan tier limits (member count) as metadata in LemonSqueezy and display on the pricing page
- Add annual billing toggle if yearly variants are configured

## Git commit

```bash
git add -A && git commit -m "feat(billing): dynamic pricing page with LemonSqueezy variant fetch"
```
