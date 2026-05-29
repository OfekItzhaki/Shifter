# 621 — Pick i18n Message Keys

## Phase

Feature: Shift Picker Lite

## Purpose

Add all user-visible strings for the `/pick` route to the i18n message files (Hebrew, English, Russian) under a dedicated `pick` namespace. This ensures no inline hardcoded display text exists in the picker components and all labels are resolved from next-intl message keys.

## What was built

| File | Description |
|------|-------------|
| `apps/web/messages/he.json` | Added `pick` namespace with Hebrew translations (title, selectGroup, noGroups, tabs.slots, tabs.myShifts, refresh, back, memberCount, error, retry) |
| `apps/web/messages/en.json` | Added `pick` namespace with English translations |
| `apps/web/messages/ru.json` | Added `pick` namespace with Russian translations |

## Key decisions

- Placed the `pick` namespace at the top level alongside existing namespaces (`selfService`, `groups`, etc.)
- Used `{count}` ICU placeholder in `memberCount` for dynamic member count interpolation
- Nested `tabs.slots` and `tabs.myShifts` under a `tabs` sub-object for logical grouping
- Hebrew values match the design document specification exactly

## How it connects

- All picker components (`PickerHeader`, `GroupSelector`, `PickerTabs`, `PickPage`) will resolve their labels from `pick.*` keys via `useTranslations("pick")`
- Error and retry strings are used by the `ErrorRetry` component when rendered within the picker context
- The `memberCount` key is used by `GroupSelector` to display group member counts

## How to run / verify

```bash
# Validate JSON syntax
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/he.json','utf8')).pick"
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/en.json','utf8')).pick"
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/ru.json','utf8')).pick"
```

## What comes next

- PickerHeader component (task 5.1) will consume `pick.refresh`, `pick.back`
- GroupSelector component (task 6.1) will consume `pick.selectGroup`, `pick.noGroups`, `pick.memberCount`
- PickerTabs component (task 7.1) will consume `pick.tabs.slots`, `pick.tabs.myShifts`

## Git commit

```bash
git add -A && git commit -m "feat(shift-picker-lite): add pick i18n message keys for he/en/ru"
```
