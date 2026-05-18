# 364 — Profile Error Page Localization

## Phase

User Timezone Settings — UI Polish

## Purpose

Replace raw English error messages ("Error loading profile") with a properly styled, localized error state that matches the app's design language. The previous implementation showed a plain red text string in English regardless of the user's locale, which was jarring on the Hebrew layout.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/profile/page.tsx` | Replaced raw `<p>` error with a centered, styled error card (icon + heading + description + retry button). Removed "Loading..." text. Replaced all hardcoded English error strings with `t()` calls. |
| `apps/web/messages/he.json` | Added `profile.loadError`, `profile.loadErrorDesc`, `profile.saveError`, `profile.deleteError`, `profile.retry` keys |
| `apps/web/messages/en.json` | Same keys in English |
| `apps/web/messages/ru.json` | Same keys in Russian |

## Key decisions

1. **Inline styled error state, not ErrorPageLayout** — The profile error is a data-loading failure within the app shell (user is authenticated), not a full-page error. Using the centered card pattern within `<AppShell>` keeps the nav visible so the user can navigate away.
2. **Retry = page reload** — Simplest recovery action. The `getMe()` call runs on mount, so a reload retries it.
3. **Removed "Loading..." text** — The spinner alone is sufficient and doesn't need localization.
4. **All error strings use `t()` now** — Including the save error fallback and delete account error.

## How it connects

- Uses existing `useTranslations("profile")` hook already in the component
- Follows the same visual pattern as the `ErrorPageLayout` component (centered, icon, heading, message, action)
- Matches the app's design tokens (colors, border-radius, font sizes)

## How to run / verify

1. Navigate to `/profile` while the API is unreachable
2. Should see a centered error card with Hebrew text (on Hebrew locale): "לא הצלחנו לטעון את הפרופיל" + description + retry button
3. No English text should appear

## What comes next

- Consider applying the same pattern to other pages with raw error strings (groups page message errors)

## Git commit

```bash
git add -A && git commit -m "fix(profile): replace raw English error with styled localized error state"
```
