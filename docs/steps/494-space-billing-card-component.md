# Step 494 — SpaceBillingCard Component

## Phase

Phase: Space-Level Billing — Frontend

## Purpose

Adds a `SpaceBillingCard` component to the space settings page that displays subscription status and relevant date information. This fulfills Requirements 4.1–4.6 of the space-billing spec: showing subscription status, trial dates, period dates, cancellation/expiry dates, a "no subscription" message, and permission-gating the section to users with `BillingManage` permission.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/billing/SpaceBillingCard.tsx` | New component displaying subscription status badge and date details based on subscription state |
| `apps/web/app/spaces/settings/page.tsx` | Updated to import and render `SpaceBillingCard`, permission-gated via `space.isOwner` |

## Key decisions

- **Permission gate via `isOwner`**: The backend enforces `BillingManage` permission on all billing endpoints. In the frontend, space owners implicitly hold all permissions (per security rules), so `space.isOwner` from `SpaceDetailDto` is used as the client-side gate. The API will still reject unauthorized requests server-side.
- **Status badge colors**: trialing=sky/blue, active=green, past_due=amber, canceled=orange, expired=slate/gray — matching the design spec's color scheme.
- **Date format**: All dates displayed in YYYY-MM-DD format as required, using `toISOString().split("T")[0]` for reliable formatting.
- **Error state with retry**: On API failure, shows "Could not load billing information" with a Retry button, matching the design's frontend error handling spec.
- **No subscription state**: When `getSpaceSubscription` returns `null`, displays "No subscription found for this space."
- **Component placement**: Added after the Members section on the settings page, following the existing card layout pattern (rounded-2xl, shadow-sm, white/dark bg).

## How it connects

- Fetches data from `getSpaceSubscription(spaceId)` defined in `lib/api/billing.ts` (task 13.1)
- Calls `GET /spaces/{spaceId}/billing/subscription` which dispatches `GetSpaceSubscriptionQuery` (task 7.1, 10.1)
- Task 14.2 will add action buttons (Upgrade/Cancel/Renew) to this card
- Task 14.3 will add unit tests for this component

## How to run / verify

1. Start the frontend dev server: `npm run dev` in `apps/web`
2. Log in as a space owner
3. Navigate to Space Settings page
4. Verify the Subscription card appears with correct status badge and dates
5. Log in as a non-owner member — verify the card is hidden
6. Disconnect the API — verify error state with Retry button appears

## What comes next

- Task 14.2: Add action buttons (Upgrade, Cancel, Renew) to the SpaceBillingCard
- Task 14.3: Write unit tests for SpaceBillingCard

## Git commit

```bash
git add -A && git commit -m "feat(billing): add SpaceBillingCard component to space settings page"
```
