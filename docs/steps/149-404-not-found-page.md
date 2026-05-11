# 149 — 404 Not Found Page

## Phase

Custom Error Pages

## Purpose

Provides a branded, user-friendly 404 page that renders automatically when a user navigates to a non-existent route. Replaces the default Next.js 404 with a page that matches the application's visual identity and guides users back to a valid location.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/not-found.tsx` | Client component that renders the 404 error page using `ErrorPageLayout`, `useTranslations`, and a "Go Home" link styled as a button |

## Key decisions

- Made it a client component (`"use client"`) to use the `useTranslations` hook from next-intl
- Reuses the shared `ErrorPageLayout` component for consistent visual design across all error pages
- Button styling matches existing patterns (`bg-blue-600`, `rounded-lg`, `hover:bg-blue-700`) seen in `ErrorBoundary.tsx` and other components
- The "Go Home" link uses `min-h-[44px] min-w-[44px]` to meet WCAG 2.1 AA touch target requirements
- Focus-visible outline added for keyboard navigation accessibility

## How it connects

- Uses `ErrorPageLayout` from `@/components/errors/ErrorPageLayout` (created in step 148)
- Uses i18n keys from `errorPages.notFound` namespace (added in step 147)
- Next.js App Router automatically renders this file when `notFound()` is called or no route matches
- Part of the custom error pages feature alongside 401, 403, 500 pages and the enhanced ErrorBoundary

## How to run / verify

1. Start the dev server: `npm run dev` (in `apps/web/`)
2. Navigate to any non-existent route (e.g., `/this-does-not-exist`)
3. Verify the branded 404 page renders with the ShifterLogo, "Page Not Found" heading, descriptive message, and "Go Home" button
4. Verify the "Go Home" link navigates to `/`
5. Toggle dark mode and verify colors remain legible
6. Switch locale to Hebrew and verify RTL layout

## What comes next

- Task 3.2: 401 Unauthorized page
- Task 3.3: 403 Forbidden page
- Task 3.4: 500 Error page
- Task 3.5: Unit tests for all error pages

## Git commit

```bash
git add -A && git commit -m "feat(error-pages): add 404 Not Found page"
```
