# 361 — Settings Page Route and Layout

## Phase

User Timezone Settings — Frontend UI Restructure (Task 8.1)

## Purpose

Introduces a dedicated `/settings` route that consolidates user preferences (location, time format, notification preferences, push notifications) into a single page. This is the first step toward separating identity information (profile) from preference management (settings), as specified in Requirement 6.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/settings/page.tsx` | New Settings page with four sections: Location, Time Format, Notification Preferences, Push Notifications |
| `apps/web/components/shell/AppShell.tsx` | Added Settings nav link (gear icon) below My Profile in the sidebar |
| `apps/web/messages/en.json` | Added `nav.settings` and `userSettings.*` translation keys |
| `apps/web/messages/he.json` | Added `nav.settings` and `userSettings.*` translation keys (Hebrew) |
| `apps/web/messages/ru.json` | Added `nav.settings` and `userSettings.*` translation keys (Russian) |

## Key decisions

1. **Settings page follows existing profile page patterns** — Uses the same `cardStyle`, inline styles, and `AppShell` wrapper as the profile page for visual consistency.
2. **Sections are separate components** — Each section (Location, TimeFormat, Notification, Push) is its own function component for clarity and future extraction.
3. **Location section is a placeholder** — Displays the current timezone from `authStore` as read-only. The Country/State selection UI will be implemented in task 8.2.
4. **Time Format toggle duplicated from profile** — The same toggle UI is rendered in the Settings page. Task 8.3 will remove it from the profile page.
5. **Nav link placed after My Profile** — The gear icon for Settings sits directly below the profile link in the sidebar, making it easy to discover.
6. **RTL direction preserved** — The page uses `direction: "rtl"` matching the profile page pattern (primary user base is Hebrew-speaking).

## How it connects

- **Depends on**: `authStore` (timezoneId, timeFormat), `NotificationPreferences` component, `PushNotificationSettings` component, `spaceStore` (currentSpaceId for push)
- **Consumed by**: Task 8.2 (Country/State selection UI), Task 8.3 (moving sections from profile to settings)
- **Requirements**: 6.1 (accessible from main nav), 6.2 (contains time format, notifications, push), 6.3 (contains Location section)

## How to run / verify

1. Start the dev server: `cd apps/web && npm run dev`
2. Log in and verify the sidebar shows a "Settings" / "הגדרות" link with a gear icon below "My Profile"
3. Click the Settings link — navigates to `/settings`
4. Verify the page shows four sections: Location (with current timezone), Time Format (24h/12h toggle), Notification Preferences, Push Notifications
5. Verify the time format toggle works (clicking 24h/12h updates the selection)

## What comes next

- **Task 8.2**: Implement Country/State selection UI in the Location section
- **Task 8.3**: Move time format, notification preferences, and push notification sections from the Profile page to Settings (removing duplicates)

## Git commit

```bash
git add -A && git commit -m "feat(settings): add /settings page route and layout with nav link"
```
