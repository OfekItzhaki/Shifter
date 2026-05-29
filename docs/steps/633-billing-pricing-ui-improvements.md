# 633 — Billing & Pricing UI Improvements

## Phase
Phase 8 — UX Polish & Billing

## Purpose
Improve the subscription card design in space settings and add a "waiting for payment" state on the pricing page to provide better user feedback during the checkout flow.

## What was built

### Task 1: SpaceBillingCard improvements
- **`apps/web/components/billing/SpaceBillingCard.tsx`** — Redesigned the active subscription card:
  - Shows plan name (resolved from `tierId` via the plans API) with a green accent badge
  - Green-tinted background for active subscriptions (better visual distinction)
  - "Days remaining" shown in period info for active, trialing, and canceled states
  - Dates formatted with `Intl.DateTimeFormat` using the current locale (no more raw ISO strings)
  - Card last-4 digits shown when available from the subscription DTO
  - Highlight styling for low days remaining (amber color)
  - RTL-safe positioning (`end-6` instead of `right-6` for error toast)

### Task 2: Pricing page "waiting for payment" state
- **`apps/web/app/pricing/page.tsx`** — Added a payment waiting overlay:
  - After checkout opens in a new tab, shows a card with spinner and "Waiting for payment completion..." message
  - Displays the selected plan name
  - "Cancel" button stops polling and returns to plan selection
  - On successful subscription detection, shows "Payment successful!" with a green checkmark before redirecting
  - Proper cleanup of polling intervals on unmount
  - Migrated from inline styles to Tailwind CSS classes

### Translation updates
- **`apps/web/messages/en.json`** — Added: `waitingForPayment`, `selectedPlan`, `waitingHint`, `cancelWaiting`, `paymentSuccess`, `redirecting` (pricing); `daysRemainingValue`, `planLabel`, `paymentMethod` (billing)
- **`apps/web/messages/he.json`** — Same keys in Hebrew
- **`apps/web/messages/ru.json`** — Same keys in Russian

### Type update
- **`apps/web/lib/api/billing.ts`** — Added optional `cardLast4` field to `SpaceSubscriptionDto`

## Key decisions
- Plan name is resolved on the frontend by matching `tierId` against the plans list (no backend change needed)
- `cardLast4` is added as an optional field — the backend doesn't currently return it, but the UI is ready when it does
- Pricing page migrated from inline styles to Tailwind for consistency with the rest of the app
- Payment polling uses refs for interval/timeout to allow proper cleanup

## How it connects
- `SpaceBillingCard` is rendered in `apps/web/app/spaces/settings/page.tsx`
- The pricing page polls `getSpaceSubscription` and redirects to `/spaces/settings` on success
- Both components use the shared `billing.ts` API client and `next-intl` translations

## How to run / verify
1. Navigate to `/spaces/settings` with an active subscription — verify green accent, plan name badge, localized dates, and days remaining
2. Navigate to `/pricing`, select a plan — verify the "waiting for payment" overlay appears with spinner and plan name
3. Click "Cancel" on the overlay — verify it returns to plan selection
4. Switch locale to Hebrew — verify RTL layout and Hebrew translations

## What comes next
- Backend could add `cardLast4` to the subscription DTO (from LemonSqueezy customer data)
- Backend could add `planName` directly to the DTO to avoid the extra plans API call

## Git commit

```bash
git add -A && git commit -m "feat(billing): improve subscription card design and add payment waiting state"
```
