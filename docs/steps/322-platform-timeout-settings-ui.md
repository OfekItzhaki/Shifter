# Step 322 — Platform Timeout Settings UI

## Phase

Admin Session Timeout — Frontend Timeout Configuration UI

## Purpose

Adds a platform timeout setting to the platform settings page, allowing the super-admin to configure the inactivity timeout duration for Super Platform Mode sessions. The setting is fetched from `GET /platform/settings` and submitted via `PATCH /platform/settings` with client-side validation enforcing the [5, 120] minute range.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/platform/PlatformSettings.tsx` | New component with a number input for `platformTimeoutMinutes`, client-side validation, and save functionality |
| `apps/web/app/platform/page.tsx` | Imported and rendered `PlatformSettings` component below the CouponManager section |
| `apps/web/messages/en.json` | Added English i18n keys for platform settings UI |
| `apps/web/messages/he.json` | Added Hebrew i18n keys for platform settings UI |
| `apps/web/messages/ru.json` | Added Russian i18n keys for platform settings UI |

## Key decisions

- **Separate component**: Created `PlatformSettings` as a standalone component (like `CouponManager`) to keep the platform page clean and modular.
- **Client-side validation**: Validates range [5, 120] and integer requirement before submitting to the API, matching the backend constraints.
- **Consistent styling**: Uses the same inline style patterns as `CouponManager` (white card with border, 14px border-radius) for visual consistency on the platform page.
- **Accessible form**: Includes `aria-describedby`, `aria-invalid`, `role="alert"` for validation errors, and proper `<label>` association.
- **Optimistic UX**: Shows a "Saved ✓" confirmation for 3 seconds after successful save.

## How it connects

- Consumes the `GET /platform/settings` and `PATCH /platform/settings` endpoints created in task 4.4.
- The platform timeout value is also fetched in the platform page's re-auth gate to configure the inactivity timer duration (task 9.2).
- Validates: Requirements 4.3 (PATCH endpoint accessible to super-admin) and 4.5 (GET returns current value).

## How to run / verify

1. Log in as a super-admin and navigate to the platform page.
2. After re-authentication, scroll down past the stats and coupon sections.
3. The "Platform Settings" card should appear with the current timeout value (default 15).
4. Try entering values outside [5, 120] — validation errors should appear.
5. Enter a valid value and click Save — the value should persist on page reload.

## What comes next

- Task 11: Final checkpoint — ensure all tests pass.

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): add platform timeout settings UI"
```
