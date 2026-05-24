# 494 — TrialBanner Space-Level Subscription Update

## Phase

Space-Level Billing — Frontend

## Purpose

Migrates the `TrialBanner` component from per-group subscription fetching to per-space subscription fetching. The banner now uses the centralized `getSpaceSubscription(spaceId)` API function and handles the new `autoRenew` field for active subscription expiry warnings.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/billing/TrialBanner.tsx` | Rewrote component: removed `groupId` prop, fetches from space-level billing endpoint, added active+non-renewing expiry warning, updated visibility logic |
| `apps/web/app/groups/[groupId]/page.tsx` | Removed `groupId` prop from `<TrialBanner />` usage |

## Key decisions

- **No props needed**: The component reads `currentSpaceId` from the Zustand space store internally, so no props are required from parent components.
- **`daysRemaining` from API**: Instead of computing days remaining client-side from `trialEndsAt`, we use the `daysRemaining` field from the `SpaceSubscriptionDto` response (computed server-side). This ensures consistency with the backend's ceiling calculation.
- **Active + non-renewing expiry**: For active subscriptions that are not auto-renewing and within 7 days of expiry, we compute days from `currentPeriodEnd` client-side (since the API `daysRemaining` is trial-focused).
- **Fail silent**: On any API error, the banner renders nothing (Req 3.5).
- **Hebrew text preserved**: Kept the existing Hebrew UI strings for consistency with the rest of the app.

## How it connects

- Depends on `lib/api/billing.ts` (`getSpaceSubscription`) created in task 13.1
- Depends on `lib/store/spaceStore.ts` for `currentSpaceId`
- Consumed by the group detail page (`app/groups/[groupId]/page.tsx`)
- Backend endpoint: `GET /spaces/{spaceId}/billing/subscription` (task 10.1)

## How to run / verify

1. Navigate to any group page while logged in
2. With a trialing subscription: banner shows days remaining with correct color (sky >7d, amber 4-7d, red ≤3d)
3. With trial expired (0 days): banner shows upgrade prompt with red styling
4. With active + auto-renewing subscription: banner is hidden
5. With active + not auto-renewing + ≤7 days to expiry: banner shows expiry warning
6. With API failure (e.g., network error): banner is hidden

## What comes next

- Task 13.3: Property test for days remaining and color computation (Property 5)
- Task 14.1: SpaceBillingCard component on space settings page

## Git commit

```bash
git add -A && git commit -m "feat(billing): update TrialBanner for space-level subscription"
```
