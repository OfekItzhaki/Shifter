# 636 — Notification Event Type Translations

## Phase
Phase 7 — UX Polish & Localization

## Purpose
The `NotificationBell.tsx` component uses `t(`events.${n.eventType}.title`)` and `t(`events.${n.eventType}.body`)` from the `notifications` namespace before falling back to server-stored text. This step adds complete translations for all notification event types so the frontend can display localized notification titles and bodies.

## What was built

| File | Description |
|------|-------------|
| `apps/web/messages/en.json` | Added/replaced `notifications.events` with 20 event type translations (English) |
| `apps/web/messages/he.json` | Added/replaced `notifications.events` with 20 event type translations (Hebrew) |
| `apps/web/messages/ru.json` | Added `notifications.events` section with 20 event type translations (Russian) |

## Key decisions
- Preserved existing keys (`group_deleted`, `ownership_transfer`) that were already in the events section, alongside the new standardized keys.
- Used the exact event type strings as keys (including dots like `schedule.published`, `group.member_added`, `self_service.request_approved`) to match what the backend sends.
- Russian translations were added since the file already had a `notifications` section but was missing the `events` object.

## How it connects
- `NotificationBell.tsx` looks up `t(`events.${n.eventType}.title`)` and `t(`events.${n.eventType}.body`)` — these keys now resolve for all known event types.
- The backend notification service stores event types like `solver_completed`, `self_service.request_approved`, etc. — these match the JSON keys exactly.

## How to run / verify
```bash
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/en.json','utf8')); console.log('en.json: valid')"
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/he.json','utf8')); console.log('he.json: valid')"
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/ru.json','utf8')); console.log('ru.json: valid')"
```

## What comes next
- Any new notification event types added to the backend should have corresponding entries added to all three message files.

## Git commit
```bash
git add -A && git commit -m "feat(i18n): add notification event type translations for en/he/ru"
```
