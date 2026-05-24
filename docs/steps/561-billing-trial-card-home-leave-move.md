# 561 — Billing Trial Card & Home Leave Config Move

## Phase
UI Polish & Feature Relocation

## Purpose
Two UI changes:
1. When no subscription exists (space is on free trial), show a trial info card with upgrade button instead of a generic "no subscription" message. Also handle the expired status with a clear upgrade CTA.
2. Move the `HomeLeaveConfigCard` from the space settings page to the group settings tab, where it's more contextually relevant for group admins.

## What was built

### Modified files
- `apps/web/components/billing/SpaceBillingCard.tsx` — Replaced the empty "no subscription" state with a trial card showing a "trialing" badge and "Upgrade Now" button. Added a dedicated expired state with upgrade messaging. Added `UpgradeButton` helper component.
- `apps/web/app/spaces/settings/page.tsx` — Removed `HomeLeaveConfigCard` import and JSX usage.
- `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` — Added `HomeLeaveConfigCard` import and placed it in the Advanced section after the `HomeLeaveConfigPanel`.
- `apps/web/messages/he.json` — Added `billing.trialPeriod`, `billing.trialExpired`, `billing.upgradeNow` keys.
- `apps/web/messages/en.json` — Added `billing.trialPeriod`, `billing.trialExpired`, `billing.upgradeNow` keys.
- `apps/web/messages/ru.json` — Added `billing.trialPeriod`, `billing.trialExpired`, `billing.upgradeNow` keys.

## Key decisions
- When `subscription === null`, the space is treated as being on a free trial (14 days). The card shows the sky-blue "trialing" badge and an upgrade button.
- When `subscription.status === "expired"`, the card shows a clear message that the trial ended with an upgrade CTA.
- The `UpgradeButton` is a self-contained component that handles its own loading/error state and calls `createSpaceCheckout`.
- `HomeLeaveConfigCard` uses `isAdmin` from the group settings context as the `isOwner` prop, since group admins should be able to manage space-level home leave config.

## How it connects
- The billing card uses the existing `createSpaceCheckout` API function from `@/lib/api/billing`.
- The `HomeLeaveConfigCard` continues to use the space-level API (`/spaces/{spaceId}/home-leave-config`) — it's just rendered in a different location.

## How to run / verify
1. Navigate to Space Settings → Billing card should show trial info when no subscription exists.
2. If subscription is expired, the card should show the expired message with upgrade button.
3. Navigate to Group Settings tab → `HomeLeaveConfigCard` should appear in the Advanced section.
4. Verify it no longer appears on the Space Settings page.

## What comes next
- Consider adding the space creation date to calculate actual days remaining in the trial.
- May want to add a visual countdown or progress bar for trial days.

## Git commit
```bash
git add -A && git commit -m "feat(ui): show trial info in billing card and move home-leave config to group settings"
```
