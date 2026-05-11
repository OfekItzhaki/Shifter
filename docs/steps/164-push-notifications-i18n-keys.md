# 164 — Push Notifications i18n Keys

## Phase

Push Notifications — Frontend UI

## Purpose

Add internationalization keys for the push notification settings UI in all three supported languages (English, Hebrew, Russian). These keys are consumed by the `PushNotificationSettings` component via `useTranslations("profile.push")`.

## What was built

| File | Change |
|------|--------|
| `apps/web/messages/en.json` | Added `profile.push` object with keys: title, enableLabel, enableDescription, notSupported, permissionDenied |
| `apps/web/messages/he.json` | Added corresponding Hebrew translations under `profile.push` |
| `apps/web/messages/ru.json` | Added corresponding Russian translations under `profile.push` |

## Key decisions

- Keys are nested under `profile.push` since the push settings live in the profile page, consistent with the existing `profile.notificationPrefs` namespace.
- Translations are human-quality, not machine-translated placeholders.

## How it connects

- The `PushNotificationSettings` component (`apps/web/components/PushNotificationSettings.tsx`) uses `useTranslations("profile.push")` and references these exact keys.
- Satisfies Requirements 7.2 (informational message when unsupported) and 7.3 (message when permission denied).

## How to run / verify

- Open the app in each locale and navigate to the profile page.
- Verify the push notification section displays correct text in English, Hebrew, and Russian.
- Validate JSON syntax: `ConvertFrom-Json (Get-Content -Raw apps/web/messages/en.json)` (repeat for he/ru).

## What comes next

- Integration of the PushNotificationSettings component into the profile page (task 8.2).

## Git commit

```bash
git add -A && git commit -m "feat(push): add i18n keys for push notifications (en, he, ru)"
```
