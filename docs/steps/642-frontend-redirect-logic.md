# 642 — Frontend Redirect Logic

## Phase

Space-First Onboarding (Task 13)

## Purpose

Implements centralized redirect logic that ensures users are routed to the correct page based on their space membership status. This covers: redirecting to onboarding when no spaces exist, ensuring a valid space is selected, preventing users with spaces from accessing onboarding, handling the edge case of users removed from all spaces, and triggering migration for existing users who have groups but no spaces.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/hooks/useSpaceGuard.ts` | **New** — Central hook encapsulating all space membership redirect logic including migration trigger |
| `apps/web/components/shell/AppShell.tsx` | **Modified** — Replaced inline space-check useEffect with `useSpaceGuard` hook |
| `apps/web/app/onboarding/page.tsx` | **Modified** — Replaced inline redirect useEffect with `useSpaceGuard` hook |
| `apps/web/components/shell/SpaceSwitcher.tsx` | **Modified** — Added redirect to `/onboarding` when user is removed from all spaces |

## Key decisions

1. **Centralized hook pattern** — All redirect logic lives in `useSpaceGuard` rather than being duplicated across components. Both `AppShell` and the onboarding page use the same hook.
2. **Migration-first approach** — When a user has no spaces, the guard first attempts `migrateUserSpace()` before redirecting to onboarding. This handles existing users with groups seamlessly.
3. **Graceful degradation** — If migration fails (network error, no groups to migrate), the user is simply sent to onboarding. No error is shown for this background operation.
4. **Guard ref prevents double-execution** — Uses a `useRef` flag to prevent the async check from running twice in React strict mode.
5. **SpaceSwitcher as secondary guard** — The SpaceSwitcher also redirects to `/onboarding` if it detects the user has been removed from all spaces (edge case during active session).

## How it connects

- Depends on: `spaceStore` (Zustand), `authStore` (Zustand), `getMySpaces()` API, `migrateUserSpace()` API
- Used by: `AppShell` (wraps all authenticated pages), `OnboardingPage`
- Satisfies: Requirement 12 (Onboarding Flow Redirect Logic) from the space-first-onboarding spec

## How to run / verify

1. **No spaces → onboarding**: Log in as a user with no space memberships → should redirect to `/onboarding`
2. **Has spaces → valid selection**: Log in as a user with spaces → should land on `/home` with a valid space selected
3. **On onboarding with spaces**: Navigate directly to `/onboarding` while having spaces → should redirect to `/home`
4. **Removed from all spaces**: Remove a user from their only space via API, then refresh → should clear store and redirect to `/onboarding`
5. **Migration trigger**: Log in as an existing user who has groups but no space memberships → should auto-create a space via migration and redirect to `/home`

## What comes next

- Task 14: Frontend — Linked Group UI
- Task 15: Update Onboarding Store for Per-Space State

## Git commit

```bash
git add -A && git commit -m "feat(spaces): centralized frontend redirect logic with migration trigger"
```
