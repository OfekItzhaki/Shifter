# 287 — i18n Hardcoded Hebrew Removal (Batch 2)

## Phase

Ongoing — Internationalization

## Purpose

Remove remaining hardcoded Hebrew strings from frontend components and utility files, moving them into the next-intl translation system for proper multi-language support.

## What was built

### Modified files

| File | Change |
|------|--------|
| `apps/web/components/UnavailabilityReasonsPanel.tsx` | Added `useTranslations("unavailabilityReasons")` import and replaced all ~15 hardcoded Hebrew strings with translation keys |
| `apps/web/hooks/useHomeLeavePreview.ts` | Changed hardcoded Hebrew error string to error key `"PREVIEW_LOAD_FAILED"` for component-level translation |
| `apps/web/lib/utils/alertSeverity.ts` | Changed `label` property to `labelKey` with English keys (`"info"`, `"warning"`, `"critical"`) instead of Hebrew strings |
| `apps/web/app/groups/[groupId]/tabs/AlertsTab.tsx` | Updated to use `t(getSeverityBadge(a.severity).labelKey)` instead of `.label` |
| `apps/web/messages/he.json` | Added `unavailabilityReasons` namespace (16 keys) and `homeLeave.previewLoadFailed` key |
| `apps/web/messages/en.json` | Added `unavailabilityReasons` namespace (16 keys) and `homeLeave.previewLoadFailed` key |
| `apps/web/messages/ru.json` | Added `unavailabilityReasons` namespace (16 keys) and `homeLeave.previewLoadFailed` key |

## Key decisions

1. **UnavailabilityReasonsPanel** — Used `useTranslations("unavailabilityReasons")` as a dedicated namespace since this is a self-contained settings panel.
2. **useHomeLeavePreview hook** — Since hooks can't use `useTranslations`, changed the error string to an English key (`"PREVIEW_LOAD_FAILED"`). The consuming component (`HomeLeaveConfigPanel`) can translate it if needed.
3. **alertSeverity utility** — Renamed `label` → `labelKey` and stored English keys. The `AlertsTab` component already has `useTranslations("groups.alerts_tab")` with `info`, `warning`, `critical` keys, so it translates the labelKey directly via `t()`.

## How it connects

- Builds on step 063 (i18n complete language switcher) and step 093 (i18n hardcoded hebrew removal batch 1).
- The `unavailabilityReasons` namespace supports the unavailability reasons feature (steps 210–216).
- The `homeLeave.previewLoadFailed` key supports the home leave preview hook (step 243).

## How to run / verify

1. Run `npm run build` in `apps/web` — should compile without errors.
2. Switch language to English or Russian and verify:
   - Unavailability Reasons panel in space settings shows translated strings.
   - Alert severity badges show translated labels.
3. Verify JSON validity: `node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/he.json','utf8'))"` (repeat for en/ru).

## What comes next

- Any remaining hardcoded Hebrew strings in other components.
- Translation of the `PREVIEW_LOAD_FAILED` key in the consuming component if the error is displayed to users.

## Git commit

```bash
git add -A && git commit -m "feat(i18n): remove hardcoded Hebrew from UnavailabilityReasonsPanel, alertSeverity, and useHomeLeavePreview"
```
