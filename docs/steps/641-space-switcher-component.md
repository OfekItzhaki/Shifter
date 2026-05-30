# 641 — Space Switcher Component

## Phase

Space-First Onboarding (Task 11)

## Purpose

Implements the Space Switcher component that allows users to switch between their spaces from the sidebar. Ensures cache invalidation on switch, handles invalid persisted spaces, and only shows the dropdown when the user has multiple spaces.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/shell/SpaceSwitcher.tsx` | Updated component with react-query cache invalidation, invalid space handling, single-space display mode, error/retry state, and ARIA attributes |

## Key decisions

- **Single space = no dropdown**: When user has only 1 space, the switcher displays the name without a clickable dropdown (per Requirement 7.2)
- **Cache invalidation via react-query**: On space switch, `queryClient.invalidateQueries()` is called to clear all cached data so the new space's data is fetched fresh (per Requirement 7.3)
- **Invalid persisted space handling**: On mount, if the stored `currentSpaceId` is not found in the user's spaces list, the store is cleared and the first available space is selected (per Requirement 7.7)
- **Error state with retry**: If the spaces API call fails, a retry button is shown in the dropdown (per Requirement 7.8)
- **Name truncation at 30 chars**: Space name is truncated with ellipsis if it exceeds 30 characters (per Requirement 7.5)
- **"+ Create New Space" option**: Always shown at the bottom of the dropdown, navigates to `/onboarding`

## How it connects

- Uses `spaceStore` (Zustand) for persisting the active space across page reloads
- Uses `getMySpaces()` from `lib/api/spaces.ts` to fetch the user's space list
- Uses `useQueryClient()` from `@tanstack/react-query` to invalidate all cached queries on switch
- Integrated into `AppShell.tsx` sidebar below the logo area
- The `+ Create New Space` option navigates to the onboarding wizard at `/onboarding`

## How to run / verify

1. Log in with a user that belongs to multiple spaces
2. Verify the space name appears in the sidebar, truncated if > 30 chars
3. Click the space name — dropdown should appear with all spaces
4. Switch to a different space — data should refresh (react-query cache invalidated)
5. Log in with a user that has only 1 space — no dropdown chevron, no clickable behavior
6. Manually corrupt localStorage `jobuler-space` with an invalid spaceId — on reload, it should auto-select the first valid space

## What comes next

- Task 12: Space Settings Page
- Task 13: Redirect Logic (uses the space switcher's store for routing decisions)

## Git commit

```bash
git add -A && git commit -m "feat(space-onboarding): space switcher with cache invalidation and invalid space handling"
```
