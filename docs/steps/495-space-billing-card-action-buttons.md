# 495 — SpaceBillingCard Action Buttons

## Phase

Space-Level Billing — Frontend

## Purpose

Adds Upgrade, Cancel, and Renew action buttons to the `SpaceBillingCard` component so space admins can manage their subscription directly from the space settings page. Includes loading states, error toasts, and checkout redirect behavior.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/billing/SpaceBillingCard.tsx` | Added `ActionButtons` sub-component with Upgrade/Cancel/Renew buttons, loading states, and an inline `ErrorToast` component |

### Changes to `SpaceBillingCard.tsx`

- **Imports**: Added `createSpaceCheckout`, `cancelSpaceSubscription`, `renewSpaceSubscription` from the billing API module.
- **ActionButtons component**: Renders contextual buttons based on subscription status:
  - **Upgrade** — visible when `trialing` or `active`; calls `createSpaceCheckout` and redirects to the returned `checkoutUrl`.
  - **Cancel** — visible when `active` or `trialing`; calls `cancelSpaceSubscription` and refetches subscription data.
  - **Renew** — visible when `canceled` or `expired`; calls `renewSpaceSubscription` and refetches subscription data.
- **Loading states**: All buttons are disabled while any action is in progress; the active button shows "Loading…" text.
- **ErrorToast component**: A floating error notification (bottom-right, auto-dismisses after 5s) matching the project's existing toast pattern (red border, warning icon, dismiss button).

## Key decisions

- Used a single `loadingAction` state variable to track which action is in progress, disabling all buttons during any operation to prevent race conditions.
- Followed the project's existing toast pattern (floating bottom-right, auto-dismiss) rather than introducing a toast library.
- Cancel button uses `bg-red-500` to visually distinguish the destructive action from the primary sky-blue buttons.
- On checkout success, uses `window.location.href` for a full-page redirect to LemonSqueezy (external URL).
- On cancel/renew success, calls `onSubscriptionChange` (which triggers `fetchSubscription`) to refresh the displayed data.

## How it connects

- Consumes API functions from `lib/api/billing.ts` (task 13.1).
- Extends the `SpaceBillingCard` component created in task 14.1.
- Satisfies requirements 5.1 (checkout), 6.1 (cancel), 6.5 (renew), and 10.1 (upgrade).

## How to run / verify

1. Navigate to a space settings page as a user with `BillingManage` permission.
2. Verify button visibility matches subscription status:
   - Trialing → Upgrade + Cancel visible
   - Active → Upgrade + Cancel visible
   - Canceled → Renew visible
   - Expired → Renew visible
3. Click Upgrade → should show "Loading…" then redirect to LemonSqueezy checkout.
4. Click Cancel → should show "Loading…" then refresh to show "Canceled" status.
5. Click Renew → should show "Loading…" then refresh to show "Active" status.
6. Simulate API failure → error toast appears at bottom-right, auto-dismisses after 5s.

## What comes next

- Task 14.3: Unit tests for `SpaceBillingCard` (date display, permission gating, error state, action buttons).

## Git commit

```bash
git add -A && git commit -m "feat(billing): add action buttons to SpaceBillingCard"
```
