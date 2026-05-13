# 208 — Mobile UI Bug Fixes

## Phase
Post-launch — Bug Fixes & Polish

## Purpose
Fix three mobile-specific UI bugs: statistics page stuck on loading spinner, 24h time format not working on mobile, and range slider thumb invisible on mobile.

## What was built

### Bug 1: Statistics page loading timeout
- **`apps/web/app/groups/[groupId]/tabs/StatsTab.tsx`** — Added a 15-second safety timeout to the `getBurdenStats()` call. If the API hangs (network issues), the loading state is cleared and a "Connection timeout — try again" error is shown. Uses a `cancelled` flag to prevent state updates after unmount.

### Bug 2: 24h time format on mobile
Added `hour12: false` to all `toLocaleTimeString()` calls to force 24-hour format regardless of device locale:
- **`apps/web/components/schedule/ScheduleTaskTable.tsx`** — `formatTime` function
- **`apps/web/components/schedule/ScheduleTable2D.tsx`** — `formatTime` function
- **`apps/web/components/schedule/ScheduleTable.tsx`** — inline `formatTime` const
- **`apps/web/components/schedule/ScheduleDiffView.tsx`** — `formatTime` function
- **`apps/web/components/schedule/LiveStatusPanel.tsx`** — `formatTime` function
- **`apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx`** — CSV export time formatting

### Bug 3: Range slider thumb invisible on mobile
- **`apps/web/app/globals.css`** — Added custom range input styles with explicit thumb sizing (20×20px blue circle with white border and shadow), track height, and dark mode support.
- **`apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx`** — Removed `accent-blue-500` class from range input since custom CSS now handles styling.

## Key decisions
- Used a safety timeout (15s) rather than AbortController for Bug 1, since `getBurdenStats` uses axios internally and passing a signal would require changing the API layer.
- Forced `hour12: false` globally in all schedule components rather than creating a shared utility, to keep changes minimal and localized.
- Used CSS pseudo-element selectors for range styling to ensure cross-browser compatibility (WebKit + Firefox).

## How it connects
- StatsTab is rendered inside the group detail page tabs.
- The schedule components are used across the schedule tab, diff view, and live status panel.
- The range slider is in the admin settings tab for solver horizon configuration.

## How to run / verify
1. Open the group page on a mobile device or emulator.
2. **Bug 1:** Navigate to the Stats tab — if the API is slow/unreachable, the spinner should disappear after 15 seconds with an error message.
3. **Bug 2:** Check any schedule view — times should display in 24h format (e.g., "14:30" not "2:30 PM").
4. **Bug 3:** Go to Settings tab → Planning Horizon slider — the blue thumb should be clearly visible and draggable.

## What comes next
- Consider extracting a shared `formatTime` utility to avoid duplication across schedule components.

## Git commit
```bash
git add -A && git commit -m "fix(mobile): stats timeout, 24h time format, range slider visibility"
```
