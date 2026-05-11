# Implementation Plan: Custom Error Pages

## Overview

Implement branded, user-friendly error pages (401, 403, 404, 500) and an enhanced ErrorBoundary for the Shifter scheduling application. All error states share a consistent `ErrorPageLayout` component, support i18n (en, he, ru with RTL), dark mode, responsive design, and WCAG 2.1 AA compliance. The axios interceptor is updated to centralize error routing with a redirect guard preventing duplicate navigations.

## Tasks

- [x] 1. Set up testing infrastructure and shared layout component
  - [x] 1.1 Install Vitest, fast-check, @testing-library/react, and axe-core dev dependencies
    - Add `vitest`, `@vitejs/plugin-react`, `jsdom`, `fast-check`, `@testing-library/react`, `@testing-library/jest-dom`, `axe-core`, and `@axe-core/react` to devDependencies
    - Create `vitest.config.ts` at `apps/web/` with jsdom environment and path aliases matching `tsconfig.json`
    - Add `"test": "vitest --run"` script to `package.json`
    - _Requirements: Testing infrastructure for 3.5, 4.5, 5.5, 6.5_

  - [x] 1.2 Create the shared `ErrorPageLayout` component
    - Create `apps/web/components/errors/ErrorPageLayout.tsx`
    - Implement the `ErrorPageLayoutProps` interface: `heading`, `message`, `statusCode?`, `children`
    - Center content vertically/horizontally with `min-h-screen`
    - Render `<ShifterLogo size={40} />` centered above heading
    - Render optional status code as large muted numeral
    - Render heading as `<h1>`, message as `<p>` with muted color
    - Render children (actions) below message with gap spacing
    - Apply `dark:` Tailwind variants for all colors (`text-gray-900 dark:text-gray-100`, `bg-white dark:bg-slate-900`)
    - Ensure all interactive elements have `min-h-[44px] min-w-[44px]` touch targets
    - Add `focus-visible` outlines on interactive elements
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [ ]* 1.3 Write unit tests for ErrorPageLayout
    - Test that ShifterLogo renders at size 40
    - Test that heading renders as h1
    - Test that status code renders when provided and is absent when null
    - Test that children (action slots) render correctly
    - Run axe-core accessibility check on rendered output
    - _Requirements: 6.2, 6.5, 6.6_

- [x] 2. Implement i18n messages for error pages
  - [x] 2.1 Add `errorPages` namespace to all locale files
    - Update `apps/web/messages/en.json` with the `errorPages` namespace (notFound, unauthorized, forbidden, serverError, clientError keys)
    - Update `apps/web/messages/he.json` with Hebrew translations for all error page strings
    - Update `apps/web/messages/ru.json` with Russian translations for all error page strings
    - _Requirements: 1.4, 2.2, 3.2, 4.2, 5.1_

- [x] 3. Implement error pages
  - [x] 3.1 Create the 404 Not Found page (`apps/web/app/not-found.tsx`)
    - Client component using `useTranslations("errorPages")`
    - Render `ErrorPageLayout` with `statusCode={404}`, translated heading and message
    - Provide "Go Home" link to `/` styled as button with 44×44px minimum target
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 3.2 Create the 401 Unauthorized page (`apps/web/app/error/unauthorized/page.tsx`)
    - Client component (`"use client"`)
    - On mount (`useEffect`): clear `access_token` and `refresh_token` from localStorage
    - Use `useTranslations("errorPages")` for all text
    - Render `ErrorPageLayout` with `statusCode={401}`, session-expired messaging
    - Primary action: Link to `/login` styled as button (44×44px minimum)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 3.3 Create the 403 Forbidden page (`apps/web/app/error/forbidden/page.tsx`)
    - Client component (`"use client"`)
    - Read `?from=` query parameter via `useSearchParams()`
    - Use `useTranslations("errorPages")` for all text
    - Render `ErrorPageLayout` with `statusCode={403}`, forbidden messaging
    - Actions: "Go Home" link to `/` and "Go Back" button using `router.back()` or navigating to `from` path
    - Never expose internal permission details, role names, or API response bodies
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 3.4 Create the 500 Error page (`apps/web/app/error.tsx`)
    - Client component (Next.js requirement for `error.tsx`)
    - Receive `error` and `reset` props from Next.js
    - Use `useTranslations("errorPages")` for all text
    - Render `ErrorPageLayout` with `statusCode={500}`, server-error messaging
    - Actions: "Try Again" button calling `reset()` or `window.location.reload()`, and "Go Home" link
    - Never display stack traces or error internals to the user
    - Must render client-side without depending on server-fetched data
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [ ]* 3.5 Write unit tests for all error pages
    - Test 404 page renders ShifterLogo, h1 heading, and home link
    - Test 404 page renders in each locale (en, he, ru) without raw i18n keys
    - Test 404 page has `dir="rtl"` when locale is Hebrew
    - Test 401 page clears localStorage tokens on mount
    - Test 401 page shows session-expired message and login link
    - Test 403 page shows forbidden message with home and back links
    - Test 403 page does not expose internal details
    - Test 500 page shows error message with reload and home actions
    - Test 500 page renders without server data dependency
    - Run axe-core accessibility checks on each page render
    - _Requirements: 1.1–1.5, 2.2–2.5, 3.1–3.5, 4.1–4.6_

- [x] 4. Checkpoint - Ensure error pages render correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Enhance ErrorBoundary with branded fallback
  - [x] 5.1 Upgrade `apps/web/components/ErrorBoundary.tsx`
    - Keep class component structure, add `componentDidCatch` that logs `error` and `errorInfo.componentStack` to `console.error`
    - Extract fallback rendering to a functional `ErrorFallback` component that uses `useTranslations("errorPages")`
    - `ErrorFallback` renders `ErrorPageLayout` with "Something Went Wrong" messaging
    - In development (`process.env.NODE_ENV === "development"`): show `error.message` in fallback
    - In production: hide all error details
    - Actions: "Reload" button (`window.location.reload()`) and "Go Home" link (`/`)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [ ]* 5.2 Write unit tests for enhanced ErrorBoundary
    - Test that ErrorBoundary catches errors and renders fallback UI
    - Test that `console.error` is called with error and componentStack
    - Test that error message is shown in development mode
    - Test that error message is hidden in production mode
    - Test that "Reload" and "Go Home" actions are present
    - Run axe-core accessibility check on fallback UI
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

- [x] 6. Update axios interceptor with error routing
  - [x] 6.1 Update `apps/web/lib/api/client.ts` with redirect guard and status code routing
    - Add module-level `let isRedirecting = false` flag
    - Implement `redirectToErrorPage(path: string)` helper that checks the flag, sets it, appends `?from=` with encoded `window.location.pathname`, and assigns `window.location.href`
    - Update response interceptor error handling order: 401 (existing refresh logic) → on refresh failure redirect to `/error/unauthorized` instead of `/login` → 403 redirect to `/error/forbidden` → 500/502/503/504 redirect to `/error/server-error` → 404 no redirect (reject with original error)
    - After triggering any redirect, still reject the promise with the original error
    - Concurrent redirect prevention: if `isRedirecting` is true, skip redirect but still reject promise
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [ ]* 6.2 Write unit tests for axios interceptor error routing
    - Test that 403 response triggers redirect to `/error/forbidden?from=...` and rejects promise
    - Test that 500/502/503/504 responses trigger redirect to `/error/server-error?from=...`
    - Test that 404 response does NOT redirect and rejects with original error including status and body
    - Test that 401 refresh failure redirects to `/error/unauthorized`
    - Test status code evaluation order: 401 before 403 before 5xx
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.6_

  - [ ]* 6.3 Write property test: Redirect "from" parameter preserves current pathname
    - **Property 2: Redirect "from" parameter preserves current pathname**
    - Generate arbitrary valid URL pathname strings (with path segments, special characters)
    - Assert that the redirect URL's decoded `from` query parameter equals the original pathname
    - Minimum 100 iterations with fast-check
    - **Validates: Requirements 7.4**

  - [ ]* 6.4 Write property test: Concurrent error redirect idempotence
    - **Property 3: Concurrent error redirect idempotence**
    - Generate N (≥2) concurrent API error responses with redirect-triggering status codes (403, 500, 502, 503, 504)
    - Assert that exactly one redirect call is triggered regardless of order or timing
    - Minimum 100 iterations with fast-check
    - **Validates: Requirements 7.5**

- [x] 7. Checkpoint - Ensure interceptor and ErrorBoundary tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Property test for information leakage and final wiring
  - [ ]* 8.1 Write property test: No internal details leakage in production error UIs
    - **Property 1: No internal details leakage in production error UIs**
    - Generate random error objects with arbitrary message strings, stack traces, permission names, role identifiers, and API response bodies
    - Render each error page component (401, 403, 404, 500) and ErrorBoundary in production mode
    - Assert that rendered HTML output does NOT contain any of the generated internal detail strings
    - Minimum 100 iterations with fast-check
    - **Validates: Requirements 3.4, 4.5, 5.5**

  - [x] 8.2 Wire ErrorBoundary into the app shell
    - Ensure `ErrorBoundary` wraps the main content in `apps/web/app/layout.tsx` or `providers.tsx`
    - Verify ErrorBoundary sits inside `NextIntlClientProvider` so translations are available in fallback
    - _Requirements: 5.1, 6.6_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The project currently has no Vitest setup — task 1.1 establishes the unit/property test infrastructure
- `fast-check` is used as the PBT library for TypeScript
- `axe-core` is used for automated accessibility validation
- The existing `ErrorBoundary.tsx` is upgraded in-place (not replaced with a new file)
- The axios interceptor redirect to `/login` on 401 refresh failure is changed to `/error/unauthorized`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "3.1", "3.2", "3.3", "3.4"] },
    { "id": 3, "tasks": ["3.5", "5.1", "6.1"] },
    { "id": 4, "tasks": ["5.2", "6.2", "6.3", "6.4"] },
    { "id": 5, "tasks": ["8.1", "8.2"] }
  ]
}
```
