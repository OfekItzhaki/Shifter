# Step 648 — RestDurationBadge Component

## Phase

Feature — Rest Duration Display

## Purpose

Provides a small, color-coded inline badge that shows the rest duration between a person's consecutive assignments. This is the presentational layer that combines the formatting utility, color classification utility, and i18n translations into a single reusable React component.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/schedule/RestDurationBadge.tsx` | Client component that renders a `<span>` with localized duration text, "rest" label suffix, and threshold-based color class |

## Key decisions

- **"use client" directive** — required because the component uses `useLocale()` and `useTranslations()` hooks from `next-intl`.
- **Locale cast to `SupportedLocale`** — `useLocale()` returns `string`; we cast to the union type expected by `formatRestDuration`. The app only supports en/he/ru so this is safe.
- **Styling** — `text-[10px] font-normal` keeps the badge visually subordinate to the primary assignment info (person name, task, time range) per requirement 3.2.
- **Separation of concerns** — the component only handles presentation; all computation (gap calculation, formatting, color logic) lives in the utility module.

## How it connects

- **Depends on**: `apps/web/lib/utils/restDuration.ts` (formatRestDuration, getRestColorClass, SupportedLocale)
- **Depends on**: i18n key `schedule.rest.label` in all three locale files (en, he, ru)
- **Used by**: `ScheduleTaskTable` (task 3.2) will render this badge below each person cell when admin mode is active and a rest entry exists

## How to run / verify

```bash
# Type-check the component
cd apps/web && npx tsc --noEmit --strict apps/web/components/schedule/RestDurationBadge.tsx
```

Or verify via the IDE — the file should show zero TypeScript errors.

## What comes next

- Task 3.2: Wire `RestDurationBadge` into `ScheduleTaskTable` with memoized rest duration computation
- Task 3.4: Unit tests for the badge rendering with various locales and threshold scenarios

## Git commit

```bash
git add -A && git commit -m "feat(rest-duration): add RestDurationBadge presentational component"
```
