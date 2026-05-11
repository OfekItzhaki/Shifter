# 164 — Push Notification Settings Component

## Phase

Push Notifications — Frontend UI

## Purpose

Provides a user-facing toggle component for enabling/disabling push notifications per space. Handles all three states: unsupported browser, denied permission, and normal toggle operation with loading state.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/PushNotificationSettings.tsx` | React component rendering push notification toggle with graceful degradation |

## Key decisions

- Reused the same `ToggleSwitch` pattern from `NotificationPreferences.tsx` for visual consistency
- Added `disabled` prop support to the toggle for loading states (opacity + cursor change)
- Used `useTranslations("profile.push")` namespace — i18n keys already exist from task 8.3
- Three render paths: not supported → info message, permission denied → amber warning box, normal → toggle with description
- Component receives `spaceId` as prop to scope subscription to the active space

## How it connects

- Consumes `usePushSubscription` hook (task 7.1) for all push state management
- Uses i18n keys from `profile.push` namespace (task 8.3)
- Will be integrated into the profile page (task 8.2)
- Styled consistently with `NotificationPreferences` component

## How to run / verify

- Import and render `<PushNotificationSettings spaceId="..." />` in any page
- Verify three states: unsupported browser shows message, denied permission shows amber warning, supported shows toggle
- Toggle should be disabled (opacity 50%) during subscribe/unsubscribe operations

## What comes next

- Task 8.2: Integrate this component into the profile page

## Git commit

```bash
git add -A && git commit -m "feat(push): push notification settings toggle component"
```
