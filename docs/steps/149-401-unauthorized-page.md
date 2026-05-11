# 149 — 401 Unauthorized Page

## Phase
Custom Error Pages — Error Page Implementation

## Purpose
Provides a branded, user-friendly page when a user's session has expired and token refresh has failed. Clears stale tokens from localStorage and guides the user to log in again.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/error/unauthorized/page.tsx` | Client component that clears localStorage tokens on mount and renders the session-expired error page with a login link |

## Key decisions
- Uses `useEffect` with empty dependency array to clear tokens immediately on mount, before the user sees the page content
- Styled the login link as a primary button matching the app's existing pattern (`bg-blue-500 hover:bg-blue-600 ... rounded-xl`)
- Ensures 44×44px minimum touch target via `min-h-[44px] min-w-[44px]` for WCAG compliance
- Uses `ErrorPageLayout` shared component for consistent branding across all error pages

## How it connects
- Triggered by the axios interceptor when a 401 response occurs and token refresh fails (redirects to `/error/unauthorized`)
- Uses `ErrorPageLayout` from `@/components/errors/ErrorPageLayout` (created in step 148)
- Uses i18n keys from `errorPages.unauthorized` namespace (added in step 147)
- Links to `/login` for re-authentication

## How to run / verify
- Navigate to `/error/unauthorized` in the browser — should see the session-expired page with ShifterLogo, 401 status code, heading, message, and login button
- Check localStorage in DevTools — `access_token` and `refresh_token` should be removed on page load
- Verify dark mode renders correctly
- Verify the login link navigates to `/login`

## What comes next
- 403 Forbidden page (`app/error/forbidden/page.tsx`)
- 500 Error page (`app/error.tsx`)
- Unit tests for all error pages
- Axios interceptor update to route errors to these pages

## Git commit

```bash
git add -A && git commit -m "feat(error-pages): add 401 unauthorized page with token cleanup"
```
