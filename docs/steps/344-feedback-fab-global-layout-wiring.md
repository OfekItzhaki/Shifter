# 344 — Feedback FAB Global Layout Wiring

## Phase

Feature: Feedback & Bug Report FAB

## Purpose

Wire the `FeedbackFab` component into the global app layout so it renders on all authenticated pages without per-page imports.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/providers.tsx` | Imported `FeedbackFab` and `useAuthStore`; conditionally renders `<FeedbackFab />` when user is authenticated |

## Key decisions

- **Placed in `providers.tsx`** rather than a route-group layout — matches the design doc's recommendation and ensures the FAB appears on every authenticated page without needing a dedicated `(authenticated)` route group.
- **Conditional render via `useAuthStore`** — the FAB only appears when `isAuthenticated` is true, preventing it from showing on login, register, and other public pages.
- **Rendered outside `{children}`** — since the FAB uses `position: fixed`, its DOM position doesn't affect layout flow.

## How it connects

- Depends on `FeedbackFab` component (step 342)
- Depends on `useAuthStore` for authentication state
- Satisfies Requirement 1.1: FAB visible on all authenticated pages

## How to run / verify

1. Start the dev server: `npm run dev` (in `apps/web`)
2. Log in to the app
3. Confirm the split FAB appears at bottom-left on every page
4. Log out — confirm the FAB disappears on public pages (login, register, landing)

## What comes next

- Frontend verification checkpoint (task 4)
- Property-based tests and unit tests for the FAB and modal

## Git commit

```bash
git add -A && git commit -m "feat(feedback): wire FeedbackFab into global providers layout"
```
