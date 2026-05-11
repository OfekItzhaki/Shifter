# 148 — ErrorPageLayout Shared Component

## Phase

Custom Error Pages — Foundation

## Purpose

All error pages (401, 403, 404, 500) and the ErrorBoundary fallback need a consistent visual layout. This step creates the shared `ErrorPageLayout` component that encapsulates the logo, heading, message, optional status code, and action slots — ensuring visual consistency, dark mode support, accessibility compliance, and responsive design across all error states.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/errors/ErrorPageLayout.tsx` | Shared layout component with centered content, ShifterLogo, optional status code numeral, heading, message, and action slot with touch targets and focus-visible outlines |

## Key decisions

- **Client component** — marked `"use client"` since it imports ShifterLogo (also a client component) and will be used inside client-side error pages.
- **Tailwind `dark:` variants** — uses `bg-white dark:bg-slate-900` for background, `text-gray-900 dark:text-gray-100` for headings, `text-gray-600 dark:text-gray-400` for messages, and `text-gray-300 dark:text-gray-700` for the muted status code numeral.
- **Touch targets via parent selector** — the actions container uses `[&>*]:min-h-[44px] [&>*]:min-w-[44px]` to enforce 44×44px minimum on all child interactive elements without requiring each page to repeat the classes.
- **Focus-visible outlines** — applied via parent selector `[&>*]:focus-visible:outline` for keyboard navigation accessibility.
- **Status code is `aria-hidden`** — the large numeral is decorative; screen readers skip it since the heading already conveys the error type.

## How it connects

- Used by all error pages: `not-found.tsx`, `error.tsx`, `app/error/unauthorized/page.tsx`, `app/error/forbidden/page.tsx`
- Used by the enhanced `ErrorBoundary` fallback UI
- Imports `ShifterLogo` from `@/components/shell/ShifterLogo`
- Receives already-translated strings (i18n is handled by the consuming page)

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

The component has no runtime side effects — verification is via TypeScript compilation and visual inspection when consumed by error pages.

## What comes next

- Task 1.3: Unit tests for ErrorPageLayout (ShifterLogo renders at size 40, heading as h1, status code conditional, children render, axe-core check)
- Tasks 3.1–3.4: Individual error pages that consume this layout

## Git commit

```bash
git add -A && git commit -m "feat(error-pages): create shared ErrorPageLayout component"
```
