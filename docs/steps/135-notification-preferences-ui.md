# 135 — Notification Preferences UI

## Phase
Phase 8 — UX & Mobile

## Purpose
Give users control over which notifications they see in the app. Admins get many solver-related notifications that can be noisy. This lets them mute categories they don't care about while keeping important ones visible.

## What was built

### Files created:

| File | Description |
|------|-------------|
| `apps/web/lib/store/notificationPrefsStore.ts` | Zustand store with localStorage persistence for notification preferences |
| `apps/web/components/NotificationPreferences.tsx` | Toggle UI component with category descriptions and badge control |

### Files modified:

| File | Change |
|------|--------|
| `apps/web/app/profile/page.tsx` | Added NotificationPreferences card below the info grid |
| `apps/web/components/shell/NotificationBell.tsx` | Filters notifications by enabled categories, respects badge visibility preference |
| `apps/web/messages/he.json` | Added Hebrew translations for all preference labels |
| `apps/web/messages/en.json` | Added English translations |
| `apps/web/messages/ru.json` | Added Russian translations |

## Key decisions

1. **Client-side preferences (localStorage)** — No backend changes needed. Preferences are per-device, stored via Zustand's `persist` middleware. This is appropriate because notification preferences are a personal UX choice, not a security boundary.

2. **Filter at display time** — Notifications are still fetched from the API (needed for badge count accuracy). Filtering happens in the NotificationBell component before rendering.

3. **Six categories** — Covers all current event types plus two future ones (`schedule_published`, `group_alert`) that will be added when those features ship.

4. **Toggle switches** — Clean, mobile-friendly toggle UI. Each category has an icon, name, and description.

5. **Badge visibility toggle** — Some users find the red badge distracting. They can disable it while still seeing notifications when they open the bell.

## Notification categories

| Category | Default | Description |
|----------|---------|-------------|
| `solver_completed` | ✅ On | Draft schedule ready for review |
| `solver_infeasible` | ✅ On | Schedule can't be created with current constraints |
| `solver_failed` | ✅ On | System error during scheduling |
| `solver_preflight_failed` | ✅ On | Pre-check identified issues |
| `schedule_published` | ✅ On | New schedule published (future) |
| `group_alert` | ✅ On | Admin sent a group alert (future) |

## How it connects
- Uses the existing `useNotifications` hook (step 021, 106)
- Integrates with the NotificationBell portal component (step 107)
- Preferences persist across sessions via localStorage
- Profile page already has the mobile-responsive layout from step 133

## How to run / verify
1. Navigate to Profile page
2. Scroll down to "Notification Preferences" card
3. Toggle off "Schedule completed" 
4. Open the notification bell — solver_completed notifications should be hidden
5. Toggle off "Show notification badge" — the red dot should disappear
6. Refresh the page — preferences persist

## What comes next
- Schedule diff view
- Landing page / marketing page

## Git commit

```bash
git add -A && git commit -m "feat(phase8): notification preferences UI — toggle categories, badge control, persisted in localStorage"
```
