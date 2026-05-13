# 207 — Register Page Redirect Preservation

## Phase

Bugfix — Invitation Flow Fixes

## Purpose

When a user visits the register page with a `redirect` query parameter (e.g., from an invitation link), the redirect URL was lost after registration. The user would be sent to `/login?registered=1` without the redirect, causing them to land on `/schedule/today` instead of completing the join flow.

## What was built

- **`apps/web/app/register/page.tsx`** — Modified to:
  - Import `useSearchParams` from `next/navigation` and `Suspense` from React
  - Read the `redirect` query parameter from the URL
  - Preserve the redirect param through registration by appending it to the login URL
  - Wrap the form in a `Suspense` boundary (required by Next.js for `useSearchParams()`)

## Key decisions

- Used the same `Suspense` wrapper pattern as the login page for consistency
- Used `encodeURIComponent` to safely encode the redirect URL in the query string
- When no redirect param is present, behavior is unchanged (`/login?registered=1`)
- The login page already handles the `redirect` param, so no changes needed there

## How it connects

- The login page reads `redirect` from search params and navigates to it after successful login
- The invitation join link sets `redirect` when sending users to register
- This fix closes the gap where the redirect was lost between register → login

## How to run / verify

1. Visit `/register?redirect=/groups/join?code=ABC123`
2. Complete registration
3. Verify you're redirected to `/login?registered=1&redirect=%2Fgroups%2Fjoin%3Fcode%3DABC123`
4. Log in and verify you're redirected to `/groups/join?code=ABC123`

## What comes next

- Task 5.1: Fix API client 401 handling to redirect to `/login` instead of error page

## Git commit

```bash
git add -A && git commit -m "fix(invitation): preserve redirect query param through registration flow"
```
