# 369 — Admin Re-Auth Gate: Russian (ru) Locale Translations

## Phase

Feature — Admin Re-Authentication Gate (Localization)

## Purpose

The re-authentication dialog (`ReAuthDialog`) uses the `reAuth` translation namespace for all user-facing text. The Hebrew and English locale files already had this section, but the Russian locale was missing it. This step adds the complete `reAuth` section to `ru.json` so Russian-speaking users see properly translated text in the re-auth modal.

## What was built

| File | Description |
|------|-------------|
| `apps/web/messages/ru.json` | Added `reAuth` section with all 17 translation keys matching the English and Hebrew locale files |

### Keys added

- `title` — "Подтверждение личности"
- `description` — Security explanation prompt
- `close` — Close button label
- `passwordLabel` — Password field label
- `passwordPlaceholder` — Password input placeholder
- `confirm` — Submit/confirm button
- `verifying` — Loading state text
- `or` — Separator between auth methods
- `webAuthnButton` — Biometric button label
- `webAuthnLabel` — Biometric ARIA label
- `webAuthnCancelled` — WebAuthn cancellation error
- `authFailed` — Generic auth failure message
- `rateLimited` — Rate limit error
- `networkError` — Network error message
- `noCredentials` — No credentials configured warning
- `loadingCredentials` — Loading state
- `cancel` — Cancel button label

## Key decisions

- Translations follow the same tone and style as the existing Russian locale strings in the file (formal "вы" form, consistent terminology for "пароль", "аутентификация", etc.)
- Key names are identical across all three locales (he, en, ru) ensuring no missing translation warnings at runtime

## How it connects

- `ReAuthDialog` component uses `useTranslations("reAuth")` to render all text
- Correctness Property 7 in the design doc requires all locales to have complete translations
- Satisfies Requirements 7.1 and 7.2 (locale support for he, en, ru)

## How to run / verify

```bash
# Validate JSON syntax
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/ru.json','utf8')); console.log('OK')"

# Verify all keys match English
node -e "const en=JSON.parse(require('fs').readFileSync('apps/web/messages/en.json','utf8')); const ru=JSON.parse(require('fs').readFileSync('apps/web/messages/ru.json','utf8')); const missing=Object.keys(en.reAuth).filter(k=>!(k in ru.reAuth)); console.log(missing.length?'MISSING: '+missing.join(','):'All keys present')"
```

## What comes next

- Integration of the `ReAuthDialog` component with the group detail page (management mode trigger)
- Property-based tests for locale completeness (Property 7)

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth-gate): add Russian locale translations for reAuth namespace"
```
