# 165 — Push Notification Settings Profile Integration

## Phase
Feature — Web Push Notifications

## Purpose
Integrate the `PushNotificationSettings` component into the user profile page so users can enable/disable push notifications from their settings. The component is conditionally rendered only when a space is selected, since push subscriptions are scoped per space.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/profile/page.tsx` | Added imports for `PushNotificationSettings` and `useSpaceStore`; read `currentSpaceId` from the space store; render `PushNotificationSettings` in a card section below `NotificationPreferences`, guarded by `currentSpaceId` availability |

## Key decisions
- The push settings card only renders when `currentSpaceId` is non-null, preventing errors when no space is selected
- Uses the same `cardStyle` wrapper as `NotificationPreferences` for visual consistency
- Reads space ID from the Zustand store (`useSpaceStore`) which persists the user's active space selection

## How it connects
- Depends on `PushNotificationSettings` component (step 164)
- Depends on `usePushSubscription` hook (step 163)
- Depends on `useSpaceStore` for the current space context
- Satisfies Requirement 6.2: the Push_Settings_UI displays the toggle in the correct state based on subscription status

## How to run / verify
1. Start the frontend dev server: `npm run dev` in `apps/web`
2. Log in and select a space
3. Navigate to the profile page
4. Verify the push notification settings card appears below the notification preferences card
5. If no space is selected, the push settings card should not appear

## What comes next
- End-to-end testing of the full push notification subscription flow from the profile page

## Git commit

```bash
git add -A && git commit -m "feat(push): integrate PushNotificationSettings into profile page"
```
