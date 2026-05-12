# Step 181 — In-App Changelog ("What's New") Page

## Phase

Phase 8 — UX Polish & Feature Completeness

## Purpose

Provides users with a visible version history inside the app so they can see what features, improvements, and fixes have been shipped in each release without leaving the application.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/changelog/page.tsx` | New "What's New" page displaying hardcoded changelog entries in reverse chronological order with type badges (New, Fix, Improved) |
| `apps/web/messages/en.json` | Added `nav.changelog` and `changelog.*` i18n keys |
| `apps/web/messages/he.json` | Added `nav.changelog` and `changelog.*` i18n keys (Hebrew) |
| `apps/web/messages/ru.json` | Added `nav.changelog` and `changelog.*` i18n keys (Russian) |
| `apps/web/components/shell/AppShell.tsx` | Added NavItem for `/changelog` in the sidebar navigation |

## Key decisions

- **Hardcoded data** — Changelog entries live directly in the component. No API call needed; this keeps it simple and fast. Can be moved to a CMS or API later if needed.
- **Reverse chronological order** — Newest version first, matching user expectations.
- **Type badges** — Color-coded badges (green/new, red/fix, blue/improved) provide quick visual scanning.
- **Dark mode support** — All badge and card styles include dark mode variants via Tailwind.
- **Placement** — NavItem added before the "restart onboarding" button in the sidebar, making it easily discoverable but not intrusive.

## How it connects

- Uses `AppShell` for consistent layout and navigation.
- Uses `next-intl` for i18n, consistent with all other pages.
- Follows the same card styling pattern as the profile page (white card, rounded-2xl, border).

## How to run / verify

1. Start the dev server: `cd apps/web && npm run dev`
2. Navigate to `/changelog`
3. Verify the page shows all 6 version entries with correct badges
4. Toggle dark mode — cards and badges should adapt
5. Switch language to Hebrew/Russian — title and badge labels should translate
6. Check the sidebar — "What's New" link should appear before the setup guide button

## What comes next

- Optionally move changelog data to a JSON file or CMS for easier updates
- Add a "new" indicator dot on the nav item when there's a version the user hasn't seen

## Git commit

```bash
git add -A && git commit -m "feat(ux): in-app changelog page with i18n and nav link"
```
