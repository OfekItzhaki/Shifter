# 628 — Wire Reused Tab Components into PickPage

## Phase

Feature: Shift Picker Lite

## Purpose

Connects the existing `SlotBrowserTab` and `MyShiftsTab` components into the `/pick` route page, completing the slot-browser phase UI. This enables members to actually browse available shifts and view their current shift requests within the lightweight picker interface.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/pick/page.tsx` | Added lazy imports for `SlotBrowserTab` and `MyShiftsTab`; replaced placeholder tab content with actual component rendering using `Suspense` fallback and `refreshKey`-based remounting |

## Key decisions

1. **Lazy loading with `React.lazy`** — Both tab components are lazy-loaded for code splitting, matching the pattern used in the main group detail page. This keeps the initial bundle small for the mobile-optimized `/pick` route.
2. **`Suspense` fallback with `LoadingCard`** — While tab components load, a skeleton placeholder (`LoadingCard` with `variant="slots"`) is shown, satisfying requirement 6.6 (skeleton placeholders while data loads).
3. **`refreshKey` on component `key` prop** — When the user taps refresh, `refreshKey` increments, causing React to unmount and remount the active tab component. This triggers a fresh data fetch without needing imperative ref callbacks.
4. **`currentSpaceId` guard** — The slot-browser phase only renders when `currentSpaceId` is available, ensuring the tab components always receive a valid `spaceId` prop.
5. **`isAdmin={false}`** — The picker is member-only, so `SlotBrowserTab` always receives `isAdmin=false`.

## How it connects

- Depends on: `SlotBrowserTab` (task from self-service-scheduling spec), `MyShiftsTab` (same), `PickerTabs` (task 7.1), `PickerHeader` (task 5.1), `LoadingCard`/`ErrorRetry` (existing shared components)
- The `refreshKey` state was introduced in task 8.1 and is now consumed here
- The `handleRefresh` callback in `PickerHeader` increments `refreshKey`, which remounts the active tab

## How to run / verify

1. Navigate to `/pick` while authenticated with a member that belongs to a self-service group
2. Select a group (or have a valid last-group memory)
3. Verify the "משמרות פנויות" (slots) tab renders `SlotBrowserTab` content
4. Switch to "המשמרות שלי" (my-shifts) tab and verify `MyShiftsTab` renders
5. Tap the refresh button and verify the tab content remounts (loading skeleton briefly appears)
6. Verify skeleton placeholder shows while lazy-loaded components are loading

## What comes next

- Task 9.1: Mobile-optimized layout and RTL styling for the `/pick` route
- Task 11.x: Property tests for slot sorting, capacity formatting, and cancellation eligibility

## Git commit

```bash
git add -A && git commit -m "feat(shift-picker-lite): wire SlotBrowserTab and MyShiftsTab into PickPage"
```
