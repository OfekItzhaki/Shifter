# 446 — Recommendation Card & Task Info Localization Keys

## Phase

Recommendation Approval Flow — Localization

## Purpose

Add localization keys for the new informational recommendation card and the task info popover to all three supported languages (English, Hebrew, Russian). These keys are consumed by the `RecommendationCard` and `TaskInfoPopover` components via `next-intl`.

## What was built

| File | Description |
|------|-------------|
| `apps/web/messages/en.json` | Added `recommendations.cardTitle`, `cardDescription`, `goToTasks`, `dismiss`, `slotsCount` and `schedule.taskInfoLabel`, `schedule.taskInfo.*` keys |
| `apps/web/messages/he.json` | Same keys with proper Hebrew (RTL) translations |
| `apps/web/messages/ru.json` | Same keys with proper Russian translations |

## Key decisions

- Added new keys to the existing `recommendations` section (which already had `bannerTitle`, `andMore`, etc.) rather than creating a separate namespace.
- Placed `taskInfoLabel` and `taskInfo` nested object inside the existing `schedule` section since the popover is part of the schedule grid.
- Used `{taskNames}` and `{count}` interpolation placeholders consistent with next-intl conventions.
- `allDay` is "24/7" in all languages since it's a universal notation.

## How it connects

- `RecommendationCard` component uses `useTranslations("recommendations")` to access `cardTitle`, `cardDescription`, `goToTasks`, `dismiss`, `slotsCount`.
- `TaskInfoPopover` component uses `useTranslations("schedule")` to access `taskInfoLabel` and `taskInfo.*` keys.
- Requirements 2.2, 2.3, 6.2 are satisfied by providing localized strings for all user-facing text.

## How to run / verify

```bash
# Validate JSON syntax
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/en.json','utf8')); console.log('OK')"
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/he.json','utf8')); console.log('OK')"
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/ru.json','utf8')); console.log('OK')"

# Verify keys exist
node -e "const j=JSON.parse(require('fs').readFileSync('apps/web/messages/en.json','utf8')); console.log(j.recommendations.cardTitle, j.schedule.taskInfoLabel, j.schedule.taskInfo.doubleShift)"
```

## What comes next

- Integration wiring (task 8) connects the schedule data fetching to pass `taskConfigurations` to `ScheduleTable2D`.
- Property tests and unit tests for the recommendation card and task info components.

## Git commit

```bash
git add -A && git commit -m "feat(i18n): add recommendation card and task info localization keys"
```
