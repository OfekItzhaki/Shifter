# 647 — Rest Duration i18n Translation Keys

## Phase

Feature — Rest Duration Display

## Purpose

Add internationalization translation keys for the rest duration display feature. These keys provide locale-aware labels and unit abbreviations (hours, days) used by the `RestDurationBadge` component and `formatRestDuration` utility.

## What was built

| File | Description |
|------|-------------|
| `apps/web/messages/en.json` | Added `schedule.rest.label`, `schedule.rest.hoursAbbrev`, `schedule.rest.daysAbbrev` with English values ("rest", "h", "d") |
| `apps/web/messages/he.json` | Added Hebrew translations ("מנוחה", "ש", "י") |
| `apps/web/messages/ru.json` | Added Russian translations ("отдых", "ч", "д") |

## Key decisions

- Keys are nested under the existing `schedule` namespace as `schedule.rest.*` to keep them co-located with other schedule-related translations.
- Abbreviations are single characters for compactness in the badge UI (e.g., "8h rest", "8ש מנוחה").
- The `label` key provides the full word for "rest" in each locale, used as a suffix in the badge display.

## How it connects

- `formatRestDuration` utility (step 646) uses `hoursAbbrev` and `daysAbbrev` for locale-aware formatting.
- `RestDurationBadge` component uses `schedule.rest.label` via `useTranslations("schedule")` for the label suffix.
- Supports all three application locales: English, Hebrew, Russian (Requirement 6.1, 6.2, 6.3).

## How to run / verify

```bash
# Validate JSON syntax
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/en.json','utf8')).schedule.rest"
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/he.json','utf8')).schedule.rest"
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/ru.json','utf8')).schedule.rest"
```

Each should output the rest object with label, hoursAbbrev, and daysAbbrev keys.

## What comes next

- `RestDurationBadge` component (task 3.1) will consume these keys via `next-intl`.
- Unit tests (task 3.4) will verify locale-specific output for all three languages.

## Git commit

```bash
git add -A && git commit -m "feat(i18n): add rest duration translation keys for en, he, ru"
```
