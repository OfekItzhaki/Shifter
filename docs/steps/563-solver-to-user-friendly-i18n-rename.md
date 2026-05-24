# 563 — Rename "solver" to user-friendly terms in translation files

## Phase

UX Polish

## Purpose

Replace all user-facing "סולבר" / "solver" / "решатель" references in the i18n translation files with more user-friendly terms. Users don't need to know about the internal "solver" engine — they should see terms like "schedule", "system", and "automatic scheduling" instead.

## What was built

| File | Changes |
|------|---------|
| `apps/web/messages/he.json` | Replaced ~20 user-facing strings: "סולבר" → "סידור" / "המערכת" / "אוטומטי" as appropriate |
| `apps/web/messages/en.json` | Replaced ~20 user-facing strings: "solver" → "schedule" / "the system" / "automatic" as appropriate |
| `apps/web/messages/ru.json` | Replaced ~16 user-facing strings: "решатель" → "расписание" / "система" / "автоматически" as appropriate |

## Key decisions

- **JSON keys unchanged** — only string values were modified. Keys like `runSolver`, `solverStarted`, etc. remain as-is since they are code references.
- **No .tsx/.cs/.py files modified** — this is a frontend-only i18n change.
- **Naming convention applied consistently:**
  - Manual activation button: "הפעל סידור" / "Run Schedule" / "Создать расписание"
  - Automatic scheduling toggle: "סידור אוטומטי" / "Automatic Scheduling" / "Автоматическое расписание"
  - Error messages referencing the engine: "המערכת" / "the system" / "система"
  - Planning horizon: removed "הסולבר" / "Solver" / "решателя" prefix
  - Source label: "אוטומטי" / "Automatic" / "Автоматически"

## How it connects

- These translation files are consumed by the Next.js frontend via `next-intl`
- The SettingsTab, admin panel, schedule views, notifications, and sandbox all reference these keys
- No code changes needed since only values changed

## How to run / verify

1. Run the app in each locale (he, en, ru) and verify:
   - Settings tab "Run Schedule" button text
   - Planning horizon label (no "solver" prefix)
   - Error messages use "system" / "המערכת" / "система"
   - Source column shows "Automatic" / "אוטומטי" / "Автоматически"
2. Verify JSON validity: all three files pass JSON parsing without errors

## What comes next

- No follow-up needed — this is a self-contained i18n polish step

## Git commit

```bash
git add -A && git commit -m "feat(i18n): rename solver to user-friendly terms in all locales"
```
