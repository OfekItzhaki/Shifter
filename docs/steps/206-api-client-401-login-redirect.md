# 206 — API Client 401 Login Redirect

## Phase

Bugfix — Invitation Flow Fixes (Task 5.1)

## Purpose

When a user's access token expires and the refresh token also fails, the API client was redirecting to `/error/unauthorized` — a dead-end error page requiring the user to manually click "Login". This fix changes the behavior to silently redirect to `/login` with the current path preserved as a `redirect` query parameter, so the user can seamlessly resume their session after re-authenticating.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/api/client.ts` | Replaced `redirectToErrorPage("/error/unauthorized")` in the 401 refresh-failure branch with a direct `window.location.href = "/login?redirect=..."` redirect that preserves the current path |

## Key decisions

- Used the existing `isRedirecting` guard to prevent multiple concurrent 401 failures from triggering multiple redirects (same pattern as other error handlers)
- Preserved `window.location.pathname + window.location.search` in the redirect param so the user returns to the exact page (including query params) after login
- Kept all token cleanup logic (`localStorage.removeItem`, cookie clearing) unchanged
- Left 403 handling completely untouched — it still uses `redirectToErrorPage("/error/forbidden")`

## How it connects

- **Login page**: Already supports a `redirect` query parameter and navigates to it after successful authentication
- **Register page** (Task 4.1): Preserves the redirect param through registration so the full flow works end-to-end
- **Preservation tests** (Task 2): Verified that 403 handling remains unchanged

## How to run / verify

1. Log in to the app, then manually clear `access_token` and `refresh_token` from localStorage
2. Trigger any API call (navigate to a page that fetches data)
3. Observe redirect to `/login?redirect=<current_path>` instead of `/error/unauthorized`
4. Verify that 403 errors still redirect to `/error/forbidden`

## What comes next

- Task 6: Checkpoint — run full test suite to verify all bug condition and preservation tests pass

## Git commit

```bash
git add -A && git commit -m "fix(web): redirect to /login on 401 refresh failure instead of error page"
```
