# Step 152 — Enhanced ErrorBoundary with Branded Fallback

## Phase

Feature — Custom Error Pages

## Purpose

Upgrade the existing `ErrorBoundary` class component to use the shared `ErrorPageLayout`, support i18n via a functional `ErrorFallback` component, log errors with component stacks, and differentiate between dev/prod modes for error detail visibility.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/ErrorBoundary.tsx` | Upgraded class component with `componentDidCatch` logging, extracted `ErrorFallback` functional component using `useTranslations("errorPages")`, dev/prod mode differentiation, and branded layout via `ErrorPageLayout` |

## Key decisions

- **Functional ErrorFallback component** — Extracted fallback rendering to a separate functional component (`ErrorFallback`) in the same file so it can use the `useTranslations` hook, which class components cannot use directly.
- **Dev/prod differentiation** — In development mode, the error message is shown in a monospace code block. In production, no error details are exposed.
- **componentDidCatch logging** — Logs both the error object and the component stack trace to `console.error` for debugging.
- **Consistent styling** — Button and link styles match the 500 error page (`error.tsx`) for visual consistency across all error states.

## How it connects

- Uses `ErrorPageLayout` from `@/components/errors/ErrorPageLayout` (created in step 148)
- Uses `errorPages.clientError.*` i18n keys (added in step 147)
- Already wired into the app shell inside `NextIntlClientProvider` in `layout.tsx`, so translations are available in the fallback UI
- Maintains the same default export signature so no import changes are needed

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # Type check passes
```

Manually trigger a client-side error in dev mode to see the error message displayed. In production build, error details should be hidden.

## What comes next

- Task 5.2: Unit tests for the enhanced ErrorBoundary
- Task 8.1: Property test for no internal details leakage in production

## Git commit

```bash
git add -A && git commit -m "feat(error-pages): upgrade ErrorBoundary with branded fallback and i18n"
```
