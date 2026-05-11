# 150 — 500 Internal Server Error Page

## Phase
Custom Error Pages — Error Page Implementation

## Purpose
Provides a branded, user-friendly error page when an unhandled server-side exception occurs during page rendering. This is the Next.js App Router `error.tsx` convention file that catches runtime errors and displays a recovery UI instead of a blank screen or raw error.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/error.tsx` | Client component that renders the 500 error page using `ErrorPageLayout`, with "Try Again" (calls `reset()`) and "Go Home" actions. Logs errors to console via `useEffect` without exposing internals to the user. |

## Key decisions
- Uses `reset()` from Next.js props for the "Try Again" action (re-renders the route segment) rather than `window.location.reload()` — this is the idiomatic Next.js approach and avoids a full page reload.
- Error is logged to `console.error` inside a `useEffect` for debugging, but never displayed in the UI (no stack traces, messages, or digests shown).
- Component is fully client-side with no server-fetched data dependencies, ensuring it renders even when SSR fails completely.
- Button styles use Tailwind classes consistent with `ErrorPageLayout` and other error pages in the project.

## How it connects
- Uses `ErrorPageLayout` from `@/components/errors/ErrorPageLayout` (created in step 148)
- Uses i18n keys from `errorPages.serverError.*` namespace (added in step 147)
- Will be triggered by the axios interceptor redirect on 5xx responses (task 6.1)
- Shares visual design with 401, 403, and 404 error pages

## How to run / verify
```bash
cd apps/web
npx next build   # Verify no build errors
```
To manually test: trigger a server-side error in any route segment and confirm the branded 500 page renders with "Try Again" and "Go Home" actions.

## What comes next
- Task 3.5: Unit tests for all error pages
- Task 6.1: Axios interceptor update to redirect 5xx responses to this page

## Git commit
```bash
git add -A && git commit -m "feat(error-pages): add 500 internal server error page"
```
