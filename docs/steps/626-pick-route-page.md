# 626 — /pick Route Page Component

## Phase

Shift Picker Lite — Task 8.1

## Purpose

Creates the `/pick` route page that orchestrates the shift picker experience. This is the main entry point for the lightweight mobile shift picker, implementing a state machine that handles authentication, group resolution, and view switching.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/pick/page.tsx` | Route page component with state machine (loading → group-select / slot-browser), auth guard, group fetching, last-group memory resolution, and tab switching |

## Key decisions

- **Client component**: Uses `"use client"` since it relies on hooks, localStorage, and zustand stores
- **No app shell**: Renders outside the main `AppShell` (no sidebar) — uses its own minimal layout with `PickerHeader`
- **Auth guard via useEffect**: Checks `isAuthenticated` from `authStore` and redirects to `/login?redirect=/pick` if not authenticated, matching the existing pattern used in other pages
- **State machine**: Three phases — `loading` (initial fetch), `group-select` (show group list), `slot-browser` (show tabs with slots/my-shifts)
- **Refresh via key prop**: Uses a `refreshKey` state that increments on refresh, causing the tab panel to remount and refetch data
- **Tab content placeholder**: The actual `SlotBrowserTab` and `MyShiftsTab` wiring is deferred to task 8.3

## How it connects

- Uses `authStore` and `spaceStore` from `lib/store/` for auth and space context
- Uses `getGroups` from `lib/api/groups.ts` to fetch member's groups
- Uses `filterSelfServiceGroups` from `lib/utils/pickGroupFilter.ts` to filter groups
- Uses `getLastGroup`, `setLastGroup`, `clearLastGroup`, `resolveLastGroup` from `lib/utils/pickLastGroup.ts`
- Renders `PickerHeader`, `GroupSelector`, `PickerTabs` from `components/pick/`
- Renders `LoadingCard` and `ErrorRetry` from `components/groups/selfService/`

## How to run / verify

1. Run the Next.js dev server: `cd apps/web && npm run dev`
2. Navigate to `http://localhost:3000/pick`
3. If not authenticated, should redirect to `/login?redirect=/pick`
4. If authenticated with a space selected, should show group selector or slot browser depending on last-group memory

## What comes next

- Task 8.2: Wire group selection and last-group memory updates
- Task 8.3: Wire `SlotBrowserTab` and `MyShiftsTab` into the slot-browser phase

## Git commit

```bash
git add -A && git commit -m "feat(shift-picker): create /pick route page with state machine and auth guard"
```
