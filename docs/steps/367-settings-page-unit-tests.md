# 367 — Settings Page Unit Tests

## Phase

Phase 8 — Frontend Settings Page & UI Restructure

## Purpose

Validates the Settings page components through unit tests, ensuring the Country dropdown is searchable, the State dropdown conditionally appears for multi-timezone countries, country changes clear state selections, and the resolved timezone displays correctly. Also verifies all settings sections (Location, Time Format, Notifications, Push Notifications) are present on the Settings page.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/settings/settingsPage.test.tsx` | 16 unit tests covering Settings page component behavior |

## Key decisions

- **Mock strategy**: Mocked `next-intl`, `authStore`, `spaceStore`, `AppShell`, `NotificationPreferences`, `PushNotificationSettings`, and the `updateUserLocation` API to isolate component logic.
- **Translation mock**: Returns the translation key directly (e.g., `countryPlaceholder`) since the component uses scoped `useTranslations` calls. Tests assert against these keys.
- **Testing-library approach**: Uses `fireEvent` for user interactions (click, change) and role-based queries (`getByRole("combobox")`, `getByRole("option")`) for accessible element selection.
- **Follows existing patterns**: Mirrors the structure and conventions from `__tests__/session/activityPromptModal.test.tsx`.

## How it connects

- Tests the components built in tasks 8.1, 8.2, and 8.3 (Settings page layout, Country/State UI, section migration from Profile).
- Validates requirements 6.5, 7.1, 7.2, 7.3, 7.4 from the user-timezone-settings spec.
- Uses the `MULTI_TIMEZONE_COUNTRIES` set from `lib/data/countries.ts` indirectly through the component logic.

## How to run / verify

```bash
cd apps/web
npx vitest --run __tests__/settings/settingsPage.test.tsx
```

All 16 tests should pass:
- Settings sections presence (4 tests)
- Country dropdown (5 tests)
- State dropdown conditional display (3 tests)
- Country change clears state (2 tests)
- Resolved timezone display (2 tests)

## What comes next

- Task 9: Final checkpoint — ensure all tests pass across the project.

## Git commit

```bash
git add -A && git commit -m "test(phase8): settings page unit tests for timezone location UI"
```
