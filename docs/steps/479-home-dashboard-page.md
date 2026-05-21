# 479 — Home Dashboard Page

## Phase
UX — Navigation & Dashboard

## Purpose
Create a dedicated home/dashboard page as the app's landing page after login, and clean up the sidebar navigation by removing the changelog and onboarding restart items (which are now accessible from the home page).

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/home/page.tsx` | New home dashboard page with welcome section, "What's New" card, "Getting Started" tips, and quick action cards |
| `apps/web/components/shell/AppShell.tsx` | Updated sidebar: added "דף הבית" nav item at top, removed changelog and onboarding restart items, changed logo link from `/spaces` to `/home`, removed unused onboarding imports |
| `apps/web/app/login/page.tsx` | Changed default redirect from `/schedule/my-missions` to `/home` |
| `apps/web/lib/hooks/useSessionExitHandler.ts` | Changed timeout redirect from `/schedule/my-missions` to `/home` |
| `apps/web/messages/he.json` | Added `nav.home` key and full `home` translation section |
| `apps/web/messages/en.json` | Added `nav.home` key and full `home` translation section |
| `apps/web/messages/ru.json` | Added `nav.home` key and full `home` translation section |

## Key decisions
- The home page uses `useTranslations` for all text and `useAuthStore` for the display name
- The "Getting Started" section always shows (useful as a reference), with a "view full guide" button that triggers onboarding restart
- The changelog and onboarding restart were removed from the sidebar but remain accessible from the home page
- The `/changelog` page itself is untouched — only the sidebar link was removed
- Quick actions use card-based layout with hover effects, matching the app's design language (rounded-2xl, slate colors, blue accents)
- Dark mode is fully supported via Tailwind dark: classes
- The page is responsive (grid-cols-1 on mobile, grid-cols-2 on sm+)

## Sidebar order (final)
1. 🏠 דף הבית (`/home`)
2. 📋 המשימות שלי (`/schedule/my-missions`)
3. 👥 הקבוצות שלי (`/groups`)
4. --- divider ---
5. 👤 הפרופיל שלי (`/profile`)
6. ⚙️ הגדרות (`/settings`)
7. --- admin divider ---
8. 🔧 פלטפורמה (`/platform`) [admin only]

## How it connects
- The home page wraps content in `AppShell` like all other authenticated pages
- It links to `/changelog`, `/groups`, `/schedule/my-missions`, and triggers the onboarding flow
- Login and session timeout now redirect to `/home` instead of `/schedule/my-missions`
- The Shifter logo in the sidebar now links to `/home`

## How to run / verify
1. Start the dev server: `npm run dev` (from `apps/web`)
2. Log in — you should be redirected to `/home`
3. Verify the sidebar shows the new nav order (Home at top, no changelog or onboarding restart)
4. Verify the home page shows: welcome greeting, date, What's New card, Getting Started tips, Quick Actions
5. Click the Shifter logo — should navigate to `/home`
6. Test dark mode toggle — all cards should render correctly
7. Test mobile view — sidebar collapses, cards stack vertically

## What comes next
- Could add dynamic content to the "What's New" card (fetch from API or changelog data)
- Could personalize quick actions based on user's groups/activity

## Git commit
```bash
git add -A && git commit -m "feat(ux): home dashboard page and sidebar cleanup"
```
