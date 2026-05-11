# Step 149 — 403 Forbidden Error Page

## Phase

Custom Error Pages — Error Page Implementation

## Purpose

Provides a branded, user-friendly 403 Forbidden page that is displayed when a user attempts to access a resource they don't have permission for. The page guides users to navigate home or go back, without exposing any internal permission details.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/error/forbidden/page.tsx` | Client component that renders the 403 Forbidden error page using `ErrorPageLayout`, with "Go Home" and "Go Back" actions |

## Key decisions

- **Suspense boundary**: The page wraps `useSearchParams()` in a Suspense boundary (via inner `ForbiddenContent` component) to comply with Next.js App Router requirements for client-side search params.
- **Go Back logic**: If a `?from=` query parameter is present, the "Go Back" button navigates to that path via `router.push(from)`. Otherwise, it uses `router.back()` for browser history navigation.
- **No internal details**: The page only shows generic "Access Denied" messaging — no permission names, role identifiers, or API response bodies are ever rendered.
- **Consistent styling**: Uses Tailwind classes matching the app's design system with dark mode support and WCAG 2.1 AA compliant contrast/touch targets.

## How it connects

- Uses `ErrorPageLayout` from `@/components/errors/ErrorPageLayout` (step 148)
- Uses i18n keys from `errorPages.forbidden.*` namespace (step 147)
- Will be the redirect target for the axios interceptor on 403 responses (task 6.1)
- Sits at `/error/forbidden` route, matching the interceptor's redirect path

## How to run / verify

```bash
# Start the dev server and navigate to /error/forbidden
# Or /error/forbidden?from=/some-page to test the "from" parameter
cd apps/web && npm run dev
```

Visit `http://localhost:3000/error/forbidden` — should see the branded 403 page with "Go Home" and "Go Back" buttons.

## What comes next

- Task 3.4: Create the 500 Error page (`apps/web/app/error.tsx`)
- Task 3.5: Write unit tests for all error pages
- Task 6.1: Update axios interceptor to redirect to `/error/forbidden` on 403 responses

## Git commit

```bash
git add -A && git commit -m "feat(error-pages): add 403 forbidden page with go-back navigation"
```
