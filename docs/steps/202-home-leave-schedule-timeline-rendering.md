# 202 — Home-Leave Schedule Timeline Rendering

## Phase

Home-Leave Scheduling — Frontend Visualization

## Purpose

Display home-leave assignments on the schedule timeline/Gantt view with a distinct visual style so that admins and members can clearly distinguish between mission assignments and home-leave periods when viewing the schedule.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/schedule/ScheduleTaskTable.tsx` | Added home-leave detection (`isHomeLeaveTask`) and distinct emerald/green styling for the "בבית" task group — house icon, green header, green table borders/backgrounds, and suppressed "can't make it" button for leave slots |
| `apps/web/components/schedule/ScheduleTable2D.tsx` | Added home-leave column header styling (emerald text + background) and cell styling for the 2D grid view |
| `apps/web/components/schedule/ScheduleTable.tsx` | Added home-leave row styling with house icon, emerald text, and distinct source badge for the flat table view |
| `apps/web/components/schedule/ScheduleDiffView.tsx` | Added home-leave task name display ("בבית" with house icon) in the diff entry cards |

## Key decisions

| Decision | Rationale |
|----------|-----------|
| Used `"home_leave"` as the task type identifier | Matches the solver output and backend assignment storage (synthetic task_type = "home_leave") |
| Display label "בבית" (At Home) | Hebrew label matching the requirement spec (Requirement 9.5) |
| Emerald/green color scheme | Clearly distinguishes from regular mission assignments (blue/slate) without conflicting with existing status colors (amber for overrides, red for errors) |
| House icon (SVG) in headers | Provides immediate visual recognition of home-leave sections |
| Suppressed "can't make it" button for home-leave | Home-leave cancellation has its own flow (schedule override), not the same as blocking a person from a mission |
| Applied styling across all 4 schedule rendering components | Ensures consistent appearance regardless of which view the user is looking at |

## How it connects

- Depends on: Task 10.1 (publish service creates assignments with `task_type = "home_leave"`) and Task 8.3 (solver generates home-leave assignments)
- The `ScheduleTaskTable` is the primary view used in `ScheduleTab.tsx` for the group schedule page
- The `ScheduleTable2D` is used in the admin groups page
- The `ScheduleTable` is used in the admin schedule detail view
- The `ScheduleDiffView` shows changes between schedule versions

## How to run / verify

1. Create a closed-base group with home-leave config
2. Run the solver — it should produce home-leave assignments
3. View the schedule tab — home-leave assignments should appear as a separate "בבית" section with emerald/green styling and a house icon
4. Verify the diff view also shows home-leave changes with the "בבית" label

## What comes next

- Task 14: Checkpoint — verify frontend components render correctly
- Property-based tests for fairness warning threshold (Task 16.3)

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): render home-leave slots on schedule timeline with distinct styling"
```
