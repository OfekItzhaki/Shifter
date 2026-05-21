# 451 — TaskInfoBadge Integration into ScheduleTable2D

## Phase

Recommendation Approval Flow — Task Info Badges in Schedule Grid

## Purpose

Integrate the `TaskInfoBadge` component into the `ScheduleTable2D` column headers so that admins can see an info icon next to each task name and quickly check task configuration without navigating away from the schedule view.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/schedule/ScheduleTable2D.tsx` | Added optional `taskConfigurations` prop (`Record<string, TaskConfigSummaryDto>`), imported `TaskInfoBadge`, and rendered it next to each task name in `<th>` column headers |

## Key decisions

- The `taskConfigurations` prop is optional to maintain backward compatibility — existing usages of `ScheduleTable2D` without task config data continue to work without changes.
- The badge is not rendered for home-leave task columns since those are system-generated and don't have user-configurable settings.
- When `taskConfigurations` is provided but a specific task is missing from the map, `null` is passed to `TaskInfoBadge`, which renders nothing (graceful degradation per Requirement 7.3).
- Used an `inline-flex` wrapper with `gap-1` to align the badge next to the task name without breaking the existing layout.

## How it connects

- Depends on `TaskInfoBadge` (step 449) and `TaskInfoPopover` (step 448) components.
- The `taskConfigurations` data will be wired from the schedule data fetch in task 8.2.
- Satisfies Requirements 5.1 (badge adjacent to each task name) and 7.1 (receives config as part of schedule data).

## How to run / verify

1. The component accepts the new prop without breaking existing usages (prop is optional).
2. TypeScript diagnostics pass with no errors.
3. When `taskConfigurations` is passed, each non-home-leave task column header shows the ℹ badge.
4. When `taskConfigurations` is undefined or a task is missing from the map, no badge is shown.

## What comes next

- Task 8.2: Wire the schedule data fetching to pass `taskConfigurations` to `ScheduleTable2D`.
- Tasks 6.4–6.6: Property and unit tests for badge presence, accessibility, and popover display.

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval): integrate TaskInfoBadge into ScheduleTable2D column headers"
```
