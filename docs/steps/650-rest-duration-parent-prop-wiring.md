# 650 — Pass minRestHours from Parent Schedule Page to ScheduleTaskTable

## Phase

Feature — Rest Duration Display (Task 3.3)

## Purpose

Wire the group-level `minRestBetweenShiftsHours` setting from the group page state through `ScheduleTab` down to `ScheduleTaskTable`, enabling the rest duration badges to use the correct threshold for color-coding.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx` | Added `minRestBetweenShiftsHours?: number` to Props interface, destructured it, and passed it as `minRestHours` to `ScheduleTaskTable` |
| `apps/web/app/groups/[groupId]/page.tsx` | Passed `minRestBetweenShiftsHours` state (from `useGroupPageState`) to `ScheduleTab` |

## Key decisions

- Only the `ScheduleTab` in the group detail page receives the prop — the today/tomorrow/my-missions pages don't pass `isAdmin`, so rest badges won't render there regardless.
- The `DraftScheduleModal` also doesn't pass `isAdmin`, so no change needed there.
- The prop is optional (`minRestBetweenShiftsHours?: number`) to maintain backward compatibility.
- The value comes from `useGroupPageState` which initializes it from the group API response (`found.minRestBetweenShiftsHours ?? 8`).

## How it connects

- **Upstream**: `useGroupPageState` loads `minRestBetweenShiftsHours` from the group settings API response.
- **Downstream**: `ScheduleTaskTable` uses `minRestHours` to enable rest duration computation and pass the threshold to `RestDurationBadge` for color-coding.
- **Requirements**: 4.4 (threshold from parent), 7.2 (no additional API calls).

## How to run / verify

1. Navigate to a group page → Schedule tab
2. Verify rest duration badges appear with correct color-coding relative to the group's min rest setting
3. Change the min rest hours in Settings tab, return to Schedule tab — badges should reflect the new threshold
4. TypeScript compilation: `npx tsc --noEmit` in `apps/web`

## What comes next

- Task 3.4: Unit tests for RestDurationBadge and ScheduleTaskTable integration

## Git commit

```bash
git add -A && git commit -m "feat(rest-duration): pass minRestHours from group page through ScheduleTab to ScheduleTaskTable"
```
