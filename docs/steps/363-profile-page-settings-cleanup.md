# 363 — Move Settings Sections from Profile to Settings Page

## Phase

User Timezone Settings — Task 8.3

## Purpose

The new `/settings` page (task 8.1) consolidates user preferences that were previously scattered across the Profile page. This step removes the time format, notification preferences, and push notification sections from the Profile page so they only live on the Settings page. The Profile page is slimmed down to personal identity information only.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/profile/page.tsx` | Removed `TimeFormatToggle` component definition and its usage |
| `apps/web/app/profile/page.tsx` | Removed `NotificationPreferences` section (card wrapper + component) |
| `apps/web/app/profile/page.tsx` | Removed `PushNotificationSettings` section (conditional card wrapper + component) |
| `apps/web/app/profile/page.tsx` | Removed unused imports: `NotificationPreferences`, `PushNotificationSettings`, `useSpaceStore` |
| `apps/web/app/profile/page.tsx` | Removed `currentSpaceId` state variable (only used for push notifications) |

## Key decisions

- **No duplication** — The Settings page already has these sections implemented (task 8.1), so this is purely a removal from Profile.
- **Retained imports** — `useAuthStore` is still needed for `timezoneId` (used in BiometricSection and date formatting). Only imports exclusive to the removed sections were cleaned up.
- **Profile page now contains only**: display name, avatar, phone, email, birthday, member since, biometric login, data export, feedback, account deletion.

## How it connects

- Depends on task 8.1 (Settings page with all preference sections already implemented)
- The Settings page (`apps/web/app/settings/page.tsx`) now owns: Location, Time Format, Notification Preferences, Push Notifications
- The Profile page is now focused on identity and account management

## How to run / verify

1. Navigate to `/profile` — confirm no time format toggle, no notification preferences, no push notification settings are visible
2. Navigate to `/settings` — confirm all three sections are present and functional
3. Run `npx next build` to verify no TypeScript/build errors

## What comes next

- Task 8.4: Unit tests for Settings page components (verifying sections are present on Settings and removed from Profile)

## Git commit

```bash
git add -A && git commit -m "feat(settings): remove time format and notification sections from profile page"
```
