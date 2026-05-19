# 417 — Reusable ErrorState Component & Polished Error Pages

## Phase

UX Polish

## Purpose

Replace bare unstyled error text across the app with a polished, reusable `ErrorState` component. Provides consistent error presentation with localized messages, contextual icons, retry actions, and dark mode support.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/shared/ErrorState.tsx` | New reusable error state component supporting 5 error types (network, not-found, permission, server, generic) with SVG illustrations, retry button, and home link |
| `apps/web/app/error.tsx` | Updated global error boundary to use new `errors` translations and polished button styles |
| `apps/web/app/not-found.tsx` | Updated 404 page to use new `errors` translations and polished button styles |
| `apps/web/components/errors/ErrorPageLayout.tsx` | Updated to use consistent slate color palette for dark mode |
| `apps/web/app/profile/page.tsx` | Replaced inline error state with `ErrorState` component |
| `apps/web/app/groups/[groupId]/tabs/StatsTab.tsx` | Replaced bare red `<p>` error with styled error card |
| `apps/web/messages/en.json` | Added `errors` translation key with all 5 error types + actions |
| `apps/web/messages/he.json` | Added Hebrew `errors` translations |
| `apps/web/messages/ru.json` | Added Russian `errors` translations |

## Key decisions

- **Placed in `components/shared/`** as requested, separate from the existing `components/errors/ErrorPageLayout.tsx` which is for full-page (standalone) errors. `ErrorState` is designed for in-page use within `AppShell`.
- **Kept existing `errorPages` translations** for backward compatibility — other pages (401, 403) still reference them. The new `errors` key is the canonical source going forward.
- **Subtle slate/blue SVG icons** instead of aggressive red — matches the app's clean design language.
- **`min-h-[50vh]`** for vertical centering within the available space (works inside AppShell without taking full screen).
- **Dark mode** via Tailwind `dark:` classes throughout.
- **Accessible** — buttons have min 44px touch targets, focus-visible outlines, and semantic HTML.

## How it connects

- `ErrorState` can be imported by any page that needs to show an error state inside `AppShell`.
- `ErrorPageLayout` remains the wrapper for full-page errors (error.tsx, not-found.tsx, 401, 403).
- Both now use the same `errors` translation namespace for consistency.

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit          # Should pass with 0 errors
npm run dev               # Visit /profile with network disconnected, or trigger a 404/500
```

## What comes next

- Migrate remaining bare error states in other tabs (alerts, messages) if desired
- Consider adding animated transitions to the error state

## Git commit

```bash
git add -A && git commit -m "feat(web): reusable ErrorState component and polished error pages"
```
