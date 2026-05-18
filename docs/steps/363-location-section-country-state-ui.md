# 363 — Location Section: Country/State Selection UI

## Phase

User Timezone Settings — Frontend Settings Page (Task 8.2)

## Purpose

Implements the interactive Country/State selection UI in the Settings page Location section. Users can select their country (and optionally state for multi-timezone countries) to determine their timezone. The resolved timezone is displayed as confirmation, and the selection is persisted via the API with immediate authStore update.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/data/countries.ts` | Static country and state data with localized names (en/he/ru), multi-timezone country set, and helper functions for name lookup |
| `apps/web/lib/api/userSettings.ts` | API module with `updateUserLocation` and `getUserSettings` functions wired to the backend endpoints |
| `apps/web/app/settings/page.tsx` | Replaced the placeholder LocationSection with full implementation: searchable Country dropdown, conditional State dropdown, resolved timezone display, and save button |

## Key decisions

1. **Custom SearchableDropdown component** — No existing dropdown/select component existed in the project. Built an inline component matching the project's style pattern (inline CSS, no external UI library).
2. **Localized country/state names** — Names provided in all 3 supported locales (en, he, ru) with locale-aware sorting via `localeCompare`.
3. **Multi-timezone country detection** — Uses a static `Set` matching the backend's `CountryTimezoneMap.MultiTimezoneCountries` to conditionally show the State dropdown.
4. **Immediate authStore update** — On successful save, `setTimezone` is called directly on the store, so all time displays update without requiring re-login (Requirement 4.5).
5. **State cleared on country change** — Prevents stale state codes from being sent to the API when the user switches countries (Requirement 7.4).
6. **Timezone displayed as read-only confirmation** — Shows the resolved IANA timezone ID after save, or the current store value before any selection (Requirement 7.5).

## How it connects

- **Backend**: Calls `PUT /api/user-settings/location` (implemented in task 4.1) which validates codes and returns the resolved timezone.
- **AuthStore**: Uses `setTimezone` action (implemented in task 6.1) to update the session timezone immediately.
- **CountryTimezoneMap**: Frontend data mirrors the backend static mapping — the 16 multi-timezone countries match exactly.
- **i18n**: Uses existing translation keys under `userSettings.location.*` (added in task 8.1).

## How to run / verify

1. Navigate to `/settings` in the app
2. The Location section should show a searchable Country dropdown
3. Select a multi-timezone country (e.g., US, AU, CA) — a State dropdown should appear
4. Select a single-timezone country (e.g., IL, FR) — no State dropdown, timezone resolves immediately on save
5. Change country — State selection should clear
6. Click "Save Location" — timezone confirmation text should update with the resolved IANA ID
7. Verify the timezone in the authStore updates (check localStorage `jobuler-auth` key)

## What comes next

- Task 8.3: Move existing settings sections from Profile page to Settings page
- Task 8.4: Write unit tests for Settings page components

## Git commit

```bash
git add -A && git commit -m "feat(settings): implement Country/State selection UI in Location section"
```
