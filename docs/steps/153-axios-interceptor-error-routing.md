# 153 — Axios Interceptor Error Routing

## Phase

Custom Error Pages — API Error Interception and Routing

## Purpose

Centralizes HTTP error handling in the axios response interceptor so that API failures (401, 403, 5xx) automatically redirect users to the appropriate branded error page. A module-level redirect guard prevents multiple concurrent API failures from triggering duplicate navigations.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/client.ts` | Updated interceptor with `isRedirecting` flag, `redirectToErrorPage()` helper, and status-code-based routing (401→unauthorized, 403→forbidden, 5xx→server-error, 404→no redirect) |
| `apps/web/app/error/server-error/page.tsx` | Dedicated route-based server error page for API 5xx responses, using `ErrorPageLayout` with serverError translations |

## Key decisions

- **Redirect guard pattern**: A module-level `isRedirecting` boolean prevents race conditions. Once set, all subsequent redirect attempts are no-ops. The flag is never reset — page navigation clears it naturally.
- **Dedicated `/error/server-error` route**: Rather than relying on Next.js `error.tsx` (which only catches rendering errors), a dedicated route handles API-triggered 5xx errors cleanly.
- **Promise always rejected**: After triggering a redirect, the interceptor still rejects the promise so calling code doesn't hang on an unresolved promise.
- **Status code evaluation order**: 401 (with refresh attempt) → 403 → 5xx → 404 (no redirect). This ensures a 403 during token refresh doesn't get swallowed.

## How it connects

- The interceptor redirects to `/error/unauthorized`, `/error/forbidden`, and `/error/server-error` — pages created in earlier tasks.
- The `?from=` query parameter enables "go back" functionality on the 403 page.
- The 404 case is intentionally left to page-level components for contextual handling.

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # Type-check
```

Manual verification:
1. Trigger a 403 API response → browser navigates to `/error/forbidden?from=/current-path`
2. Trigger a 500 API response → browser navigates to `/error/server-error?from=/current-path`
3. Trigger multiple concurrent 5xx responses → only one redirect occurs
4. Trigger a 404 API response → no redirect, error propagates to calling code

## What comes next

- Unit tests for the interceptor error routing (task 6.2)
- Property tests for `from` parameter correctness (task 6.3)
- Property tests for concurrent redirect idempotence (task 6.4)

## Git commit

```bash
git add -A && git commit -m "feat(error-pages): axios interceptor redirect guard and status code routing"
```
