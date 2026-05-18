# 362 — Timezone-Aware Time Display Integration

## Phase
Feature: User Timezone Settings (Task 7.1)

## Purpose
Replace all direct date formatting (`toLocaleTimeString`, `toLocaleDateString`, `toLocaleString`) across the frontend with the centralized timezone-aware formatting utilities (`formatLocalTime`, `formatLocalDateTime`, `formatLocalDate` from `lib/utils/formatTime.ts`). This ensures all displayed times respect the user's configured timezone (IANA ID from authStore) and correctly handle DST transitions.

## What was built

### Core utility updates
- **`lib/utils/dateFormat.ts`** — Added `timezoneId` parameter to all formatting functions (`formatDate`, `formatDateLong`, `formatDateTime`, `formatTime`, `formatDateTimeShort`, `formatDateRange`). Each function now passes `timeZone` to `Intl.DateTimeFormat`. Defaults to `"Asia/Jerusalem"` when null.
- **`lib/hooks/useDateFormat.ts`** — Updated to pull `timezoneId` from `authStore` and pass it to all formatting functions. All components using this hook automatically get timezone-aware formatting.

### Schedule components updated
- **`components/schedule/ScheduleDiffView.tsx`** — Replaced inline `formatTime`/`formatDate` with `formatLocalTime`/`formatLocalDate` using `timezoneId` from authStore.
- **`components/schedule/ScheduleTable2D.tsx`** — Replaced inline `formatTime` with `formatLocalTime`.
- **`components/schedule/ScheduleTaskTable.tsx`** — Replaced inline `formatTime`/`formatShiftTime` with timezone-aware versions.
- **`components/schedule/ScheduleTable.tsx`** — Replaced inline `formatTime` with `formatLocalTime`.
- **`components/schedule/LiveStatusPanel.tsx`** — Replaced inline `formatTime` and `lastUpdated` display with timezone-aware formatting.
- **`components/schedule/OverrideModal.tsx`** — Replaced inline time formatting with `formatLocalTime`.

### Page-level components updated
- **`app/admin/schedule/page.tsx`** — Replaced `formatVersionTime`/`formatVersionDay`/`formatDateLabel` with timezone-aware versions.
- **`app/admin/tasks/page.tsx`** — Replaced inline `toLocaleString` with `formatLocalDateTime`.
- **`app/admin/logs/page.tsx`** — Replaced inline `toLocaleString` with `formatLocalDateTime`.
- **`app/admin/people/[personId]/page.tsx`** — Replaced `fmt` function with `formatLocalDateTime`.
- **`app/admin/stats/_components/StatsPeopleTable.tsx`** — Replaced inline `formatDate` with `formatLocalDate`.
- **`app/groups/[groupId]/page.tsx`** — Replaced cached date display with `formatLocalDateTime`.
- **`app/groups/[groupId]/tabs/ScheduleTab.tsx`** — Replaced inline slot formatting with `formatLocalTime`/`formatLocalDate`, added timezone to date comparison logic.
- **`app/groups/[groupId]/tabs/MembersTab.tsx`** — Replaced inline date display with `formatLocalDate`.
- **`app/schedule/today/page.tsx`** — Added `timeZone` option to date label.
- **`app/schedule/tomorrow/page.tsx`** — Added `timeZone` option to date label.
- **`app/profile/page.tsx`** — Replaced `formatBirthday`/`formatMemberSince`/credential dates with `formatLocalDate`.
- **`components/DraftScheduleModal.tsx`** — Added `timeZone` option to date label.
- **`components/sandbox/SandboxTasksTab.tsx`** — Replaced inline `toLocaleString` with `formatLocalDateTime`.

## Key decisions
1. **Updated `dateFormat.ts` as the primary integration point** — Since `useDateFormat` hook wraps `dateFormat.ts`, updating the utility layer means all hook consumers automatically get timezone support without code changes.
2. **Kept `formatTime.ts` for schedule-specific components** — Components that only need time (not date) use `formatLocalTime` directly for clarity and minimal imports.
3. **Timezone passed via authStore** — Each component reads `timezoneId` from `useAuthStore` rather than prop-drilling, keeping the API simple.
4. **Date comparison logic also uses timezone** — The `toLocaleDateString("sv", { timeZone })` pattern ensures UTC timestamps are correctly mapped to local dates for day-filtering.
5. **Static pages (terms, privacy) left unchanged** — These show "last updated" dates that are not user-specific timestamps.

## How it connects
- Depends on: Task 6.1 (authStore timezone fields), Task 6.2 (formatLocalTime utility)
- Consumed by: All time-rendering UI across the application
- Validates: Requirements 5.1 (apply timezone offset to all displayed times), 5.2 (centralized formatting utility), 5.3 (use IANA timezone ID for DST handling)

## How to run / verify
```bash
cd apps/web
npx tsc --noEmit  # TypeScript compilation — should pass with 0 errors
```

Visual verification:
1. Log in with a user that has a timezone set (e.g., "America/New_York")
2. Navigate to schedule views — times should display in the user's local timezone
3. Check admin logs, tasks, and stats pages — all timestamps should be timezone-aware
4. Change timezone in settings — all displayed times should update immediately

## What comes next
- Task 8.1: Settings page route and layout
- Task 8.2: Country/State selection UI
- Task 8.3: Move settings sections from Profile to Settings page

## Git commit
```bash
git add -A && git commit -m "feat(timezone): integrate formatLocalTime across all time-rendering components"
```
